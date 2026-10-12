using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The machine is never woken: it only gives the nodes their agent, and each test ticks the graph by hand.
    public class EncounterNodeTests
    {
        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject target;
        private GameObject thug;
        private EncounterAgent agent;
        private GameObject otherThug;
        private GameObject loner;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 1));

            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);

            target = HiddenObject("Target");
            encounter.SetTarget(target.transform);

            thug = HiddenObject("Thug");
            agent = AgentInTheEncounter(thug);

            otherThug = HiddenObject("Other thug");

            loner = HiddenObject("Loner");
            loner.AddComponent<BehaviorTreeMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(otherThug);
            Object.DestroyImmediate(loner);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(encounterObject);
        }

        private EncounterAgent AgentInTheEncounter(GameObject agentObject)
        {
            agentObject.AddComponent<BehaviorTreeMachine>();
            EncounterAgent encounterAgent = agentObject.AddComponent<EncounterAgent>();
            encounterAgent.Construct(encounter);
            encounterAgent.OnEnable();
            return encounterAgent;
        }

        private static BehaviorTreeGraph TreeOfOne<TNode>(GameObject agentObject, out TNode node) where TNode : BehaviorTreeNode, new()
        {
            BehaviorTreeGraph graph = new BehaviorTreeGraph();
            node = BehaviorTreeAuthoring.AddNode<TNode>(graph, 0f, 100f);
            if (node.CanBeUsedAsTransitionDestination)
            {
                BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, node);
            }

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

        private static TValue ValueOf<TValue>(ValueOutput output) => (TValue)output.GetPortValue();

        [Test]
        public void RequestAttackToken_Succeeds_WhenTheTurnIsFree()
        {
            BehaviorTreeGraph graph = TreeOfOne(thug, out RequestAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
            Assert.IsTrue(agent.HasToken);
        }

        [Test]
        public void RequestAttackToken_Fails_WhileAnotherAgentHasTheTurn()
        {
            EncounterAgent attacker = AgentInTheEncounter(otherThug);
            attacker.TryAcquireToken();
            BehaviorTreeGraph graph = TreeOfOne(thug, out RequestAttackToken _);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void RequestAttackToken_Fails_OnAnAgentThatIsInNoEncounter()
        {
            BehaviorTreeGraph graph = TreeOfOne(loner, out RequestAttackToken _);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void ReleaseAttackToken_GivesTheTurnBack_AndSucceeds()
        {
            agent.TryAcquireToken();
            BehaviorTreeGraph graph = TreeOfOne(thug, out ReleaseAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void ReleaseAttackToken_Succeeds_OnAnAgentThatIsInNoEncounter()
        {
            BehaviorTreeGraph graph = TreeOfOne(loner, out ReleaseAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void HasAttackToken_FollowsTheAgentsToken()
        {
            TreeOfOne(thug, out HasAttackToken hasAttackToken);
            Assert.IsFalse(ValueOf<bool>(hasAttackToken.Output), "before it has the turn");

            agent.TryAcquireToken();

            Assert.IsTrue(ValueOf<bool>(hasAttackToken.Output), "while it has the turn");
        }

        [Test]
        public void HasAttackToken_IsFalse_OnAnAgentThatIsInNoEncounter()
        {
            TreeOfOne(loner, out HasAttackToken hasAttackToken);

            Assert.IsFalse(ValueOf<bool>(hasAttackToken.Output));
        }

        [Test]
        public void GetSlotPosition_OutputsTheSlotTheDirectorAssigned()
        {
            thug.transform.position = new Vector3(0f, 0f, 9f);
            director.SetTargetPosition(Vector3.zero);
            director.Tick(0.1f);
            TreeOfOne(thug, out GetSlotPosition getSlotPosition);

            Assert.AreEqual(director.GetSlotPosition(agent), ValueOf<Vector3>(getSlotPosition.Output));
        }

        [Test]
        public void GetSlotPosition_OutputsWhereTheAgentStands_WhenItIsInNoEncounter()
        {
            loner.transform.position = new Vector3(3f, 0f, -2f);
            TreeOfOne(loner, out GetSlotPosition getSlotPosition);

            Assert.AreEqual(loner.transform.position, ValueOf<Vector3>(getSlotPosition.Output));
        }

        [Test]
        public void GetEncounterTarget_OutputsWhatTheEncounterRevolvesAround()
        {
            TreeOfOne(thug, out GetEncounterTarget getEncounterTarget);

            Assert.AreSame(target.transform, ValueOf<Transform>(getEncounterTarget.Output));
        }

        [Test]
        public void GetEncounterTarget_OutputsNothing_OnAnAgentThatIsInNoEncounter()
        {
            TreeOfOne(loner, out GetEncounterTarget getEncounterTarget);

            Assert.IsNull(ValueOf<Transform>(getEncounterTarget.Output));
        }
    }
}
