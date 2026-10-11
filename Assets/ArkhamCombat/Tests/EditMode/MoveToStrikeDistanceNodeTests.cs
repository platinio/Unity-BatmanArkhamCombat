using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // No NavMesh exists here, so no test reaches the node walking: Running, the countdown and the stop on exit are checked in play mode.
    public class MoveToStrikeDistanceNodeTests
    {
        private const float StrikeDistance = 1.5f;
        private const float JustInside = StrikeDistance - 0.1f;
        private const float WellOutside = StrikeDistance * 10f;
        private const float PlentyOfTime = 30f;
        private const float NoTimeLeft = 0f;

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
            graph = TreeOfOne(agent, out moveToStrikeDistance);

            Feed(moveToStrikeDistance, target.transform, PlentyOfTime);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }

        private static void Feed(MoveToStrikeDistanceNode node, Transform targetToFeed, float timeout)
        {
            node.Target.SetDefaultValue(targetToFeed);
            node.StrikeDistance.SetDefaultValue(StrikeDistance);
            node.Timeout.SetDefaultValue(timeout);
        }

        private void StandThisFarFromTheTarget(float metres) => agent.transform.position = target.transform.position + Vector3.right * metres;

        [Test]
        public void AlreadyWithinTheStrikeDistance_ItSucceeds()
        {
            StandThisFarFromTheTarget(JustInside);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void TheDistance_IsMeasuredOnTheGround()
        {
            StandThisFarFromTheTarget(JustInside);
            agent.transform.position += Vector3.up * WellOutside;

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate(), "the agent's origin sits higher than its target's");
        }

        [Test]
        public void OutOfTime_ItFails()
        {
            StandThisFarFromTheTarget(WellOutside);
            moveToStrikeDistance.Timeout.SetDefaultValue(NoTimeLeft);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void OutOfTime_ButAlreadyWithinTheStrikeDistance_ItStillSucceeds()
        {
            StandThisFarFromTheTarget(JustInside);
            moveToStrikeDistance.Timeout.SetDefaultValue(NoTimeLeft);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void WithNoTarget_ItFails_AndSaysSo()
        {
            moveToStrikeDistance.Target.SetDefaultValue(null);
            LogAssert.Expect(LogType.Error, new Regex("has no target"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void StandingOnNoNavMesh_ItFails_AndSaysSo()
        {
            StandThisFarFromTheTarget(WellOutside);
            LogAssert.Expect(LogType.Error, new Regex("is not standing on a NavMesh"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void OnAnAgentWithoutANavMeshAgent_ItFails_AndSaysSo()
        {
            GameObject bareAgent = HiddenObject("Bare agent");
            bareAgent.AddComponent<BehaviorTreeMachine>();
            bareAgent.transform.position = target.transform.position + Vector3.right * WellOutside;
            BehaviorTreeGraph bareGraph = TreeOfOne(bareAgent, out MoveToStrikeDistanceNode bareNode);
            Feed(bareNode, target.transform, PlentyOfTime);
            LogAssert.Expect(LogType.Error, new Regex("has no NavMeshAgent"));

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
        public void FailingAgainForTheSameReason_ItSaysWhyOnlyOnce()
        {
            StandThisFarFromTheTarget(WellOutside);
            LogAssert.Expect(LogType.Error, new Regex("is not standing on a NavMesh"));

            graph.OnUpdate();
            graph.OnUpdate();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FailingForADifferentReason_ItSaysTheNewOne()
        {
            moveToStrikeDistance.Target.SetDefaultValue(null);
            LogAssert.Expect(LogType.Error, new Regex("has no target"));
            graph.OnUpdate();

            moveToStrikeDistance.Target.SetDefaultValue(target.transform);
            StandThisFarFromTheTarget(WellOutside);
            LogAssert.Expect(LogType.Error, new Regex("is not standing on a NavMesh"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }
    }
}
