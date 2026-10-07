using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class StandInTargetPickerTests
    {
        private sealed class FakeTarget : ICombatTarget
        {
            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; }
            public string State => "Idle";
            public AttackDefinition LastReceived;

            public FakeTarget(Vector3 position) => Position = position;

            public void Receive(AttackDefinition attack) => LastReceived = attack;
        }

        private sealed class FakeRoster : ITargetRoster
        {
            public readonly List<ICombatTarget> Targets = new List<ICombatTarget>();

            IReadOnlyList<ICombatTarget> ITargetRoster.Targets => Targets;
        }

        private CombatConfig config;
        private FakeRoster roster;
        private ITargetPicker picker;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CombatConfig>();
            roster = new FakeRoster();
            picker = new StandInTargetPicker(config, roster);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        private FakeTarget Add(Vector3 position)
        {
            FakeTarget target = new FakeTarget(position);
            roster.Targets.Add(target);
            return target;
        }

        [Test]
        public void PicksTheNearestTargetAlongThePreferredDirection()
        {
            FakeTarget near = Add(new Vector3(0f, 0f, 2f));
            Add(new Vector3(0f, 0f, 5f));

            Assert.AreSame(near, picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void PrefersATargetInTheDirectionOverACloserOneBehind()
        {
            Add(new Vector3(0f, 0f, -1.5f));
            FakeTarget ahead = Add(new Vector3(0f, 0f, 3f));

            Assert.AreSame(ahead, picker.Pick(Vector3.zero, Vector3.forward), "the one behind is outside the angle limit");
        }

        [Test]
        public void NothingInRange_GivesNull()
        {
            Add(new Vector3(0f, 0f, 50f));

            Assert.IsNull(picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void AnInvalidTarget_IsSkipped()
        {
            FakeTarget gone = Add(new Vector3(0f, 0f, 1f));
            gone.IsValid = false;
            FakeTarget alive = Add(new Vector3(0f, 0f, 3f));

            Assert.AreSame(alive, picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void WithNoPreferredDirection_DistanceAloneDecides()
        {
            Add(new Vector3(0f, 0f, 3f));
            FakeTarget behindButNear = Add(new Vector3(0f, 0f, -1f));

            Assert.AreSame(behindButNear, picker.Pick(Vector3.zero, Vector3.zero));
        }
    }

    public class SceneTargetRosterTests
    {
        [Test]
        public void FindsEveryCombatTargetComponentInTheScene_OnFirstRead()
        {
            GameObject a = new GameObject("a");
            GameObject b = new GameObject("b");
            CombatDummy dummyA = a.AddComponent<CombatDummy>();
            CombatDummy dummyB = b.AddComponent<CombatDummy>();

            try
            {
                SceneTargetRoster roster = new SceneTargetRoster();
                IReadOnlyList<ICombatTarget> targets = roster.Targets;

                CollectionAssert.Contains(targets, dummyA);
                CollectionAssert.Contains(targets, dummyB);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
