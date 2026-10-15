using ArkhamCombat.Combat;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EnemyArchetypeWriterTests
    {
        private const float IdleMin = 0.3f;
        private const float IdleMax = 0.9f;
        private const float CombatRange = 6f;
        private const float MoveSpeed = 2.5f;
        private const float Tolerance = 1e-5f;

        private GameObject enemy;
        private VariableDeclarations facts;
        private EnemyArchetypeWriter writer;
        private EnemyArchetype archetype;
        private AttackDefinition haymaker;

        [SetUp]
        public void SetUp()
        {
            archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.Configure(ReceiverProfile(), IdleMin, IdleMax);
            archetype.ConfigureMovement(CombatRange, MoveSpeed);
            haymaker = Attack("Haymaker");

            enemy = HiddenObject("Enemy");
            enemy.AddComponent<Variables>();
            facts = Variables.Object(enemy);
            writer = enemy.AddComponent<EnemyArchetypeWriter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(archetype.HitReceiverProfile);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(haymaker);
            Object.DestroyImmediate(enemy);
        }

        [Test]
        public void WritesTheArchetypesIdleRangeOntoTheAgent()
        {
            writer.Construct(archetype);

            writer.WriteTunables();

            Assert.AreEqual(IdleMin, (float)facts.Get(EnemyVariableNames.IdleMin), Tolerance);
            Assert.AreEqual(IdleMax, (float)facts.Get(EnemyVariableNames.IdleMax), Tolerance);
        }

        [Test]
        public void WritesTheArchetypesCombatRangeOntoTheAgent()
        {
            writer.Construct(archetype);

            writer.WriteTunables();

            Assert.AreEqual(CombatRange, (float)facts.Get(EnemyVariableNames.CombatRange), Tolerance);
        }

        [Test]
        public void WritesTheArchetypesMoveSpeedOntoTheAgent_SoABranchThatWalkedSlowlyCanWalkFastAgain()
        {
            writer.Construct(archetype);

            writer.WriteTunables();

            Assert.AreEqual(MoveSpeed, (float)facts.Get(EnemyVariableNames.MoveSpeed), Tolerance);
        }

        [Test]
        public void WritesTheArchetypesTauntChanceOntoTheAgent()
        {
            const float tauntChance = 0.4f;
            archetype.ConfigureTauntChance(tauntChance);
            writer.Construct(archetype);

            writer.WriteTunables();

            Assert.AreEqual(tauntChance, (float)facts.Get(EnemyVariableNames.TauntChance), Tolerance);
        }

        [Test]
        public void WritesTheArchetypesAttackOntoTheAgent()
        {
            archetype.ConfigureAttack(haymaker);
            writer.Construct(archetype);

            writer.WriteTunables();

            Assert.AreSame(haymaker, facts.Get(EnemyVariableNames.Attack));
        }

        [Test]
        public void TheEnemyWalksAtItsArchetypesMoveSpeed()
        {
            NavMeshAgent navMeshAgent = enemy.AddComponent<NavMeshAgent>();
            writer.Construct(archetype);

            writer.ApplyMoveSpeed();

            Assert.AreEqual(MoveSpeed, navMeshAgent.speed, Tolerance);
        }

        [Test]
        public void WakingUp_WritesTheTunables_AndSetsTheWalkingSpeed()
        {
            NavMeshAgent navMeshAgent = enemy.AddComponent<NavMeshAgent>();
            writer.Construct(archetype);

            writer.Awake();

            Assert.AreEqual(CombatRange, (float)facts.Get(EnemyVariableNames.CombatRange), Tolerance);
            Assert.AreEqual(MoveSpeed, navMeshAgent.speed, Tolerance);
        }

        [Test]
        public void AnEnemyThatDoesNotWalk_StillGetsItsTunables()
        {
            writer.Construct(archetype);

            writer.Awake();

            Assert.AreEqual(IdleMin, (float)facts.Get(EnemyVariableNames.IdleMin), Tolerance);
        }

        [Test]
        public void WithoutAnArchetype_TheWalkingSpeedIsLeftAlone()
        {
            NavMeshAgent navMeshAgent = enemy.AddComponent<NavMeshAgent>();
            float speedOnThePrefab = navMeshAgent.speed;
            writer.Construct(null);

            writer.ApplyMoveSpeed();

            Assert.AreEqual(speedOnThePrefab, navMeshAgent.speed, Tolerance);
        }

        [Test]
        public void WithoutAnArchetype_WritesNothing()
        {
            writer.Construct(null);

            writer.WriteTunables();

            Assert.IsFalse(facts.IsDefined(EnemyVariableNames.IdleMin));
            Assert.IsFalse(facts.IsDefined(EnemyVariableNames.IdleMax));
        }
    }
}
