using System.Collections.Generic;
using ArkhamCombat.Encounter;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterTokenGrantTests
    {
        private const float GrantGraceSeconds = 0.5f;
        private const float ReacquireCooldownSeconds = 1.5f;
        private const float FairnessHorizonSeconds = 6f;
        private const float SlotRadius = 4f;
        private const float NoTime = 0f;
        private const float ALongTime = 60f;

        private static readonly Vector3 North = Vector3.forward;
        private static readonly Vector3 East = Vector3.right;
        private static readonly Vector3 South = Vector3.back;
        private static readonly Vector3 WhereTheTargetLooks = North;

        private static EncounterDirector Director(
            int maxMeleeTokens = 1,
            float fairnessWeight = 0.6f,
            float grantGraceSeconds = GrantGraceSeconds,
            float secondsBetweenTurns = 0f)
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(
                maxMeleeTokens: maxMeleeTokens,
                maxTokenHoldSeconds: 0f,
                reacquireCooldownSeconds: ReacquireCooldownSeconds,
                slotRadius: SlotRadius,
                fairnessWeight: fairnessWeight,
                fairnessHorizonSeconds: FairnessHorizonSeconds,
                grantGraceSeconds: grantGraceSeconds,
                secondsBetweenTurns: secondsBetweenTurns));
            director.SetTarget(Vector3.zero, WhereTheTargetLooks);
            return director;
        }

        private static StandingAgent Join(EncounterDirector director, Vector3 position)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, AttackTokenType.Melee);
            return agent;
        }

        [Test]
        public void AFirstAsk_IsAnsweredOnlyWhenTheTickEnds()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);

            Assert.IsFalse(director.TryAcquireToken(agent), "the ask itself");
            Assert.IsFalse(director.HasToken(agent), "before the tick ends");

            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(agent), "once the tick ended");
            Assert.IsTrue(director.TryAcquireToken(agent), "asking again while it holds the token");
        }

        [Test]
        public void AnAgentThatNeverAsked_IsGrantedNothing()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);

            director.Tick(NoTime);

            Assert.IsFalse(director.HasToken(agent));
        }

        [Test]
        public void AnAsk_IsForgottenWhenTheTickEnds()
        {
            EncounterDirector director = Director();
            StandingAgent holder = Join(director, North);
            StandingAgent waiting = Join(director, East);
            TakeAToken(director, holder);
            director.TryAcquireToken(waiting);
            director.Tick(NoTime);

            director.ReleaseToken(holder);
            director.Tick(NoTime);

            Assert.IsFalse(director.HasToken(waiting), "it has to ask again for the token that came free");
        }

        [Test]
        public void OfTwoAsking_TheOneInFrontOfTheTargetIsGranted_WhoeverJoinedFirst()
        {
            EncounterDirector director = Director();
            StandingAgent atTheSide = Join(director, East);
            StandingAgent inFront = Join(director, North);

            director.TryAcquireToken(atTheSide);
            director.TryAcquireToken(inFront);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(inFront));
            Assert.IsFalse(director.HasToken(atTheSide));
        }

        [Test]
        public void TheFront_FollowsWhereTheTargetLooks()
        {
            EncounterDirector director = Director();
            StandingAgent north = Join(director, North);
            StandingAgent east = Join(director, East);
            director.SetTarget(Vector3.zero, East);

            director.TryAcquireToken(north);
            director.TryAcquireToken(east);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(east));
        }

        [Test]
        public void OfTwoAskingFromTheSameSide_TheCloserOneIsGranted()
        {
            EncounterDirector director = Director();
            StandingAgent far = Join(director, North * SlotRadius);
            StandingAgent close = Join(director, North);

            director.TryAcquireToken(far);
            director.TryAcquireToken(close);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(close));
        }

        [Test]
        public void AnAgentThatWaitedLong_IsGrantedBeforeABetterPlacedOneThatJustHadItsTurn()
        {
            EncounterDirector director = Director();
            StandingAgent inFront = Join(director, North);
            StandingAgent behind = Join(director, South);
            director.Tick(FairnessHorizonSeconds);
            TakeAToken(director, inFront);
            director.Tick(FairnessHorizonSeconds);
            director.ReleaseToken(inFront);
            director.Tick(ReacquireCooldownSeconds);

            director.TryAcquireToken(inFront);
            director.TryAcquireToken(behind);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(behind));
        }

        private const float BreathSeconds = 1f;
        private const float HalfTheBreath = BreathSeconds / 2f;

        [Test]
        public void AfterATurnEnds_TheNextOneWaits_UntilTheBreathBetweenTurnsIsOver()
        {
            EncounterDirector director = Director(secondsBetweenTurns: BreathSeconds);
            StandingAgent first = Join(director, North);
            StandingAgent next = Join(director, East);
            TakeAToken(director, first);
            director.ReleaseToken(first);

            director.TryAcquireToken(next);
            director.Tick(HalfTheBreath);
            Assert.IsFalse(director.HasToken(next), "half way through the breath");

            director.TryAcquireToken(next);
            director.Tick(BreathSeconds);
            Assert.IsTrue(director.HasToken(next), "once the breath is over");
        }

        [Test]
        public void WithNoBreathBetweenTurns_TheNextTurnStartsAsTheTickEnds()
        {
            EncounterDirector director = Director(secondsBetweenTurns: 0f);
            StandingAgent first = Join(director, North);
            StandingAgent next = Join(director, East);
            TakeAToken(director, first);
            director.ReleaseToken(first);

            director.TryAcquireToken(next);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(next));
        }

        [Test]
        public void ATurnThatEndsWithItsLastAttack_IsFollowedByTheBreathToo()
        {
            EncounterDirector director = Director(secondsBetweenTurns: BreathSeconds);
            StandingAgent first = Join(director, North);
            StandingAgent next = Join(director, East);
            TakeAToken(director, first);
            director.SpendAnAttackOfTheTurn(first);
            Assert.IsFalse(director.HasToken(first), "sanity: a turn is worth one attack here, so it is over");

            director.TryAcquireToken(next);
            director.Tick(HalfTheBreath);

            Assert.IsFalse(director.HasToken(next));
        }

        [Test]
        public void TheBreathAfterAMeleeTurn_DoesNotHoldBackARangedOne()
        {
            EncounterDirector director = Director(secondsBetweenTurns: BreathSeconds);
            StandingAgent brawler = Join(director, North);
            StandingAgent shooter = new StandingAgent(East);
            director.Register(shooter, AttackTokenType.Ranged);
            TakeAToken(director, brawler);
            director.ReleaseToken(brawler);

            director.TryAcquireToken(shooter);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(shooter));
        }

        private const string CombatConfigPath = "Assets/ArkhamCombat/Settings/CombatConfig.asset";
        private const float FightSeconds = 60f;
        private const float FightTickSeconds = 0.1f;
        private const float SecondsToWalkInAndSwing = 2f;

        // The arena as it was when one thug never got a turn: two thugs ahead of the camera and one behind it.
        [Test]
        public void InAFightWithTheShippedTuning_EveryAgentGetsATurn_AndTheOnesInFrontGetMore()
        {
            EncounterDirectorSettings shippedTuning = AssetDatabase.LoadAssetAtPath<CombatConfig>(CombatConfigPath).Encounter;
            EncounterDirector director = new EncounterDirector(shippedTuning);
            director.SetTarget(Vector3.zero, WhereTheTargetLooks);
            StandingAgent aheadOnTheLeft = Join(director, OnTheRing(shippedTuning, degreesOffTheFront: -45f));
            StandingAgent aheadOnTheRight = Join(director, OnTheRing(shippedTuning, degreesOffTheFront: 45f));
            StandingAgent behind = Join(director, OnTheRing(shippedTuning, degreesOffTheFront: 180f));

            Dictionary<IEncounterAgent, int> turns = FightAndCountTheTurns(director, aheadOnTheLeft, aheadOnTheRight, behind);

            Assert.Greater(turns[behind], 0, "the agent behind the target is never left out");
            Assert.Greater(turns[aheadOnTheLeft], turns[behind]);
            Assert.Greater(turns[aheadOnTheRight], turns[behind]);
        }

        private static Vector3 OnTheRing(EncounterDirectorSettings settings, float degreesOffTheFront) =>
            Quaternion.Euler(0f, degreesOffTheFront, 0f) * WhereTheTargetLooks * settings.SlotRadius;

        // Every agent asks on every tick, as a tree does, and whoever has the turn keeps it for the time an attack takes.
        private static Dictionary<IEncounterAgent, int> FightAndCountTheTurns(EncounterDirector director, params StandingAgent[] agents)
        {
            Dictionary<IEncounterAgent, int> turns = new Dictionary<IEncounterAgent, int>();
            Dictionary<IEncounterAgent, float> secondsIntoItsTurn = new Dictionary<IEncounterAgent, float>();
            foreach (StandingAgent agent in agents)
            {
                turns[agent] = 0;
                secondsIntoItsTurn[agent] = 0f;
            }

            director.TokenGranted += agent => turns[agent]++;

            for (float elapsed = 0f; elapsed < FightSeconds; elapsed += FightTickSeconds)
            {
                foreach (StandingAgent agent in agents)
                {
                    bool isItsTurn = director.TryAcquireToken(agent);
                    if (!isItsTurn)
                    {
                        continue;
                    }

                    secondsIntoItsTurn[agent] += FightTickSeconds;
                    if (secondsIntoItsTurn[agent] >= SecondsToWalkInAndSwing)
                    {
                        director.ReleaseToken(agent);
                        secondsIntoItsTurn[agent] = 0f;
                    }
                }

                director.Tick(FightTickSeconds);
            }

            return turns;
        }

        [Test]
        public void AnAgentStillCoolingDown_IsPassedOverForOneWorsePlaced()
        {
            EncounterDirector director = Director();
            StandingAgent inFront = Join(director, North);
            StandingAgent behind = Join(director, South);
            TakeAToken(director, inFront);
            director.ReleaseToken(inFront);

            director.TryAcquireToken(inFront);
            director.TryAcquireToken(behind);
            director.Tick(ReacquireCooldownSeconds / 2f);

            Assert.IsTrue(director.HasToken(behind));
        }

        [Test]
        public void WithTwoFreeTokens_TheTwoBestPlacedAreGranted()
        {
            EncounterDirector director = Director(maxMeleeTokens: 2);
            StandingAgent behind = Join(director, South);
            StandingAgent atTheSide = Join(director, East);
            StandingAgent inFront = Join(director, North);

            director.TryAcquireToken(behind);
            director.TryAcquireToken(atTheSide);
            director.TryAcquireToken(inFront);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(inFront));
            Assert.IsTrue(director.HasToken(atTheSide));
            Assert.IsFalse(director.HasToken(behind));
        }

        [Test]
        public void AGrantTheAgentNeverComesBackFor_IsTakenBackAfterTheGraceTime()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            director.Tick(NoTime);

            director.Tick(GrantGraceSeconds / 2f);
            Assert.IsTrue(director.HasToken(agent), "halfway through the grace time");

            director.Tick(GrantGraceSeconds / 2f);
            Assert.IsFalse(director.HasToken(agent), "once the grace time has passed");
        }

        [Test]
        public void ALaterGrant_IsTakenBackToo_WhenTheAgentDoesNotComeBackForIt()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            TakeAToken(director, agent);
            director.ReleaseToken(agent);
            director.Tick(ReacquireCooldownSeconds);
            director.TryAcquireToken(agent);
            director.Tick(NoTime);
            Assert.IsTrue(director.HasToken(agent), "sanity: it was granted a second turn");

            director.Tick(GrantGraceSeconds);

            Assert.IsFalse(director.HasToken(agent));
        }

        [Test]
        public void AGrantTakenBackForNotComingBack_CountsAsTheAgentsTurn()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            director.Tick(NoTime);
            director.Tick(GrantGraceSeconds);

            director.TryAcquireToken(agent);
            director.Tick(NoTime);

            Assert.IsFalse(director.HasToken(agent), "it cools down like any agent that gave its token back");
        }

        [Test]
        public void AGrantTheAgentCameBackFor_IsKeptPastTheGraceTime()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            TakeAToken(director, agent);

            director.Tick(GrantGraceSeconds * 4f);

            Assert.IsTrue(director.HasToken(agent));
        }

        [Test]
        public void WithAGraceTimeOfZero_AGrantIsNeverTakenBackForNotComingBack()
        {
            EncounterDirector director = Director(grantGraceSeconds: 0f);
            StandingAgent agent = Join(director, North);
            director.TryAcquireToken(agent);
            director.Tick(NoTime);

            director.Tick(ALongTime);

            Assert.IsTrue(director.HasToken(agent));
        }

        [Test]
        public void AGrant_IsAnnouncedWhenTheTickEnds_NotWhenItIsAskedFor()
        {
            EncounterDirector director = Director();
            StandingAgent agent = Join(director, North);
            int grantsAnnounced = 0;
            director.TokenGranted += _ => grantsAnnounced++;

            director.TryAcquireToken(agent);
            Assert.AreEqual(0, grantsAnnounced, "after the ask");

            director.Tick(NoTime);
            Assert.AreEqual(1, grantsAnnounced, "after the tick");
        }
    }
}
