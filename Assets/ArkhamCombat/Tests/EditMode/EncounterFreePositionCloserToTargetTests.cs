using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterFreePositionCloserToTargetTests
    {
        private const float RingRadius = 4f;
        private const float CloseToTheTarget = 1.8f;
        private const float RoomEachAgentKeeps = 1.2f;
        private const float AnyTick = 0.1f;
        private const float Tolerance = 1e-4f;

        private static readonly Vector3 OnTheRing = new Vector3(0f, 0f, RingRadius);
        private static readonly Vector3 StraightInFromTheRing = new Vector3(0f, 0f, CloseToTheTarget);

        private sealed class NoGroundIsWalkable : ISlotValidator
        {
            public bool TryValidate(Vector3 desiredPosition, out Vector3 validPosition)
            {
                validPosition = desiredPosition;
                return false;
            }
        }

        private static EncounterDirector DirectorAroundTheOrigin(ISlotValidator slotValidator = null)
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(slotRadius: RingRadius), slotValidator);
            director.SetTarget(Vector3.zero, Vector3.forward);
            return director;
        }

        private static StandingAgent JoinAt(EncounterDirector director, Vector3 position)
        {
            StandingAgent agent = new StandingAgent(position);
            director.Register(agent, AttackTokenType.Melee);
            director.Tick(AnyTick);
            return agent;
        }

        private static Vector3 FreePositionCloserToTarget(EncounterDirector director, StandingAgent agent) =>
            director.GetFreePositionCloserToTarget(agent, CloseToTheTarget, RoomEachAgentKeeps);

        private static void AssertSamePosition(Vector3 expected, Vector3 actual, string message = null) =>
            Assert.Less(Vector3.Distance(expected, actual), Tolerance, $"{message} expected {expected}, was {actual}");

        [Test]
        public void AnAgentOnTheRing_IsOfferedTheSpotStraightInFromWhereItIs()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent agent = JoinAt(director, OnTheRing);

            AssertSamePosition(StraightInFromTheRing, FreePositionCloserToTarget(director, agent));
        }

        [Test]
        public void WhenAnotherAgentStandsWithinTheRoomEachKeeps_ItIsOfferedWhereItAlreadyStands()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent agent = JoinAt(director, OnTheRing);
            JoinAt(director, StraightInFromTheRing + Vector3.right * (RoomEachAgentKeeps * 0.5f));

            AssertSamePosition(director.GetStandingPosition(agent), FreePositionCloserToTarget(director, agent));
        }

        [Test]
        public void AnotherAgentFartherAwayThanTheRoomEachKeeps_DoesNotTakeTheSpot()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent agent = JoinAt(director, OnTheRing);
            JoinAt(director, StraightInFromTheRing + Vector3.right * (RoomEachAgentKeeps * 2f));

            AssertSamePosition(StraightInFromTheRing, FreePositionCloserToTarget(director, agent));
        }

        [Test]
        public void TheRoomEachAgentKeeps_IsMeasuredOnTheGround()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent agent = JoinAt(director, OnTheRing);
            JoinAt(director, StraightInFromTheRing + Vector3.up * (RoomEachAgentKeeps * 2f));

            AssertSamePosition(director.GetStandingPosition(agent), FreePositionCloserToTarget(director, agent), "an agent right above the spot stands on it.");
        }

        [Test]
        public void AnAgentAlreadyThatClose_IsOfferedWhereItAlreadyStands()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent agent = JoinAt(director, StraightInFromTheRing * 0.5f);

            AssertSamePosition(agent.Position, FreePositionCloserToTarget(director, agent));
        }

        [Test]
        public void ASpotOffTheWalkableGround_IsNotOffered()
        {
            EncounterDirector director = DirectorAroundTheOrigin(new NoGroundIsWalkable());
            StandingAgent agent = JoinAt(director, OnTheRing);

            AssertSamePosition(director.GetStandingPosition(agent), FreePositionCloserToTarget(director, agent));
        }

        [Test]
        public void AnAgentThatIsNotInTheEncounter_IsOfferedNoSpotCloserIn()
        {
            EncounterDirector director = DirectorAroundTheOrigin();
            StandingAgent stranger = new StandingAgent(OnTheRing);

            AssertSamePosition(director.GetStandingPosition(stranger), FreePositionCloserToTarget(director, stranger));
        }
    }
}
