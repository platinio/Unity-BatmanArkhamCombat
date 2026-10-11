using ArkhamCombat.Enemies;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class EnemyArchetypeWriterTests
    {
        private const float IdleMin = 0.3f;
        private const float IdleMax = 0.9f;
        private const float Tolerance = 1e-5f;

        private GameObject enemy;
        private VariableDeclarations facts;
        private EnemyArchetypeWriter writer;
        private EnemyArchetype archetype;

        [SetUp]
        public void SetUp()
        {
            archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            archetype.Configure(ReceiverProfile(), IdleMin, IdleMax);

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
        public void WithoutAnArchetype_WritesNothing()
        {
            writer.Construct(null);

            writer.WriteTunables();

            Assert.IsFalse(facts.IsDefined(EnemyVariableNames.IdleMin));
            Assert.IsFalse(facts.IsDefined(EnemyVariableNames.IdleMax));
        }
    }
}
