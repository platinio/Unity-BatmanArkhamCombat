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
    public class CombatDummyTests
    {
        private const float MaxHealth = 30f;
        private const float AThirdOfItsHealth = 10f;
        private const float Tolerance = 1e-4f;

        private static readonly Regex NoProfileError = new Regex("No hit receiver profile");

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

            dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyObject.hideFlags = HideFlags.HideAndDontSave;
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

        private void WakeTheDummyWithItsProfile()
        {
            SerializedObject serializedDummy = new SerializedObject(dummy);
            serializedDummy.FindProperty("profile").objectReferenceValue = profile;
            serializedDummy.ApplyModifiedPropertiesWithoutUndo();

            WakeTheDummyAsAwakeWould();
        }

        private void WakeTheDummyWithoutAProfile()
        {
            LogAssert.Expect(LogType.Error, NoProfileError);

            WakeTheDummyAsAwakeWould();
        }

        private void WakeTheDummyAsAwakeWould()
        {
            dummy.PrepareBody();
            dummy.CreateReceiver();
        }

        private static HitInfo HitWith(AttackDefinition attack) => new HitInfo(attack, Vector3.forward);

        [Test]
        public void AHit_IsCounted()
        {
            WakeTheDummyWithItsProfile();

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsTrue(result.HasLanded);
            Assert.AreEqual(1, dummy.HitsTaken);
            Assert.AreSame(jab, dummy.LastHitBy);
        }

        [Test]
        public void ADummyThatSurvivesAHit_StaysATarget()
        {
            WakeTheDummyWithItsProfile();

            dummy.Receive(HitWith(jab));

            Assert.IsTrue(dummy.IsValid);
        }

        [Test]
        public void TheHitThatEmptiesItsHealth_KillsIt()
        {
            WakeTheDummyWithItsProfile();
            dummy.Receive(HitWith(jab));
            dummy.Receive(HitWith(jab));

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsTrue(result.WasKilled);
        }

        [Test]
        public void AKilledDummy_StopsBeingATarget()
        {
            WakeTheDummyWithItsProfile();

            dummy.Receive(HitWith(finisher));

            Assert.IsFalse(dummy.IsValid);
        }

        [Test]
        public void ADeadDummy_TakesNoMoreHits()
        {
            WakeTheDummyWithItsProfile();
            dummy.Receive(HitWith(finisher));

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsFalse(result.HasLanded);
            Assert.AreEqual(1, dummy.HitsTaken);
            Assert.AreSame(finisher, dummy.LastHitBy);
        }

        [Test]
        public void AKilledDummy_LiesDownAlongTheHit()
        {
            WakeTheDummyWithItsProfile();
            Vector3 hitDirection = Vector3.right;

            dummy.Receive(new HitInfo(finisher, hitDirection));

            Vector3 whereItsHeadPoints = dummy.transform.up;
            Assert.AreEqual(1f, Vector3.Dot(whereItsHeadPoints, hitDirection), Tolerance);
        }

        [Test]
        public void AKilledDummy_IsLoweredOntoItsSide()
        {
            WakeTheDummyWithItsProfile();
            Bounds standingBounds = dummyObject.GetComponent<Renderer>().bounds;
            float halfHeight = standingBounds.extents.y;
            float halfWidth = standingBounds.extents.x;
            float standingHeight = dummy.transform.position.y;

            dummy.Receive(HitWith(finisher));

            Assert.AreEqual(standingHeight - (halfHeight - halfWidth), dummy.transform.position.y, Tolerance);
        }

        [Test]
        public void AKilledDummy_StopsBlockingTheWay()
        {
            WakeTheDummyWithItsProfile();
            Collider bodyCollider = dummyObject.GetComponent<Collider>();

            dummy.Receive(HitWith(jab));
            Assert.IsTrue(bodyCollider.enabled, "a dummy that is still alive stays in the way");

            dummy.Receive(HitWith(finisher));
            Assert.IsFalse(bodyCollider.enabled);
        }

        [Test]
        public void ADummyWithoutAProfile_IsNeverATarget()
        {
            WakeTheDummyWithoutAProfile();

            Assert.IsFalse(dummy.IsValid);
        }

        [Test]
        public void ADummyWithoutAProfile_CannotBeHit()
        {
            WakeTheDummyWithoutAProfile();

            HitResult result = dummy.Receive(HitWith(jab));

            Assert.IsFalse(result.HasLanded);
            Assert.AreEqual(0, dummy.HitsTaken);
        }
    }
}
