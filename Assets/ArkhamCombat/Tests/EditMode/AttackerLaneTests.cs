using ArkhamCombat.Encounter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode never calls Awake, OnEnable or OnDisable on these components, so each test calls them by hand.
    public class AttackerLaneTests
    {
        private const int AttackerPriority = 12;
        private const int DefaultPriority = 61;
        private const float MaxTokenHoldSeconds = 3f;

        private EncounterDirectorSettings settings;
        private EncounterDirector director;
        private GameObject encounterObject;
        private GameObject thug;
        private EncounterAgent agent;
        private NavMeshAgent navMeshAgent;
        private AttackerLane lane;

        [SetUp]
        public void SetUp()
        {
            settings = new EncounterDirectorSettings(
                maxTokenHoldSeconds: MaxTokenHoldSeconds, attackerAvoidancePriority: AttackerPriority, defaultAvoidancePriority: DefaultPriority);
            director = new EncounterDirector(settings);

            encounterObject = HiddenObject("Encounter");
            EncounterDirectorBehaviour encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);

            thug = HiddenObject("Thug");
            navMeshAgent = thug.AddComponent<NavMeshAgent>();
            agent = thug.AddComponent<EncounterAgent>();
            agent.Construct(encounter);
            agent.OnEnable();

            lane = thug.AddComponent<AttackerLane>();
            lane.Construct(settings);
            lane.Awake();
            lane.OnEnable();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(encounterObject);
        }

        [Test]
        public void WithoutAToken_TheAgentHasTheDefaultPriority()
        {
            Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void WhileItHoldsAToken_TheOthersSteerAroundIt()
        {
            TakeAToken(director, agent);

            Assert.AreEqual(AttackerPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void WhenItGivesTheTokenBack_ItsPriorityIsTheDefaultAgain()
        {
            TakeAToken(director, agent);

            agent.ReleaseToken();

            Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void WhenTheDirectorTakesTheTokenBack_ItsPriorityIsTheDefaultAgain()
        {
            TakeAToken(director, agent);

            director.Tick(MaxTokenHoldSeconds);

            Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void AnotherAgentsToken_ChangesNothingHere()
        {
            GameObject otherThug = HiddenObject("Other thug");
            EncounterAgent otherAgent = otherThug.AddComponent<EncounterAgent>();
            otherAgent.Construct(encounterObject.GetComponent<EncounterDirectorBehaviour>());
            otherAgent.OnEnable();

            try
            {
                TakeAToken(director, otherAgent);

                Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
            }
            finally
            {
                Object.DestroyImmediate(otherThug);
            }
        }

        [Test]
        public void EnabledWhileItsAgentAlreadyHoldsAToken_ItTakesTheLane()
        {
            lane.OnDisable();
            TakeAToken(director, agent);

            lane.OnEnable();

            Assert.AreEqual(AttackerPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void DisabledWhileItsAgentHoldsAToken_ItGivesTheLaneBack()
        {
            TakeAToken(director, agent);

            lane.OnDisable();

            Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
        }

        [Test]
        public void OnceDisabled_ItNoLongerFollowsTheToken()
        {
            lane.OnDisable();

            TakeAToken(director, agent);

            Assert.AreEqual(DefaultPriority, navMeshAgent.avoidancePriority);
        }
    }
}
