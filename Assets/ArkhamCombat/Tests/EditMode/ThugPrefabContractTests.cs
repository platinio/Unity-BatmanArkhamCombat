using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using ArkhamCombat.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ArkhamCombat.Tests
{
    // What the code only works with when the prefab is wired a certain way. Nothing compiles the two together, so these read the prefab.
    public class ThugPrefabContractTests
    {
        private const string ThugPath = "Assets/ArkhamCombat/Prefabs/Thug.prefab";

        private GameObject thug;

        [SetUp]
        public void SetUp()
        {
            thug = AssetDatabase.LoadAssetAtPath<GameObject>(ThugPath);
            Assert.IsNotNull(thug, $"no prefab at {ThugPath}");
        }

        [Test]
        public void TheFactsAreWrittenBeforeTheActionsTick()
        {
            SerializedProperty jobs = new SerializedObject(thug.GetComponent<CharacterComponentUpdater>()).FindProperty("characterComponents");

            Assert.AreEqual(2, jobs.arraySize);
            Assert.IsInstanceOf<EncounterFactsUpdater>(jobs.GetArrayElementAtIndex(0).objectReferenceValue);
            Assert.IsInstanceOf<CombatActions>(jobs.GetArrayElementAtIndex(1).objectReferenceValue);
        }

        [Test]
        public void WhatThePlayerStrikes_SitsBesideTheEncounterAgent()
        {
            EncounterAgent agent = thug.GetComponent<EncounterAgent>();

            Assert.IsNotNull(agent);
            Assert.IsTrue(agent.TryGetComponent(out ICombatTarget _), "the roster looks for the target on the agent's own object");
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
