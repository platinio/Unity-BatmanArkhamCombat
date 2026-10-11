using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The machine is never woken: it only gives the node its agent, and each test ticks the graph by hand.
    // No NavMesh exists here, so what is covered is every way the node ends without walking.
    public class MoveToStrikeDistanceNodeTests
    {
        private const float StrikeDistance = 1.5f;
        private const float PlentyOfTime = 30f;

        private static readonly Vector3 FarFromAnyNavMesh = new Vector3(5000f, 0f, 5000f);

        private GameObject agent;
        private GameObject target;
        private BehaviorTreeGraph graph;
        private MoveToStrikeDistanceNode moveToStrikeDistance;

        [SetUp]
        public void SetUp()
        {
            target = HiddenObject("Target");
            target.transform.position = FarFromAnyNavMesh;

            agent = HiddenObject("Agent");
            agent.AddComponent<BehaviorTreeMachine>();
            agent.AddComponent<NavMeshAgent>();
            graph = TreeOfOneMoveToStrikeDistance(agent, out moveToStrikeDistance);

            moveToStrikeDistance.Target.SetDefaultValue(target.transform);
            moveToStrikeDistance.StrikeDistance.SetDefaultValue(StrikeDistance);
            moveToStrikeDistance.Timeout.SetDefaultValue(PlentyOfTime);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }

        private static BehaviorTreeGraph TreeOfOneMoveToStrikeDistance(GameObject agentObject, out MoveToStrikeDistanceNode node)
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            node = BehaviorTreeAuthoring.AddNode<MoveToStrikeDistanceNode>(graph, 0f, 100f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, node);
            BehaviorTreeMachine machine = agentObject.GetComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            foreach (BehaviorTreeNode graphNode in graph.Nodes)
            {
                graphNode.SetMachine(machine);
            }

            graph.OnAwake();
            graph.OnEnter();
            return graph;
        }

        private void StandThisFarFromTheTarget(float metres) => agent.transform.position = target.transform.position + Vector3.right * metres;

        [Test]
        public void AlreadyWithinTheStrikeDistance_ItSucceeds()
        {
            StandThisFarFromTheTarget(StrikeDistance - 0.1f);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void TheDistance_IsMeasuredOnTheGround()
        {
            StandThisFarFromTheTarget(StrikeDistance - 0.1f);
            agent.transform.position += Vector3.up * (StrikeDistance * 4f);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate(), "the agent's origin sits higher than its target's");
        }

        [Test]
        public void OutOfTime_ItFails()
        {
            StandThisFarFromTheTarget(StrikeDistance * 10f);
            moveToStrikeDistance.Timeout.SetDefaultValue(0f);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void OutOfTime_ButAlreadyWithinTheStrikeDistance_ItStillSucceeds()
        {
            StandThisFarFromTheTarget(StrikeDistance - 0.1f);
            moveToStrikeDistance.Timeout.SetDefaultValue(0f);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void HandedNoTarget_ItFails_AndSaysSo()
        {
            moveToStrikeDistance.Target.SetDefaultValue(null);
            LogAssert.Expect(LogType.Error, new Regex("handed no target"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void StandingOnNoNavMesh_ItFails_AndSaysSo()
        {
            StandThisFarFromTheTarget(StrikeDistance * 10f);
            LogAssert.Expect(LogType.Error, new Regex("cannot walk to its target"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void OnAnAgentWithoutANavMeshAgent_ItFails_AndSaysSo()
        {
            GameObject bareAgent = HiddenObject("Bare agent");
            bareAgent.AddComponent<BehaviorTreeMachine>();
            BehaviorTreeGraph bareGraph = TreeOfOneMoveToStrikeDistance(bareAgent, out MoveToStrikeDistanceNode bareNode);
            bareNode.Target.SetDefaultValue(target.transform);
            bareNode.StrikeDistance.SetDefaultValue(StrikeDistance);
            LogAssert.Expect(LogType.Error, new Regex("cannot walk to its target"));

            try
            {
                Assert.AreEqual(ExecutionStatus.Failure, bareGraph.OnUpdate());
            }
            finally
            {
                Object.DestroyImmediate(bareAgent);
            }
        }

        [Test]
        public void EnteredAgainAfterFailing_ItSaysWhyOnlyOnce()
        {
            StandThisFarFromTheTarget(StrikeDistance * 10f);
            LogAssert.Expect(LogType.Error, new Regex("cannot walk to its target"));

            graph.OnUpdate();
            graph.OnUpdate();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LeavingEarly_OnAnAgentThatCannotWalk_DoesNotThrow()
        {
            StandThisFarFromTheTarget(StrikeDistance - 0.1f);
            graph.OnUpdate();

            Assert.DoesNotThrow(() => graph.OnExit());
        }
    }
}
