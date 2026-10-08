using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class StandInTargetPickerTests
    {
        private sealed class PointCombatTarget : ICombatTarget
        {
            public bool IsValid { get; set; } = true;
            public Vector3 Position { get; set; }
            public string State => "Idle";
            public AttackDefinition LastReceived;

            public PointCombatTarget(Vector3 position) => Position = position;

            public void Receive(AttackDefinition attack) => LastReceived = attack;
        }

        private sealed class ListRoster : ITargetRoster
        {
            public readonly List<ICombatTarget> Targets = new List<ICombatTarget>();

            IReadOnlyList<ICombatTarget> ITargetRoster.Targets => Targets;
        }

        private CombatConfig config;
        private ListRoster roster;
        private ITargetPicker picker;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CombatConfig>();
            roster = new ListRoster();
            picker = new StandInTargetPicker(config, roster);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        private PointCombatTarget AddTargetAt(Vector3 position)
        {
            PointCombatTarget target = new PointCombatTarget(position);
            roster.Targets.Add(target);
            return target;
        }

        [Test]
        public void PicksTheNearestTargetAlongThePreferredDirection()
        {
            PointCombatTarget near = AddTargetAt(new Vector3(0f, 0f, 2f));
            AddTargetAt(new Vector3(0f, 0f, 5f));

            Assert.AreSame(near, picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void PrefersATargetInTheDirectionOverACloserOneBehind()
        {
            AddTargetAt(new Vector3(0f, 0f, -1.5f));
            PointCombatTarget ahead = AddTargetAt(new Vector3(0f, 0f, 3f));

            Assert.AreSame(ahead, picker.Pick(Vector3.zero, Vector3.forward), "the one behind is outside the angle limit");
        }

        [Test]
        public void NothingInRange_GivesNull()
        {
            AddTargetAt(new Vector3(0f, 0f, 50f));

            Assert.IsNull(picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void AnInvalidTarget_IsSkipped()
        {
            PointCombatTarget gone = AddTargetAt(new Vector3(0f, 0f, 1f));
            gone.IsValid = false;
            PointCombatTarget alive = AddTargetAt(new Vector3(0f, 0f, 3f));

            Assert.AreSame(alive, picker.Pick(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void WithNoPreferredDirection_DistanceAloneDecides()
        {
            AddTargetAt(new Vector3(0f, 0f, 3f));
            PointCombatTarget behindButNear = AddTargetAt(new Vector3(0f, 0f, -1f));

            Assert.AreSame(behindButNear, picker.Pick(Vector3.zero, Vector3.zero));
        }
    }

    public class SceneTargetRosterTests
    {
        [Test]
        public void FindsEveryCombatTargetComponentInTheScene_OnFirstRead()
        {
            GameObject firstObject = new GameObject("a");
            GameObject secondObject = new GameObject("b");
            CombatDummy firstDummy = firstObject.AddComponent<CombatDummy>();
            CombatDummy secondDummy = secondObject.AddComponent<CombatDummy>();

            try
            {
                SceneTargetRoster roster = new SceneTargetRoster();
                IReadOnlyList<ICombatTarget> targets = roster.Targets;

                CollectionAssert.Contains(targets, firstDummy);
                CollectionAssert.Contains(targets, secondDummy);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }
    }
}
