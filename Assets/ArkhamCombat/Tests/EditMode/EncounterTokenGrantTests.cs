using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The target stands at the origin and looks north, so north of it is in front.
    public class EncounterTokenGrantTests
    {
        private const float GrantGraceSeconds = 0.5f;
        private const float ReacquireCooldownSeconds = 1.5f;
        private const float FairnessHorizonSeconds = 6f;
        private const float SlotRadius = 4f;
        private const float NoTime = 0f;

        private static readonly Vector3 North = Vector3.forward;
        private static readonly Vector3 East = Vector3.right;
        private static readonly Vector3 South = Vector3.back;

        private static EncounterDirector Director(
            int maxMeleeTokens = 1,
            float fairnessWeight = 0.6f,
            float grantGraceSeconds = GrantGraceSeconds)
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(
                maxMeleeTokens: maxMeleeTokens,
                maxTokenHoldSeconds: 0f,
                reacquireCooldownSeconds: ReacquireCooldownSeconds,
                slotRadius: SlotRadius,
                fairnessWeight: fairnessWeight,
                fairnessHorizonSeconds: FairnessHorizonSeconds,
                grantGraceSeconds: grantGraceSeconds));
            director.SetTarget(Vector3.zero, North);
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
            EncounterDirector director = Director(fairnessWeight: 2f);
            StandingAgent inFront = Join(director, North);
            StandingAgent behind = Join(director, South);
            TakeAToken(director, inFront);
            director.Tick(FairnessHorizonSeconds);
            director.ReleaseToken(inFront);
            director.Tick(ReacquireCooldownSeconds);

            director.TryAcquireToken(inFront);
            director.TryAcquireToken(behind);
            director.Tick(NoTime);

            Assert.IsTrue(director.HasToken(behind));
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

            director.Tick(60f);

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
