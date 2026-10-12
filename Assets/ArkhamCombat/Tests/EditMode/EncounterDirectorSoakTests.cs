using System.Collections.Generic;
using System.Linq;
using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // A minute of a messy fight on the shipped tuning: agents attack, are hit out of their attacks,
    // die and are replaced, while the target wanders and turns. The fight is played once and each
    // test asks one thing of what was seen.
    public class EncounterDirectorSoakTests
    {
        private const string CombatConfigPath = "Assets/ArkhamCombat/Settings/CombatConfig.asset";
        private const int Seed = 20261012;
        private const float FightSeconds = 60f;
        private const float TickSeconds = 1f / 30f;
        private const int AgentsInTheFight = 6;
        private const float AttackSeconds = 1.2f;
        private const float TelegraphSeconds = 0.6f;
        private const float SecondsBetweenDeaths = 7f;
        private const float SecondsBetweenTurnsOfTheTarget = 3f;
        private const float ChanceAnAttackerIsHitEachTick = 0.01f;
        private const float WalkingSpeed = 3.5f;
        private const float LongEnoughToExpectATurn = 25f;
        private const int FewestTurnsOfALivelyFight = 15;

        private sealed class Fighter : IEncounterAgent
        {
            public Vector3 Position { get; set; }
            public float JoinedAt;
            public float LeftAt = float.NaN;
            public int Turns;
            public float SecondsLeftInItsAttack;
            public float SecondsLeftTelegraphing;
            public bool IsTelegraphingACounterableAttack;

            public bool IsAttacking => SecondsLeftInItsAttack > 0f;
            public bool IsTelegraphing => SecondsLeftTelegraphing > 0f;
            public bool HasLeft => !float.IsNaN(LeftAt);

            public float SecondsInTheFight(float fightEndedAt) => (HasLeft ? LeftAt : fightEndedAt) - JoinedAt;
        }

        private EncounterDirectorSettings settings;
        private readonly List<Fighter> everyoneWhoFought = new List<Fighter>();
        private readonly List<string> timesAPoolWasOverdrawn = new List<string>();
        private readonly List<string> telegraphsThatOutlivedTheirAgent = new List<string>();
        private readonly List<string> timesTheCounterPromptWasWrong = new List<string>();
        private readonly List<string> placesNobodyCouldStand = new List<string>();
        private int turnsGranted;

        [OneTimeSetUp]
        public void PlayTheFightOnce()
        {
            settings = CombatStaticInstaller.EncounterSettingsOf(AssetDatabase.LoadAssetAtPath<CombatConfig>(CombatConfigPath));
            System.Random random = new System.Random(Seed);
            EncounterDirector director = new EncounterDirector(settings, slotValidator: null, random);
            director.TokenGranted += agent =>
            {
                ((Fighter)agent).Turns++;
                turnsGranted++;
            };

            List<Fighter> fighters = new List<Fighter>();
            Vector3 targetPosition = Vector3.zero;
            Vector3 targetForward = Vector3.forward;
            float nextDeathAt = SecondsBetweenDeaths;
            float nextTurnOfTheTargetAt = SecondsBetweenTurnsOfTheTarget;

            for (float now = 0f; now < FightSeconds; now += TickSeconds)
            {
                while (fighters.Count < AgentsInTheFight)
                {
                    fighters.Add(Join(director, random, targetPosition, now));
                }

                if (now >= nextTurnOfTheTargetAt)
                {
                    nextTurnOfTheTargetAt = now + SecondsBetweenTurnsOfTheTarget;
                    targetForward = SomeDirection(random);
                    targetPosition += SomeDirection(random);
                }

                if (now >= nextDeathAt)
                {
                    nextDeathAt = now + SecondsBetweenDeaths;
                    Fighter whoDies = fighters[random.Next(fighters.Count)];
                    director.Unregister(whoDies);
                    whoDies.LeftAt = now;
                    fighters.Remove(whoDies);
                }

                director.SetTarget(targetPosition, targetForward);
                foreach (Fighter fighter in fighters)
                {
                    WalkTowardWhereItStands(director, fighter);
                    FightForATick(director, fighter, random);
                }

                director.Tick(TickSeconds);
                RecordWhatIsWrong(director, fighters, targetPosition, now);
            }
        }

        private Fighter Join(EncounterDirector director, System.Random random, Vector3 targetPosition, float now)
        {
            Fighter fighter = new Fighter { Position = targetPosition + SomeDirection(random) * (settings.SlotRadius * 2f), JoinedAt = now };
            director.Register(fighter, AttackTokenType.Melee);
            everyoneWhoFought.Add(fighter);
            return fighter;
        }

        private static Vector3 SomeDirection(System.Random random)
        {
            float radians = (float)(random.NextDouble() * Mathf.PI * 2f);
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private static void WalkTowardWhereItStands(EncounterDirector director, Fighter fighter)
        {
            if (!fighter.IsAttacking)
            {
                fighter.Position = Vector3.MoveTowards(fighter.Position, director.GetStandingPosition(fighter), WalkingSpeed * TickSeconds);
            }
        }

        // What an enemy's tree does with its turn: ask every tick, wind up, strike, spend the attack; and stop at once when the turn is gone.
        private static void FightForATick(EncounterDirector director, Fighter fighter, System.Random random)
        {
            bool isItsTurn = director.TryAcquireToken(fighter);
            bool wasHitOutOfItsAttack = fighter.IsAttacking && random.NextDouble() < ChanceAnAttackerIsHitEachTick;

            if (!isItsTurn || wasHitOutOfItsAttack)
            {
                StopAttacking(director, fighter);
                if (wasHitOutOfItsAttack)
                {
                    director.ReleaseToken(fighter);
                }

                return;
            }

            if (!fighter.IsAttacking)
            {
                fighter.SecondsLeftInItsAttack = AttackSeconds;
                fighter.SecondsLeftTelegraphing = TelegraphSeconds;
                fighter.IsTelegraphingACounterableAttack = random.Next(2) == 0;
            }

            fighter.SecondsLeftInItsAttack -= TickSeconds;
            fighter.SecondsLeftTelegraphing -= TickSeconds;

            if (fighter.IsTelegraphing)
            {
                director.TelegraphAnAttack(fighter, fighter.IsTelegraphingACounterableAttack);
            }
            else
            {
                director.StopTelegraphing(fighter);
            }

            if (!fighter.IsAttacking)
            {
                director.SpendAnAttackOfTheTurn(fighter);
            }
        }

        private static void StopAttacking(EncounterDirector director, Fighter fighter)
        {
            fighter.SecondsLeftInItsAttack = 0f;
            fighter.SecondsLeftTelegraphing = 0f;
            director.StopTelegraphing(fighter);
        }

        private void RecordWhatIsWrong(EncounterDirector director, List<Fighter> fighters, Vector3 targetPosition, float now)
        {
            int tokensOut = director.HeldTokenCount(AttackTokenType.Melee);
            if (tokensOut > settings.MaxMeleeTokens)
            {
                timesAPoolWasOverdrawn.Add($"{now:0.00}s: {tokensOut} melee tokens out of {settings.MaxMeleeTokens}");
            }

            if (director.TryGetNearestTelegraphingAgent(targetPosition, out IEncounterAgent whoTelegraphs))
            {
                Fighter fighter = (Fighter)whoTelegraphs;
                if (fighter.HasLeft || !fighter.IsTelegraphing)
                {
                    telegraphsThatOutlivedTheirAgent.Add($"{now:0.00}s: an agent that {(fighter.HasLeft ? "left" : "stopped winding up")} is still named");
                }
            }

            bool isACounterableAttackComing = fighters.Any(fighter => fighter.IsTelegraphing && fighter.IsTelegraphingACounterableAttack);
            if (director.IsACounterableAttackIncoming != isACounterableAttackComing)
            {
                timesTheCounterPromptWasWrong.Add($"{now:0.00}s: the director says {director.IsACounterableAttackIncoming}, the fight says {isACounterableAttackComing}");
            }

            foreach (Fighter fighter in fighters)
            {
                Vector3 whereItStands = director.GetStandingPosition(fighter);
                if (float.IsNaN(whereItStands.x) || float.IsNaN(whereItStands.z))
                {
                    placesNobodyCouldStand.Add($"{now:0.00}s: {whereItStands}");
                }
            }
        }

        [Test]
        public void NeverMoreTokensAreOutThanThePoolHolds()
        {
            Assert.IsEmpty(timesAPoolWasOverdrawn);
        }

        [Test]
        public void NoTelegraphOutlivesTheAgentOrTheAttackThatMadeIt()
        {
            Assert.IsEmpty(telegraphsThatOutlivedTheirAgent);
        }

        [Test]
        public void TheCounterPrompt_IsOnExactlyWhileACounterableAttackIsBeingAnnounced()
        {
            Assert.IsEmpty(timesTheCounterPromptWasWrong);
        }

        [Test]
        public void EveryAgentAlwaysHasSomewhereToStand()
        {
            Assert.IsEmpty(placesNobodyCouldStand);
        }

        [Test]
        public void NobodyWhoFoughtLongEnough_WasLeftWithoutATurn()
        {
            string[] leftOut = everyoneWhoFought
                .Where(fighter => fighter.SecondsInTheFight(FightSeconds) >= LongEnoughToExpectATurn && fighter.Turns == 0)
                .Select(fighter => $"joined at {fighter.JoinedAt:0.0}s, fought {fighter.SecondsInTheFight(FightSeconds):0.0}s")
                .ToArray();

            Assert.IsEmpty(leftOut);
        }

        [Test]
        public void TheFightNeverStalls()
        {
            Assert.GreaterOrEqual(turnsGranted, FewestTurnsOfALivelyFight);
        }

        [Test]
        public void TheFight_WasAMessyOne()
        {
            Assert.Greater(everyoneWhoFought.Count, AgentsInTheFight, "sanity: agents died and were replaced");
            Assert.IsTrue(everyoneWhoFought.Any(fighter => fighter.Turns > 1), "sanity: somebody had more than one turn");
        }
    }
}
