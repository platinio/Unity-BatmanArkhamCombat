using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The machine is never woken: it only gives the nodes their agent, and each test ticks the graph by hand.
    public class PlayActionNodeTests
    {
        private const float ActionSeconds = 1f;
        private const float TickSeconds = 0.1f;

        private ActionDefinition flinch;
        private GameObject agent;
        private CombatActions combatActions;
        private BehaviorTreeGraph graph;
        private PlayActionNode playAction;

        [SetUp]
        public void SetUp()
        {
            flinch = Action("Flinch", ActionSeconds);
            agent = AgentWithARunner("Agent", out combatActions);
            graph = TreeOfOnePlayAction(agent, out playAction);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(flinch);
        }

        private static GameObject AgentWithARunner(string name, out CombatActions combatActions)
        {
            GameObject agent = HiddenObject(name);
            agent.AddComponent<BehaviorTreeMachine>();
            MotorWarpMover warpMover = new MotorWarpMover();
            ActionRunner runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), warpMover, new NullHitWindowListener());
            combatActions = agent.AddComponent<CombatActions>();
            combatActions.Construct(runner, warpMover, new AlwaysOpenActionStartGate(), null);
            return agent;
        }

        private static BehaviorTreeGraph TreeOfOnePlayAction(GameObject agent, out PlayActionNode playAction)
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            playAction = BehaviorTreeAuthoring.AddNode<PlayActionNode>(graph, 0f, 100f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, playAction);
            BehaviorTreeMachine machine = agent.GetComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            foreach (BehaviorTreeNode node in graph.Nodes)
            {
                node.SetMachine(machine);
            }

            graph.OnAwake();
            graph.OnEnter();
            return graph;
        }

        private void FeedTheAction(ActionDefinition action) => playAction.Action.SetDefaultValue(action);

        private void TickTheRunnerFor(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / TickSeconds);
            for (int i = 0; i < ticks; i++)
            {
                combatActions.Tick(TickSeconds);
            }
        }

        [Test]
        public void EnteringTheNode_StartsTheActionOnTheAgentsRunner()
        {
            FeedTheAction(flinch);

            ExecutionStatus status = graph.OnUpdate();

            Assert.AreEqual(ExecutionStatus.Running, status);
            Assert.AreSame(flinch, combatActions.Runner.CurrentAction);
        }

        [Test]
        public void StaysRunning_UntilTheActionEnds_ThenSucceeds()
        {
            FeedTheAction(flinch);
            graph.OnUpdate();

            TickTheRunnerFor(ActionSeconds / 2f);
            Assert.AreEqual(ExecutionStatus.Running, graph.OnUpdate(), "halfway through the action");

            TickTheRunnerFor(ActionSeconds);
            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate(), "the action has ended");
            Assert.IsFalse(combatActions.Runner.IsPlaying);
        }

        [Test]
        public void LeavingTheNodeEarly_CancelsTheAction()
        {
            FeedTheAction(flinch);
            graph.OnUpdate();

            graph.OnExit();

            Assert.IsFalse(combatActions.Runner.IsPlaying, "an aborted branch leaves no action behind");
        }

        [Test]
        public void HandedNoAction_ItFails_AndSaysSo()
        {
            FeedTheAction(null);
            LogAssert.Expect(LogType.Error, new Regex("handed no action"));

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
            Assert.IsFalse(combatActions.Runner.IsPlaying);
        }

        [Test]
        public void OnAnAgentWithoutCombatActions_ItFails_AndSaysSo()
        {
            GameObject bareAgent = HiddenObject("Bare agent");
            bareAgent.AddComponent<BehaviorTreeMachine>();
            BehaviorTreeGraph bareGraph = TreeOfOnePlayAction(bareAgent, out PlayActionNode bareNode);
            bareNode.Action.SetDefaultValue(flinch);
            LogAssert.Expect(LogType.Error, new Regex("has no CombatActions"));

            try
            {
                Assert.AreEqual(ExecutionStatus.Failure, bareGraph.OnUpdate());
            }
            finally
            {
                Object.DestroyImmediate(bareAgent);
            }
        }
    }
}
