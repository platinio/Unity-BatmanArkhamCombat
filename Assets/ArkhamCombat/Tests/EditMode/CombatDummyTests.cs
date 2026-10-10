using System.Text.RegularExpressions;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode runs no Awake, so each test creates the dummy's receiver by hand.
    public class CombatDummyTests
    {
        private const float MaxHealth = 30f;
        private const float AThirdOfItsHealth = 10f;
        private const float DirectionTolerance = 1e-4f;

        private GameObject dummyObject;
        private CombatDummy dummy;
        private HitReceiverProfile profile;
        private AttackDefinition jab;
        private AttackDefinition finisher;

        [SetUp]
        public void SetUp()
        {
            profile = ReceiverProfile(MaxHealth);
            jab = Attack("Jab", damage: AThirdOfItsHealth);
            finisher = Attack("Finisher", damage: MaxHealth);

            dummyObject = HiddenObject("Dummy");
            dummy = dummyObject.AddComponent<CombatDummy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(dummyObject);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(finisher);
        }

        private void GiveTheDummyItsProfile()
        {
            SerializedObject serializedDummy = new SerializedObject(dummy);
            serializedDummy.FindProperty("profile").objectReferenceValue = profile;
            serializedDummy.ApplyModifiedPropertiesWithoutUndo();
            dummy.CreateReceiver();
        }

        private static HitInfo HitWith(AttackDefinition attack) => new HitInfo(attack, Vector3.forward);

        [Test]
        public void AHit_IsCounted_AndTheDummyStaysATarget()
        {
            GiveTheDummyItsProfile();

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(1, dummy.HitsTaken);
            Assert.AreSame(jab, dummy.LastHitBy);
            Assert.IsTrue(dummy.IsValid);
        }

        [Test]
        public void TheHitThatEmptiesItsHealth_KillsIt_AndItStopsBeingATarget()
        {
            GiveTheDummyItsProfile();
            dummy.Receive(HitWith(jab));
            dummy.Receive(HitWith(jab));

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsTrue(result.WasKilled);
            Assert.IsFalse(dummy.IsValid);
        }

        [Test]
        public void ADeadDummy_TakesNoMoreHits()
        {
            GiveTheDummyItsProfile();
            dummy.Receive(HitWith(finisher));

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsFalse(result.HasLanded);
            Assert.AreEqual(1, dummy.HitsTaken);
            Assert.AreSame(finisher, dummy.LastHitBy);
        }

        [Test]
        public void AKilledDummy_LiesDownAlongTheHit()
        {
            GiveTheDummyItsProfile();
            Vector3 hitDirection = Vector3.right;

            dummy.Receive(new HitInfo(finisher, hitDirection));

            Vector3 whereItsHeadPoints = dummy.transform.up;
            Assert.AreEqual(1f, Vector3.Dot(whereItsHeadPoints, hitDirection), DirectionTolerance);
        }

        [Test]
        public void ADummyWithoutAProfile_SaysSo_AndCanNeitherBeTargetedNorHit()
        {
            LogAssert.Expect(LogType.Error, new Regex("No hit receiver profile"));

            dummy.CreateReceiver();
            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsFalse(dummy.IsValid);
            Assert.IsFalse(result.HasLanded);
            Assert.AreEqual(0, dummy.HitsTaken);
        }
    }
}
