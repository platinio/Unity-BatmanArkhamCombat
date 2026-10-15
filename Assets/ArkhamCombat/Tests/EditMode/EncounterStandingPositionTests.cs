using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterStandingPositionTests
    {
        private const float RingRadius = 4f;
        private const float AnyTick = 0.1f;
        private const float SecondsBeforeSteppingBackOut = 2f;

        private static readonly Vector3 FarOutsideTheRing = new Vector3(0f, 0f, 9f);
        private static readonly Vector3 AtStrikeDistance = new Vector3(0f, 0f, 1.3f);

        private static EncounterDirector DirectorWithARingOf(float radius)
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(slotRadius: radius));
            director.SetTarget(Vector3.zero, Vector3.forward);
            return director;
        }

        private static EncounterDirector DirectorWhoseWaitingAgentsStepBackOut()
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(
                maxTokenHoldSeconds: 0f, slotRadius: RingRadius, secondsBeforeAWaitingAgentStepsBackOut: SecondsBeforeSteppingBackOut));
            director.SetTarget(Vector3.zero, Vector3.forward);
            return director;
        }

        private static void TickFor(EncounterDirector director, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += AnyTick)
            {
                director.Tick(AnyTick);
            }
        }

        private static StandingAgent JoinAt(EncounterDirector director, Vector3 position)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, AttackTokenType.Melee);
            director.Tick(AnyTick);
            return agent;
        }

        [Test]
        public void AnAgentOutsideTheRing_StandsOnItsSlot()
        {
            EncounterDirector director = DirectorWithARingOf(RingRadius);
            StandingAgent agent = JoinAt(director, FarOutsideTheRing);

            Assert.AreEqual(director.GetSlotPosition(agent), director.GetStandingPosition(agent));
            Assert.AreNotEqual(agent.Position, director.GetStandingPosition(agent), "sanity: its slot is not where it already is");
        }

        [Test]
        public void AnAgentNearerTheTargetThanTheRing_StandsWhereItIs_AndDoesNotWalkBackOut()
        {
            EncounterDirector director = DirectorWithARingOf(RingRadius);
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent));
        }

        [Test]
        public void WhenStepsBackOutAreOff_AnAgentInsideTheRing_StaysForAsLongAsItWaits()
        {
            EncounterDirector director = DirectorWithARingOf(RingRadius);
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            TickFor(director, 60f);

            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent));
        }

        [Test]
        public void AWaitingAgentInsideTheRing_StandsWhereItIs_UntilItHasWaitedThereLongEnough()
        {
            EncounterDirector director = DirectorWhoseWaitingAgentsStepBackOut();
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            TickFor(director, SecondsBeforeSteppingBackOut * 0.5f);

            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent));
        }

        [Test]
        public void AWaitingAgentThatStayedInsideTheRingLongEnough_StepsBackOutToItsSlot()
        {
            EncounterDirector director = DirectorWhoseWaitingAgentsStepBackOut();
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            TickFor(director, SecondsBeforeSteppingBackOut + AnyTick);

            Assert.AreEqual(director.GetSlotPosition(agent), director.GetStandingPosition(agent));
        }

        [Test]
        public void TimeOnItsOwnTurn_DoesNotCountTowardSteppingBackOut()
        {
            EncounterDirector director = DirectorWhoseWaitingAgentsStepBackOut();
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            for (float elapsed = 0f; elapsed < SecondsBeforeSteppingBackOut * 1.5f; elapsed += AnyTick)
            {
                director.TryAcquireToken(agent);
                director.Tick(AnyTick);
            }

            Assert.IsTrue(director.HasToken(agent), "sanity: the only agent that asks has the turn");
            director.ReleaseToken(agent);
            director.Tick(AnyTick);
            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent), "the wait starts when the turn ends");
        }

        [Test]
        public void AnAgentThatLeftTheRing_StartsItsWaitInsideAgainWhenItComesBack()
        {
            EncounterDirector director = DirectorWhoseWaitingAgentsStepBackOut();
            StandingAgent agent = JoinAt(director, AtStrikeDistance);
            TickFor(director, SecondsBeforeSteppingBackOut * 0.75f);

            agent.Position = FarOutsideTheRing;
            director.Tick(AnyTick);
            agent.Position = AtStrikeDistance;
            TickFor(director, SecondsBeforeSteppingBackOut * 0.75f);

            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent));
        }

        [Test]
        public void HowNearTheTargetAnAgentIs_IsMeasuredOnTheGround()
        {
            EncounterDirector director = DirectorWithARingOf(RingRadius);
            Vector3 nearOnTheGroundButHighAbove = AtStrikeDistance + Vector3.up * (2f * RingRadius);
            StandingAgent agent = JoinAt(director, nearOnTheGroundButHighAbove);

            Assert.AreEqual(agent.Position, director.GetStandingPosition(agent));
        }

        [Test]
        public void WithoutARing_NoAgentIsInsideIt()
        {
            EncounterDirector director = DirectorWithARingOf(radius: 0f);
            StandingAgent agent = JoinAt(director, AtStrikeDistance);

            Assert.AreEqual(director.GetSlotPosition(agent), director.GetStandingPosition(agent));
        }

        [Test]
        public void AnAgentThatIsNotInTheEncounter_IsGivenNoGroundToHold()
        {
            EncounterDirector director = DirectorWithARingOf(RingRadius);
            StandingAgent stranger = new StandingAgent(AtStrikeDistance);

            Assert.AreEqual(director.GetSlotPosition(stranger), director.GetStandingPosition(stranger));
        }
    }
}
