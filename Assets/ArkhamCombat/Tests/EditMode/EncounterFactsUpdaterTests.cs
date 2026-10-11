using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode never calls Awake or OnEnable on these components, so each test calls them by hand.
    public class EncounterFactsUpdaterTests
    {
        private const float CombatRange = 5f;
        private const float MoveSpeed = 3f;
        private const float Tolerance = 1e-4f;
        private const float AnyDeltaTime = 0.02f;

        private EncounterDirector director;
        private GameObject encounterObject;
        private EncounterDirectorBehaviour encounter;
        private GameObject target;
        private GameObject thug;
        private EncounterAgent agent;
        private AgentVariableWriter agentVariables;
        private VariableDeclarations facts;
        private EncounterFactsUpdater updater;
        private EnemyArchetype archetype;

        [SetUp]
        public void SetUp()
        {
            director = new EncounterDirector(new EncounterDirectorSettings(maxMeleeTokens: 1));
            encounterObject = HiddenObject("Encounter");
            encounter = encounterObject.AddComponent<EncounterDirectorBehaviour>();
            encounter.Construct(director);
            target = HiddenObject("Target");
            encounter.SetTarget(target.transform);

            archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.ConfigureMovement(CombatRange, MoveSpeed);

            thug = HiddenObject("Thug");
            agent = thug.AddComponent<EncounterAgent>();
            updater = thug.AddComponent<EncounterFactsUpdater>();
            agentVariables = thug.GetComponent<AgentVariableWriter>();
            facts = Variables.Object(thug);

            updater.Construct(archetype);
            updater.Awake();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(thug);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(encounterObject);
            Object.DestroyImmediate(archetype);
        }

        private void JoinTheEncounter()
        {
            agent.Construct(encounter);
            agent.OnEnable();
        }

        private void StandThisFarFromTheTarget(float metres) => thug.transform.position = target.transform.position + Vector3.forward * metres;

        private void Tick() => updater.Tick(AnyDeltaTime);

        private bool Fact(string key) => (bool)facts.Get(key);

        [Test]
        public void InTheEncounter_ItHasATarget()
        {
            JoinTheEncounter();

            Tick();

            Assert.IsTrue(Fact(EnemyVariableNames.HasTarget));
        }

        [Test]
        public void InASceneWithNoEncounter_ItHasNoTarget_AndIsNotInCombatRange()
        {
            LogAssert.Expect(LogType.Warning, new Regex("found no encounter director"));
            agent.Construct(null);
            agent.OnEnable();

            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.HasTarget));
            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
        }

        [Test]
        public void OnceItHasLeftTheEncounter_ItHasNoTarget()
        {
            JoinTheEncounter();
            Tick();

            agent.OnDisable();
            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.HasTarget));
        }

        [Test]
        public void TheTargetDistance_IsMeasuredOnTheGround()
        {
            JoinTheEncounter();
            target.transform.position = new Vector3(1f, 0f, 2f);
            thug.transform.position = new Vector3(4f, 1.8f, 6f);

            Tick();

            Assert.AreEqual(5f, (float)facts.Get(EnemyVariableNames.TargetDistance), Tolerance, "3 across and 4 along, whatever the height");
        }

        [Test]
        public void InsideItsCombatRange_ItIsInCombatRange()
        {
            JoinTheEncounter();
            StandThisFarFromTheTarget(CombatRange - 0.5f);

            Tick();

            Assert.IsTrue(Fact(EnemyVariableNames.InCombatRange));
        }

        [Test]
        public void BeyondItsCombatRange_ItIsNotInCombatRange()
        {
            JoinTheEncounter();
            StandThisFarFromTheTarget(CombatRange + 0.5f);

            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
        }

        [Test]
        public void WithoutAnArchetype_ItIsNeverInCombatRange()
        {
            updater.Construct(null);
            JoinTheEncounter();
            StandThisFarFromTheTarget(0.5f);

            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
        }

        [Test]
        public void HasToken_FollowsTheDirector()
        {
            JoinTheEncounter();
            Tick();
            Assert.IsFalse(Fact(EnemyVariableNames.HasToken), "before it has the turn");

            agent.TryAcquireToken();
            Tick();
            Assert.IsTrue(Fact(EnemyVariableNames.HasToken), "while it has the turn");

            agent.ReleaseToken();
            Tick();
            Assert.IsFalse(Fact(EnemyVariableNames.HasToken), "after giving it back");
        }

        [Test]
        public void AFactThatDidNotChange_IsNotWrittenAgain()
        {
            JoinTheEncounter();
            StandThisFarFromTheTarget(CombatRange - 0.5f);
            Tick();
            int hasTargetVersion = agentVariables.VersionOf(EnemyVariableNames.HasTarget);
            int inCombatRangeVersion = agentVariables.VersionOf(EnemyVariableNames.InCombatRange);
            int hasTokenVersion = agentVariables.VersionOf(EnemyVariableNames.HasToken);

            Tick();

            Assert.AreEqual(hasTargetVersion, agentVariables.VersionOf(EnemyVariableNames.HasTarget));
            Assert.AreEqual(inCombatRangeVersion, agentVariables.VersionOf(EnemyVariableNames.InCombatRange));
            Assert.AreEqual(hasTokenVersion, agentVariables.VersionOf(EnemyVariableNames.HasToken));
        }

        [Test]
        public void AFactThatChanged_IsWrittenAgain()
        {
            JoinTheEncounter();
            StandThisFarFromTheTarget(CombatRange - 0.5f);
            Tick();
            int inCombatRangeVersion = agentVariables.VersionOf(EnemyVariableNames.InCombatRange);

            StandThisFarFromTheTarget(CombatRange + 0.5f);
            Tick();

            Assert.AreNotEqual(inCombatRangeVersion, agentVariables.VersionOf(EnemyVariableNames.InCombatRange), "sanity: the version is what a guard waits on");
        }
    }
}
