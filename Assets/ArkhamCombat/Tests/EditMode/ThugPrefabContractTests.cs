using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ArkhamCombat.Tests
{
    // What the code only works with when the prefab is wired a certain way. Nothing compiles the two together, so these read the prefab.
    [TestFixture("Thug")]
    [TestFixture("Armored")]
    [TestFixture("Knife")]
    public class ThugPrefabContractTests
    {
        private readonly string prefabPath;

        private GameObject thug;

        public ThugPrefabContractTests(string prefabName) => prefabPath = $"Assets/ArkhamCombat/Prefabs/{prefabName}.prefab";

        [SetUp]
        public void SetUp()
        {
            thug = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(thug, $"no prefab at {prefabPath}");
        }

        [Test]
        public void TheFactsAreWrittenBeforeTheActionsTick_AndTheWarpAndTheTelegraphFollowThem()
        {
            SerializedProperty jobs = new SerializedObject(thug.GetComponent<CharacterComponentUpdater>()).FindProperty("characterComponents");

            Assert.AreEqual(4, jobs.arraySize);
            Assert.IsInstanceOf<EncounterFactsUpdater>(jobs.GetArrayElementAtIndex(0).objectReferenceValue);
            Assert.IsInstanceOf<CombatActions>(jobs.GetArrayElementAtIndex(1).objectReferenceValue);
            Assert.IsInstanceOf<NavMeshWarpMover>(jobs.GetArrayElementAtIndex(2).objectReferenceValue);
            Assert.IsInstanceOf<TelegraphRelay>(jobs.GetArrayElementAtIndex(3).objectReferenceValue);
        }

        [Test]
        public void TheThugAimsItsActions_AtWhoeverTheEncounterRevolvesAround()
        {
            Assert.IsTrue(thug.TryGetComponent(out EncounterTargetPicker _), "the installer binds the target picker it finds on the enemy");
        }

        [Test]
        public void TheThugsArchetype_NamesAnAttackThatTelegraphsBeforeItCanHit()
        {
            EnemyArchetype archetype = (EnemyArchetype)new SerializedObject(thug.GetComponent<EnemyInstaller>()).FindProperty("archetype").objectReferenceValue;

            Assert.IsNotNull(archetype.Attack);
            Assert.Greater(archetype.Attack.TelegraphWindow.Length, 0f);
            Assert.LessOrEqual(archetype.Attack.TelegraphWindow.End, archetype.Attack.HitWindow.Start);
        }

        [Test]
        public void WhatThePlayerStrikes_SitsBesideTheEncounterAgent()
        {
            EncounterAgent agent = thug.GetComponent<EncounterAgent>();

            Assert.IsNotNull(agent);
            Assert.IsTrue(agent.TryGetComponent(out ICombatTarget _), "the roster looks for the target on the agent's own object");
        }

        [Test]
        public void ThePictureSitsOnTheBodyChild_SoAVariantSwapsItByDeactivatingThatChild()
        {
            ProceduralPresentationDriver driver = thug.GetComponentInChildren<ProceduralPresentationDriver>();

            Assert.IsNotNull(driver);
            Assert.AreNotSame(thug.transform, driver.transform, "a driver on the root would still be found once the capsule child is deactivated");
            Assert.AreSame(driver.transform, driver.Body);
        }

        [Test]
        public void OnlyTheTreeTurnsTheThug()
        {
            Assert.AreEqual(0f, thug.GetComponent<NavMeshAgent>().angularSpeed, "a NavMeshAgent that turned it would fight the tree's Face Target");
        }

        [Test]
        public void TheNavMeshAgentStandsWhereTheColliderDoes()
        {
            NavMeshAgent navMeshAgent = thug.GetComponent<NavMeshAgent>();
            CapsuleCollider capsule = thug.GetComponent<CapsuleCollider>();

            Assert.AreEqual(capsule.height * 0.5f, navMeshAgent.baseOffset, 1e-4f, "the prefab's origin is the capsule's centre");
            Assert.AreEqual(capsule.radius, navMeshAgent.radius, 1e-4f);
        }
    }
}
