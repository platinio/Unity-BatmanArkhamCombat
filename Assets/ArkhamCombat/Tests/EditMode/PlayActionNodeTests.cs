using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
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
    public class PlayActionNodeTests
    {
        private const float ActionSeconds = 1f;
        private const float TickSeconds = 0.1f;

        private ActionDefinition flinch;
        private ActionDefinition stagger;
        private GameObject agent;
        private CombatActions combatActions;
        private BehaviorTreeGraph graph;
        private PlayActionNode playAction;

        [SetUp]
        public void SetUp()
        {
            flinch = Action("Flinch", ActionSeconds);
            stagger = Action("Stagger", ActionSeconds);
            agent = AgentWithARunner("Agent", out combatActions);
            graph = TreeOfOne(agent, out playAction);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(flinch);
            Object.DestroyImmediate(stagger);
        }

        private static GameObject AgentWithARunner(string name, out CombatActions combatActions)
        {
            GameObject agent = HiddenObject(name);
            agent.AddComponent<BehaviorTreeMachine>();
            PendingWarpMovement pendingWarpMovement = new PendingWarpMovement();
            ActionRunner runner = new ActionRunner(
                stance: null, intents: null, new CombatFacts(), new AlwaysConditionEvaluator(), new InterruptKinds(null, null),
                new NullPresentationDriver(), pendingWarpMovement, new NullHitWindowListener());
            combatActions = agent.AddComponent<CombatActions>();
            combatActions.Construct(runner, pendingWarpMovement, new AlwaysOpenActionStartGate(), null);
            return agent;
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
        public void ADashFedADirection_TravelsAlongIt()
        {
            const float travelDistance = 2f;
            DashDefinition dodge = ScriptableObject.CreateInstance<DashDefinition>();
            dodge.Configure(ActionSeconds);
            dodge.ConfigureDash(new Window(0f, 1f), travelDistance);
            FeedTheAction(dodge);
            playAction.Direction.SetDefaultValue(Vector3.left);

            graph.OnUpdate();
            TickTheRunnerFor(ActionSeconds);

            Assert.Less(Vector3.Distance(Vector3.left * travelDistance, combatActions.TakePendingWarpMovement()), 1e-4f);
            Object.DestroyImmediate(dodge);
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
        public void WhenSomethingElseStartsAnotherAction_ItStopsWaiting_AndLeavesThatActionAlone()
        {
            FeedTheAction(flinch);
            graph.OnUpdate();

            combatActions.Runner.PlayAction(stagger, agent.transform.position);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate(), "the flinch is over, whoever ended it");
            graph.OnExit();
            Assert.AreSame(stagger, combatActions.Runner.CurrentAction, "the stagger is not this node to cancel");
        }

        [Test]
        public void EnteredAgainAfterFailing_ItSaysWhyOnlyOnce()
        {
            FeedTheAction(null);
            LogAssert.Expect(LogType.Error, new Regex("handed no action"));

            graph.OnUpdate();
            graph.OnUpdate();

            LogAssert.NoUnexpectedReceived();
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
            BehaviorTreeGraph bareGraph = TreeOfOne(bareAgent, out PlayActionNode bareNode);
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
