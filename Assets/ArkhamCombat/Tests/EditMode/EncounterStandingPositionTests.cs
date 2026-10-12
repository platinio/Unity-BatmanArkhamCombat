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

        private static readonly Vector3 FarOutsideTheRing = new Vector3(0f, 0f, 9f);
        private static readonly Vector3 AtStrikeDistance = new Vector3(0f, 0f, 1.3f);

        private static EncounterDirector DirectorWithARingOf(float radius)
        {
            EncounterDirector director = new EncounterDirector(new EncounterDirectorSettings(slotRadius: radius));
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
