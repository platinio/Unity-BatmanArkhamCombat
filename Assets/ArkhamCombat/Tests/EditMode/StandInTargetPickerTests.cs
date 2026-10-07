using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class StandInTargetPickerTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private CombatConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CombatConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                Object.DestroyImmediate(go);
            }

            spawned.Clear();
            Object.DestroyImmediate(config);
        }

        private CombatDummy Dummy(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);
            return go.AddComponent<CombatDummy>();
        }

        [Test]
        public void PicksTheNearestDummyAlongThePreferredDirection()
        {
            CombatDummy near = Dummy("near", new Vector3(0f, 0f, 2f));
            Dummy("far", new Vector3(0f, 0f, 5f));
            ITargetPicker picker = new StandInTargetPicker(config);

            IActionTarget target = picker.Pick(Vector3.zero, Vector3.forward);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(near.transform.position, target.Position);
        }

        [Test]
        public void PrefersADummyInTheDirectionOverACloserOneBehind()
        {
            Dummy("behind", new Vector3(0f, 0f, -1.5f));
            CombatDummy ahead = Dummy("ahead", new Vector3(0f, 0f, 3f));
            ITargetPicker picker = new StandInTargetPicker(config);

            IActionTarget target = picker.Pick(Vector3.zero, Vector3.forward);

            Assert.AreEqual(ahead.transform.position, target.Position, "the one behind is outside the angle limit");
        }

        [Test]
        public void NothingInRange_GivesAnInvalidTarget()
        {
            Dummy("far", new Vector3(0f, 0f, 50f));
            ITargetPicker picker = new StandInTargetPicker(config);

            IActionTarget target = picker.Pick(Vector3.zero, Vector3.forward);

            Assert.IsFalse(target.IsValid);
        }

        [Test]
        public void TheSameTargetInstance_IsReusedAcrossPicks()
        {
            Dummy("a", new Vector3(0f, 0f, 2f));
            ITargetPicker picker = new StandInTargetPicker(config);

            IActionTarget first = picker.Pick(Vector3.zero, Vector3.forward);
            IActionTarget second = picker.Pick(Vector3.zero, Vector3.forward);

            Assert.AreSame(first, second, "picking allocates nothing per frame");
        }
    }
}
