using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EncounterNodeTests
    {
        private const float NoTime = 0f;

        private const float AnyTick = 0.1f;

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

        private static TValue ValueOf<TValue>(ValueOutput output) => (TValue)output.GetPortValue();

        [Test]
        public void TryAcquireAttackToken_FailsOnTheFirstAsk_EvenWhenTheTurnIsFree()
        {
            BehaviorTreeGraph graph = TreeOfOne(thug, out TryAcquireAttackToken _);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void TryAcquireAttackToken_Succeeds_OnceTheDirectorGrantedTheTurnItAskedFor()
        {
            TreeOfOne(thug, out TryAcquireAttackToken _).OnUpdate();
            director.Tick(NoTime);
            BehaviorTreeGraph graphAskingAgain = TreeOfOne(thug, out TryAcquireAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graphAskingAgain.OnUpdate());
            Assert.IsTrue(agent.HasToken);
        }

        [Test]
        public void TryAcquireAttackToken_Fails_WhileAnotherAgentHasTheTurn()
        {
            EncounterAgent attacker = AgentInTheEncounter(otherThug);
            Assert.IsTrue(TakeAToken(director, attacker), "sanity: the other agent has the turn");
            TreeOfOne(thug, out TryAcquireAttackToken _).OnUpdate();
            director.Tick(NoTime);
            BehaviorTreeGraph graphAskingAgain = TreeOfOne(thug, out TryAcquireAttackToken _);

            Assert.AreEqual(ExecutionStatus.Failure, graphAskingAgain.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void TryAcquireAttackToken_Fails_OnAnObjectWithoutAnEncounterAgent()
        {
            BehaviorTreeGraph graph = TreeOfOne(loner, out TryAcquireAttackToken _);

            Assert.AreEqual(ExecutionStatus.Failure, graph.OnUpdate());
        }

        [Test]
        public void ReleaseAttackToken_GivesTheTurnBack()
        {
            Assert.IsTrue(TakeAToken(director, agent), "sanity: it holds the token before the node runs");
            BehaviorTreeGraph graph = TreeOfOne(thug, out ReleaseAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void SpendAttackOfTheTurn_EndsATurnWorthOneAttack()
        {
            Assert.IsTrue(TakeAToken(director, agent), "sanity: it holds the token before the node runs");
            BehaviorTreeGraph graph = TreeOfOne(thug, out SpendAttackOfTheTurn _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
            Assert.IsFalse(agent.HasToken);
        }

        [Test]
        public void SpendAttackOfTheTurn_Succeeds_OnAnObjectWithoutAnEncounterAgent()
        {
            BehaviorTreeGraph graph = TreeOfOne(loner, out SpendAttackOfTheTurn _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void ReleaseAttackToken_Succeeds_OnAnObjectWithoutAnEncounterAgent()
        {
            BehaviorTreeGraph graph = TreeOfOne(loner, out ReleaseAttackToken _);

            Assert.AreEqual(ExecutionStatus.Success, graph.OnUpdate());
        }

        [Test]
        public void HasAttackToken_FollowsTheAgentsToken()
        {
            TreeOfOne(thug, out HasAttackToken hasAttackToken);
            Assert.IsFalse(ValueOf<bool>(hasAttackToken.Output), "before it has the turn");

            TakeAToken(director, agent);

            Assert.IsTrue(ValueOf<bool>(hasAttackToken.Output), "while it has the turn");
        }

        [Test]
        public void HasAttackToken_IsFalse_OnAnObjectWithoutAnEncounterAgent()
        {
            TreeOfOne(loner, out HasAttackToken hasAttackToken);

            Assert.IsFalse(ValueOf<bool>(hasAttackToken.Output));
        }

        [Test]
        public void GetStandingPosition_OutputsTheSlotTheDirectorAssigned_ToAnAgentOutsideTheRing()
        {
            thug.transform.position = new Vector3(0f, 0f, 9f);
            director.SetTarget(Vector3.zero, Vector3.forward);
            director.Tick(AnyTick);
            TreeOfOne(thug, out GetStandingPosition getStandingPosition);

            Assert.AreEqual(director.GetSlotPosition(agent), ValueOf<Vector3>(getStandingPosition.Output));
        }

        [Test]
        public void GetStandingPosition_OutputsWhereTheAgentStands_WhenItIsNearerTheTargetThanTheRing()
        {
            thug.transform.position = new Vector3(0f, 0f, 1f);
            director.SetTarget(Vector3.zero, Vector3.forward);
            director.Tick(AnyTick);
            TreeOfOne(thug, out GetStandingPosition getStandingPosition);

            Assert.AreEqual(thug.transform.position, ValueOf<Vector3>(getStandingPosition.Output));
        }

        [Test]
        public void GetStandingPosition_OutputsWhereTheAgentStands_OnAnObjectWithoutAnEncounterAgent()
        {
            loner.transform.position = new Vector3(3f, 0f, -2f);
            TreeOfOne(loner, out GetStandingPosition getStandingPosition);

            Assert.AreEqual(loner.transform.position, ValueOf<Vector3>(getStandingPosition.Output));
        }

        [Test]
        public void GetFreePositionCloserToTarget_OutputsTheSpotStraightInFromTheAgent_AtTheDistanceItWasFed()
        {
            const float distanceFromTarget = 2f;
            thug.transform.position = new Vector3(0f, 0f, 9f);
            director.SetTarget(Vector3.zero, Vector3.forward);
            director.Tick(AnyTick);
            TreeOfOne(thug, out GetFreePositionCloserToTarget getFreePositionCloserToTarget);
            getFreePositionCloserToTarget.DistanceFromTarget.SetDefaultValue(distanceFromTarget);

            Assert.AreEqual(new Vector3(0f, 0f, distanceFromTarget), ValueOf<Vector3>(getFreePositionCloserToTarget.Output));
        }

        [Test]
        public void GetFreePositionCloserToTarget_OutputsWhereTheAgentStands_WhenAnotherAgentIsWithinTheRoomItWasFed()
        {
            const float distanceFromTarget = 2f;
            const float roomEachAgentKeeps = 1.5f;
            thug.transform.position = new Vector3(0f, 0f, 9f);
            otherThug.transform.position = new Vector3(1f, 0f, distanceFromTarget);
            AgentInTheEncounter(otherThug);
            director.SetTarget(Vector3.zero, Vector3.forward);
            director.Tick(AnyTick);
            TreeOfOne(thug, out GetFreePositionCloserToTarget getFreePositionCloserToTarget);
            getFreePositionCloserToTarget.DistanceFromTarget.SetDefaultValue(distanceFromTarget);
            getFreePositionCloserToTarget.RoomEachAgentKeeps.SetDefaultValue(roomEachAgentKeeps);

            Assert.AreEqual(agent.StandingPosition, ValueOf<Vector3>(getFreePositionCloserToTarget.Output));
        }

        [Test]
        public void GetFreePositionCloserToTarget_OutputsWhereTheAgentStands_OnAnObjectWithoutAnEncounterAgent()
        {
            loner.transform.position = new Vector3(3f, 0f, -2f);
            TreeOfOne(loner, out GetFreePositionCloserToTarget getFreePositionCloserToTarget);

            Assert.AreEqual(loner.transform.position, ValueOf<Vector3>(getFreePositionCloserToTarget.Output));
        }

        [Test]
        public void GetEncounterTarget_OutputsWhatTheEncounterRevolvesAround()
        {
            TreeOfOne(thug, out GetEncounterTarget getEncounterTarget);

            Assert.AreSame(target.transform, ValueOf<Transform>(getEncounterTarget.Output));
        }

        [Test]
        public void GetEncounterTarget_OutputsNothing_OnAnObjectWithoutAnEncounterAgent()
        {
            TreeOfOne(loner, out GetEncounterTarget getEncounterTarget);

            Assert.IsNull(ValueOf<Transform>(getEncounterTarget.Output));
        }
    }
}
