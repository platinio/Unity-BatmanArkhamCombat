using ArkhamCombat.Encounter;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterTelegraphTests
    {
        private const bool Counterable = true;
        private const bool NotCounterable = false;

        private EncounterDirector director;
        private StandingAgent agent;
        private StandingAgent anotherAgent;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings());
            agent = new StandingAgent();
            anotherAgent = new StandingAgent();
            director.Register(agent, AttackTokenType.Melee);
            director.Register(anotherAgent, AttackTokenType.Melee);
        }

        [Test]
        public void NobodyTelegraphs_UntilAnAgentSaysItDoes()
        {
            Assert.IsFalse(director.IsTelegraphing(agent));
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void AnAgentTelegraphingACounterableAttack_MakesACounterableAttackIncoming()
        {
            director.TelegraphAnAttack(agent, Counterable);

            Assert.IsTrue(director.IsTelegraphing(agent));
            Assert.IsFalse(director.IsTelegraphing(anotherAgent));
            Assert.IsTrue(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void AnAttackThatCannotBeCountered_IsTelegraphed_ButNoCounterableAttackIsIncoming()
        {
            director.TelegraphAnAttack(agent, NotCounterable);

            Assert.IsTrue(director.IsTelegraphing(agent));
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void OfTwoTelegraphedAttacks_OneCounterableIsEnough()
        {
            director.TelegraphAnAttack(agent, NotCounterable);
            director.TelegraphAnAttack(anotherAgent, Counterable);

            Assert.IsTrue(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void WhenTheAgentStopsTelegraphing_ItsAttackIsNoLongerIncoming()
        {
            director.TelegraphAnAttack(agent, Counterable);

            director.StopTelegraphing(agent);

            Assert.IsFalse(director.IsTelegraphing(agent));
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void WhenOneOfTwoStopsTelegraphing_TheOthersAttackIsStillIncoming()
        {
            director.TelegraphAnAttack(agent, Counterable);
            director.TelegraphAnAttack(anotherAgent, Counterable);

            director.StopTelegraphing(agent);

            Assert.IsTrue(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void AnAgentThatLeavesTheEncounter_TakesItsTelegraphWithIt()
        {
            director.TelegraphAnAttack(agent, Counterable);

            director.Unregister(agent);

            Assert.IsFalse(director.IsTelegraphing(agent));
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }

        [Test]
        public void AnAgentOutsideTheEncounter_CannotTelegraph()
        {
            StandingAgent stranger = new StandingAgent();

            director.TelegraphAnAttack(stranger, Counterable);

            Assert.IsFalse(director.IsTelegraphing(stranger));
            Assert.IsFalse(director.IsACounterableAttackIncoming);
        }
    }
}
