using System.Collections.Generic;
using System.Linq;
using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Tests
{
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
        private const float RingRadiiAwayAFighterJoins = 2f;
        private const float RoundsAfterWhichEveryoneHasHadATurn = 1.35f;
        private const int TicksAGrantMayTake = 3;

        private sealed class Fighter : IEncounterAgent
        {
            public Vector3 Position { get; set; }
            public float JoinedAt;
            public float? LeftAt;
            public int Turns;
            public float SecondsLeftInItsAttack;
            public float SecondsLeftTelegraphing;
            public bool IsItsAttackCounterable;

            public bool IsAttacking => SecondsLeftInItsAttack > 0f;
            public bool IsTelegraphing => SecondsLeftTelegraphing > 0f;
            public bool HasLeft => LeftAt.HasValue;

            public float SecondsInTheFight(float fightEndedAt) => (LeftAt ?? fightEndedAt) - JoinedAt;
        }

        private EncounterDirectorSettings settings;
        private EncounterDirector director;
        private System.Random random;
        private readonly List<Fighter> fighters = new List<Fighter>();
        private readonly List<Fighter> everyoneWhoFought = new List<Fighter>();
        private readonly List<string> timesAPoolWasOverdrawn = new List<string>();
        private readonly List<string> telegraphsThatOutlivedTheirAgentOrTheirAttack = new List<string>();
        private readonly List<string> timesTheCounterPromptWasWrong = new List<string>();
        private readonly List<string> timesAnAgentHadNoPlaceOfItsOwn = new List<string>();
        private float secondsSinceAnyoneHadTheTurn;
        private float longestTimeNobodyHadTheTurn;
        private int attacksCutShortByAHit;
        private int deathsOfWhoeverHadTheTurn;

        private float SecondsAFullRoundOfTurnsTakes =>
            AgentsInTheFight * (settings.MaxAttacksPerTurn * AttackSeconds + settings.SecondsBetweenTurns);

        [OneTimeSetUp]
        public void PlayTheFightOnce()
        {
            CombatConfig config = AssetDatabase.LoadAssetAtPath<CombatConfig>(CombatConfigPath);
            Assert.IsNotNull(config, $"no combat config at {CombatConfigPath}");
            settings = config.Encounter;
            random = new System.Random(Seed);
            director = new EncounterDirector(settings, slotValidator: null, random);
            director.TokenGranted += agent => ((Fighter)agent).Turns++;

            Vector3 targetPosition = Vector3.zero;
            Vector3 targetForward = Vector3.forward;
            float nextDeathAt = SecondsBetweenDeaths;
            float nextTurnOfTheTargetAt = SecondsBetweenTurnsOfTheTarget;

            for (float now = 0f; now < FightSeconds; now += TickSeconds)
            {
                ReplaceWhoeverDied(targetPosition, now);

                if (now >= nextTurnOfTheTargetAt)
                {
                    nextTurnOfTheTargetAt = now + SecondsBetweenTurnsOfTheTarget;
                    targetForward = SomeDirection();
                    targetPosition += SomeDirection();
                }

                if (now >= nextDeathAt)
                {
                    nextDeathAt = now + SecondsBetweenDeaths;
                    KillSomeone(now);
                }

                director.SetTarget(targetPosition, targetForward);
                foreach (Fighter fighter in fighters)
                {
                    WalkTowardWhereItStands(fighter);
                    TakeItsTurnWhileItHasOne(fighter);
                }

                director.Tick(TickSeconds);
                RecordWhatIsWrong(now);
            }
        }

        private void ReplaceWhoeverDied(Vector3 targetPosition, float now)
        {
            while (fighters.Count < AgentsInTheFight)
            {
                Vector3 whereItJoins = targetPosition + SomeDirection() * (settings.SlotRadius * RingRadiiAwayAFighterJoins);
                Fighter fighter = new Fighter { Position = whereItJoins, JoinedAt = now };
                director.Register(fighter, AttackTokenType.Melee);
                fighters.Add(fighter);
                everyoneWhoFought.Add(fighter);
            }
        }

        private void KillSomeone(float now)
        {
            Fighter whoDies = fighters[random.Next(fighters.Count)];
            if (director.HasToken(whoDies))
            {
                deathsOfWhoeverHadTheTurn++;
            }

            director.Unregister(whoDies);
            whoDies.LeftAt = now;
            fighters.Remove(whoDies);
        }

        private Vector3 SomeDirection()
        {
            float radians = (float)(random.NextDouble() * Mathf.PI * 2f);
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private void WalkTowardWhereItStands(Fighter fighter)
        {
            if (!fighter.IsAttacking)
            {
                fighter.Position = Vector3.MoveTowards(fighter.Position, director.GetStandingPosition(fighter), WalkingSpeed * TickSeconds);
            }
        }

        private void TakeItsTurnWhileItHasOne(Fighter fighter)
        {
            bool isItsTurn = director.TryAcquireToken(fighter);
            if (!isItsTurn)
            {
                StopAttacking(fighter);
                return;
            }

            if (fighter.IsAttacking && IsHitThisTick())
            {
                BeHitOutOfItsAttack(fighter);
                return;
            }

            if (!fighter.IsAttacking)
            {
                StartAnAttack(fighter);
            }

            AdvanceItsAttack(fighter);
            ReportWhetherItIsWindingUp(fighter);

            bool hasTheAttackEnded = !fighter.IsAttacking;
            if (hasTheAttackEnded)
            {
                director.SpendAnAttackOfTheTurn(fighter);
            }
        }

        private bool IsHitThisTick() => random.NextDouble() < ChanceAnAttackerIsHitEachTick;

        private void BeHitOutOfItsAttack(Fighter fighter)
        {
            attacksCutShortByAHit++;
            StopAttacking(fighter);
            director.ReleaseToken(fighter);
        }

        private void StopAttacking(Fighter fighter)
        {
            fighter.SecondsLeftInItsAttack = 0f;
            fighter.SecondsLeftTelegraphing = 0f;
            director.StopTelegraphing(fighter);
        }

        private void StartAnAttack(Fighter fighter)
        {
            fighter.SecondsLeftInItsAttack = AttackSeconds;
            fighter.SecondsLeftTelegraphing = TelegraphSeconds;
            fighter.IsItsAttackCounterable = random.Next(2) == 0;
        }

        private static void AdvanceItsAttack(Fighter fighter)
        {
            fighter.SecondsLeftInItsAttack -= TickSeconds;
            fighter.SecondsLeftTelegraphing -= TickSeconds;
        }

        private void ReportWhetherItIsWindingUp(Fighter fighter)
        {
            if (fighter.IsTelegraphing)
            {
                director.TelegraphAnAttack(fighter, fighter.IsItsAttackCounterable);
            }
            else
            {
                director.StopTelegraphing(fighter);
            }
        }

        private void RecordWhatIsWrong(float now)
        {
            RecordAnOverdrawnPool(now);
            RecordTelegraphsThatOutlivedTheirAgentOrTheirAttack(now);
            RecordAWrongCounterPrompt(now);
            RecordAgentsWithNoPlaceOfTheirOwn(now);
            RecordHowLongNobodyHasHadTheTurn();
        }

        private void RecordAnOverdrawnPool(float now)
        {
            int tokensOut = director.HeldTokenCount(AttackTokenType.Melee);
            if (tokensOut > settings.MaxMeleeTokens)
            {
                timesAPoolWasOverdrawn.Add($"{now:0.00}s: {tokensOut} melee tokens out of {settings.MaxMeleeTokens}");
            }
        }

        private void RecordTelegraphsThatOutlivedTheirAgentOrTheirAttack(float now)
        {
            foreach (Fighter fighter in everyoneWhoFought)
            {
                bool shouldBeNamed = !fighter.HasLeft && fighter.IsTelegraphing;
                if (director.IsTelegraphing(fighter) && !shouldBeNamed)
                {
                    string whatItDid = fighter.HasLeft ? "left" : "stopped winding up";
                    telegraphsThatOutlivedTheirAgentOrTheirAttack.Add($"{now:0.00}s: an agent that {whatItDid} is still telegraphing");
                }
            }
        }

        private void RecordAWrongCounterPrompt(float now)
        {
            bool isACounterableAttackComing = fighters.Any(fighter => fighter.IsTelegraphing && fighter.IsItsAttackCounterable);
            if (director.IsACounterableAttackIncoming != isACounterableAttackComing)
            {
                timesTheCounterPromptWasWrong.Add(
                    $"{now:0.00}s: the director says {director.IsACounterableAttackIncoming}, the fight says {isACounterableAttackComing}");
            }
        }

        private void RecordAgentsWithNoPlaceOfTheirOwn(float now)
        {
            HashSet<int> slotsTaken = new HashSet<int>();
            foreach (Fighter fighter in fighters)
            {
                int slot = director.GetSlot(fighter);
                if (slot == EncounterDirector.UnassignedSlot)
                {
                    timesAnAgentHadNoPlaceOfItsOwn.Add($"{now:0.00}s: an agent has no slot on the ring");
                }
                else if (!slotsTaken.Add(slot))
                {
                    timesAnAgentHadNoPlaceOfItsOwn.Add($"{now:0.00}s: two agents share slot {slot}");
                }
            }
        }

        private void RecordHowLongNobodyHasHadTheTurn()
        {
            bool hasSomeoneTheTurn = director.HeldTokenCount(AttackTokenType.Melee) > 0;
            secondsSinceAnyoneHadTheTurn = hasSomeoneTheTurn ? 0f : secondsSinceAnyoneHadTheTurn + TickSeconds;
            longestTimeNobodyHadTheTurn = Mathf.Max(longestTimeNobodyHadTheTurn, secondsSinceAnyoneHadTheTurn);
        }

        [Test]
        public void NeverMoreTokensAreOutThanThePoolHolds()
        {
            Assert.IsEmpty(timesAPoolWasOverdrawn);
        }

        [Test]
        public void NoTelegraphOutlivesTheAgentOrTheAttackThatMadeIt()
        {
            Assert.IsEmpty(telegraphsThatOutlivedTheirAgentOrTheirAttack);
        }

        [Test]
        public void TheCounterPrompt_IsOnExactlyWhileACounterableAttackIsBeingAnnounced()
        {
            Assert.IsEmpty(timesTheCounterPromptWasWrong);
        }

        [Test]
        public void EveryAgentAlwaysHasASlotOnTheRing_ThatNobodyElseHas()
        {
            Assert.IsEmpty(timesAnAgentHadNoPlaceOfItsOwn);
        }

        [Test]
        public void NobodyWhoFoughtThroughAFullRoundOfTurns_WasLeftWithoutOne()
        {
            float longEnoughToExpectATurn = SecondsAFullRoundOfTurnsTakes * RoundsAfterWhichEveryoneHasHadATurn;
            string[] leftOut = everyoneWhoFought
                .Where(fighter => fighter.SecondsInTheFight(FightSeconds) >= longEnoughToExpectATurn && fighter.Turns == 0)
                .Select(fighter => $"joined at {fighter.JoinedAt:0.0}s, fought {fighter.SecondsInTheFight(FightSeconds):0.0}s")
                .ToArray();

            Assert.Less(longEnoughToExpectATurn, FightSeconds, "the fight is too short for the shipped tuning to say anything about turns");
            Assert.IsEmpty(leftOut);
        }

        [Test]
        public void TheFightNeverStalls_TheTurnIsNeverFreeForLongerThanTheBreathBetweenTurns()
        {
            float longestABreathMayLast = settings.SecondsBetweenTurns + TicksAGrantMayTake * TickSeconds;

            Assert.LessOrEqual(longestTimeNobodyHadTheTurn, longestABreathMayLast);
        }

        [Test]
        public void TheFight_WasAMessyOne()
        {
            Assert.Greater(everyoneWhoFought.Count, AgentsInTheFight, "agents died and were replaced");
            Assert.IsTrue(everyoneWhoFought.Any(fighter => fighter.Turns > 1), "somebody had more than one turn");
            Assert.Greater(attacksCutShortByAHit, 0, "somebody was hit out of an attack");
            Assert.Greater(deathsOfWhoeverHadTheTurn, 0, "somebody died on its turn");
        }
    }
}
