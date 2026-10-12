using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree;
using ArkhamCombat.Combat;
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
        public void ATargetThatWasKilled_IsNoLongerATarget()
        {
            EnemyStatus targetsStatus = MakeTheTargetAFighterOneHitFromDeath(out HitReceiverProfile profile, out AttackDefinition jab);
            JoinTheEncounter();
            Tick();
            Assert.IsTrue(Fact(EnemyVariableNames.HasTarget), "while the target lives");

            targetsStatus.Receive(new HitInfo(jab, Vector3.forward));
            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.HasTarget));
            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(jab);
        }

        private EnemyStatus MakeTheTargetAFighterOneHitFromDeath(out HitReceiverProfile profile, out AttackDefinition jab)
        {
            const float healthForOneHit = 10f;
            profile = ReceiverProfile(healthForOneHit);
            jab = Attack("Jab", damage: healthForOneHit);

            EnemyStatus targetsStatus = target.AddComponent<EnemyStatus>();
            targetsStatus.Construct(profile);
            targetsStatus.Awake();
            return targetsStatus;
        }

        [Test]
        public void TheFactsExist_BeforeTheFirstTick()
        {
            Assert.IsTrue(facts.IsDefined(EnemyVariableNames.HasTarget));
            Assert.IsTrue(facts.IsDefined(EnemyVariableNames.TargetDistance));
            Assert.IsTrue(facts.IsDefined(EnemyVariableNames.InCombatRange));
            Assert.IsTrue(facts.IsDefined(EnemyVariableNames.HasToken));
        }

        [Test]
        public void InASceneWithNoEncounter_EveryFactSaysThereIsNoFight()
        {
            LogAssert.Expect(LogType.Warning, new Regex("found no encounter director"));
            agent.Construct(null);
            agent.OnEnable();

            Tick();

            AssertEveryFactSaysThereIsNoFight();
        }

        [Test]
        public void OnceItHasLeftTheEncounter_EveryFactSaysThereIsNoFight()
        {
            JoinTheEncounter();
            StandThisFarFromTheTarget(CombatRange - 0.5f);
            TakeAToken(director, agent);
            Tick();
            Assert.IsTrue(Fact(EnemyVariableNames.InCombatRange) && Fact(EnemyVariableNames.HasToken), "sanity: in range and holding the token before it leaves");

            agent.OnDisable();
            Tick();

            AssertEveryFactSaysThereIsNoFight();
        }

        private void AssertEveryFactSaysThereIsNoFight()
        {
            Assert.IsFalse(Fact(EnemyVariableNames.HasTarget));
            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
            Assert.IsFalse(Fact(EnemyVariableNames.HasToken));
            Assert.IsTrue(float.IsPositiveInfinity((float)facts.Get(EnemyVariableNames.TargetDistance)), "no target is infinitely far, never zero metres away");
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
            StandThisFarFromTheTarget(0f);

            Tick();

            Assert.IsFalse(Fact(EnemyVariableNames.InCombatRange));
        }

        [Test]
        public void HasToken_FollowsTheDirector()
        {
            JoinTheEncounter();
            Tick();
            Assert.IsFalse(Fact(EnemyVariableNames.HasToken), "before it has the turn");

            TakeAToken(director, agent);
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
