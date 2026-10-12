using System;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class EncounterSlotValidationTests
    {
        private const int SlotCount = 4;
        private const float SlotRadius = 4f;
        private const float Tolerance = 0.001f;

        private static readonly Vector3 North = new Vector3(0f, 0f, SlotRadius);
        private static readonly Vector3 East = new Vector3(SlotRadius, 0f, 0f);
        private static readonly Vector3 South = new Vector3(0f, 0f, -SlotRadius);

        private sealed class StandingAgent : IEncounterAgent
        {
            public Vector3 Position { get; }

            public StandingAgent(Vector3 position) => Position = position;
        }

        private sealed class SlotValidatorThat : ISlotValidator
        {
            private readonly Func<Vector3, bool> accepts;
            private readonly Vector3 correction;

            public SlotValidatorThat(Func<Vector3, bool> accepts, Vector3 correction = default)
            {
                this.accepts = accepts;
                this.correction = correction;
            }

            public bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition)
            {
                validPosition = desiredPosition + correction;
                return accepts(desiredPosition);
            }
        }

        private static EncounterDirector DirectorWith(ISlotValidator slotValidator) =>
            new EncounterDirector(new EncounterDirectorSettings(slotCount: SlotCount, slotRadius: SlotRadius), slotValidator);

        private static bool IsOnTheEasternHalf(Vector3 position) => position.x > Tolerance;

        [Test]
        public void ARejectedSlot_IsGivenToNobody()
        {
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => !IsOnTheEasternHalf(position)));
            StandingAgent agentInTheEast = new StandingAgent(East * 1.5f);
            director.Register(agentInTheEast, AttackTokenType.Melee);

            director.Tick(0.1f);

            Vector3 slotPosition = director.GetSlotPosition(agentInTheEast);
            Assert.IsFalse(IsOnTheEasternHalf(slotPosition), $"the agent was sent to {slotPosition}, on the half the validator rejects");
        }

        [Test]
        public void WhenEverySlotLeftIsRejected_TheAgentHoldsItsBearingAtTheRingsRadius()
        {
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => false));
            StandingAgent agent = new StandingAgent(East * 2f);
            director.Register(agent, AttackTokenType.Melee);

            director.Tick(0.1f);

            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(agent));
            Assert.Less((director.GetSlotPosition(agent) - East).magnitude, Tolerance);
        }

        [Test]
        public void WithHalfTheRingRejected_OnlyTheUsableHalfIsFilled_AndTheRestHoldTheirBearing()
        {
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => position.z > -Tolerance && position.x < Tolerance));
            StandingAgent nearestInTheNorth = new StandingAgent(North * 1.1f);
            StandingAgent nextInTheWest = new StandingAgent(-East * 1.2f);
            StandingAgent fartherInTheEast = new StandingAgent(East * 1.5f);
            StandingAgent farthestInTheSouth = new StandingAgent(South * 1.6f);
            StandingAgent[] agents = { farthestInTheSouth, fartherInTheEast, nextInTheWest, nearestInTheNorth };
            foreach (StandingAgent agent in agents)
            {
                director.Register(agent, AttackTokenType.Melee);
            }

            director.Tick(0.1f);

            Assert.AreNotEqual(EncounterDirector.UnassignedSlot, director.GetSlot(nearestInTheNorth), "north is usable");
            Assert.AreNotEqual(EncounterDirector.UnassignedSlot, director.GetSlot(nextInTheWest), "west is usable");
            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(fartherInTheEast), "east is rejected and the usable slots are taken");
            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(farthestInTheSouth), "south is rejected and the usable slots are taken");
            Assert.Less((director.GetSlotPosition(farthestInTheSouth) - South).magnitude, Tolerance, "it holds its bearing, due south");
        }

        [Test]
        public void AValidatedSlot_StandsWhereTheValidatorPutIt()
        {
            Vector3 upOntoALedge = new Vector3(0f, 0.5f, 0f);
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => true, upOntoALedge));
            StandingAgent agent = new StandingAgent(North * 1.5f);
            director.Register(agent, AttackTokenType.Melee);

            director.Tick(0.1f);

            Assert.Less((director.GetSlotPosition(agent) - (North + upOntoALedge)).magnitude, Tolerance);
        }

        [Test]
        public void AValidatedSlot_StillFollowsTheTarget()
        {
            Vector3 upOntoALedge = new Vector3(0f, 0.5f, 0f);
            Vector3 whereTheTargetWent = new Vector3(10f, 0f, 0f);
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => true, upOntoALedge));
            StandingAgent agent = new StandingAgent(North * 1.5f);
            director.Register(agent, AttackTokenType.Melee);
            director.Tick(0.1f);

            director.SetTargetPosition(whereTheTargetWent);

            Assert.Less((director.GetSlotPosition(agent) - (whereTheTargetWent + North + upOntoALedge)).magnitude, Tolerance);
        }

        [Test]
        public void ASlotRejectedOnce_IsUsableAgain_WhenALaterAssignmentAcceptsIt()
        {
            bool isTheGroundThere = false;
            EncounterDirector director = DirectorWith(new SlotValidatorThat(position => isTheGroundThere));
            StandingAgent agent = new StandingAgent(North * 1.5f);
            director.Register(agent, AttackTokenType.Melee);
            director.Tick(0.1f);
            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(agent), "sanity: nothing is usable yet");

            isTheGroundThere = true;
            director.Tick(director.Settings.SlotReassignIntervalSeconds);

            Assert.AreNotEqual(EncounterDirector.UnassignedSlot, director.GetSlot(agent));
        }
    }
}
