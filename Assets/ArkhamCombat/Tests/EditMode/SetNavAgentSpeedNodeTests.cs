using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class SetNavAgentSpeedNodeTests
    {
        private const float ProwlSpeed = 1.4f;
        private const float Tolerance = 1e-5f;

        private GameObject agent;

        [SetUp]
        public void SetUp()
        {
            agent = HiddenObject("Agent");
            agent.AddComponent<BehaviorTreeMachine>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(agent);

        private BehaviorTreeGraph TreeThatSetsTheSpeedTo(float speed)
        {
            BehaviorTreeGraph graph = TreeOfOne(agent, out SetNavAgentSpeedNode setNavAgentSpeed);
            setNavAgentSpeed.Speed.SetDefaultValue(speed);
            return graph;
        }

        [Test]
        public void TheAgentWalksAtTheSpeedItWasFed()
        {
            NavMeshAgent navMeshAgent = agent.AddComponent<NavMeshAgent>();
            BehaviorTreeGraph graph = TreeThatSetsTheSpeedTo(ProwlSpeed);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
            Assert.AreEqual(ProwlSpeed, navMeshAgent.speed, Tolerance);
        }

        [Test]
        public void ItFails_OnAnAgentThatCannotWalk()
        {
            BehaviorTreeGraph graph = TreeThatSetsTheSpeedTo(ProwlSpeed);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }
    }
}
