using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class StandInTargetScorerTests
    {
        private sealed class ListRoster : ITargetRoster
        {
            public readonly List<ICombatTarget> Targets = new List<ICombatTarget>();

            IReadOnlyList<ICombatTarget> ITargetRoster.Targets => Targets;
        }

        private sealed class TargetingSettings : ITargetingSettings
        {
            public float MaxTargetDistance { get; set; } = 8f;
            public float MaxTargetAngle { get; set; } = 110f;
            public float AngleCountingAsDoubleDistance { get; set; } = 90f;
            public float StickPushedMagnitude { get; set; } = 0.1f;
        }

        private TargetingSettings settings;
        private ListRoster roster;
        private ITargetScorer scorer;

        [SetUp]
        public void SetUp()
        {
            settings = new TargetingSettings();
            roster = new ListRoster();
            scorer = new StandInTargetScorer(settings, roster);
        }

        private PointCombatTarget AddTargetAt(Vector3 position)
        {
            PointCombatTarget target = new PointCombatTarget(position);
            roster.Targets.Add(target);
            return target;
        }

        [Test]
        public void ASmallerAngleCountingAsDoubleDistance_FavoursAimOverDistance()
        {
            PointCombatTarget nearButOffTheDirection = AddTargetAt(Quaternion.Euler(0f, 30f, 0f) * Vector3.forward * 2f);
            PointCombatTarget fartherButDeadAhead = AddTargetAt(Vector3.forward * 3f);

            Assert.AreSame(nearButOffTheDirection, scorer.BestTarget(Vector3.zero, Vector3.forward));

            settings.AngleCountingAsDoubleDistance = 30f;

            Assert.AreSame(fartherButDeadAhead, scorer.BestTarget(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void TheBestTarget_IsTheNearestAlongTheDirection()
        {
            PointCombatTarget near = AddTargetAt(new Vector3(0f, 0f, 2f));
            AddTargetAt(new Vector3(0f, 0f, 5f));

            Assert.AreSame(near, scorer.BestTarget(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void PrefersATargetInTheDirectionOverACloserOneBehind()
        {
            AddTargetAt(new Vector3(0f, 0f, -1.5f));
            PointCombatTarget ahead = AddTargetAt(new Vector3(0f, 0f, 3f));

            Assert.AreSame(ahead, scorer.BestTarget(Vector3.zero, Vector3.forward), "the one behind is outside the angle limit");
        }

        [Test]
        public void NothingInRange_GivesNull()
        {
            AddTargetAt(new Vector3(0f, 0f, 50f));

            Assert.IsNull(scorer.BestTarget(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void AnInvalidTarget_IsSkipped()
        {
            PointCombatTarget gone = AddTargetAt(new Vector3(0f, 0f, 1f));
            gone.IsValid = false;
            PointCombatTarget alive = AddTargetAt(new Vector3(0f, 0f, 3f));

            Assert.AreSame(alive, scorer.BestTarget(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void WithNoDirection_DistanceAloneDecides()
        {
            AddTargetAt(new Vector3(0f, 0f, 3f));
            PointCombatTarget behindButNear = AddTargetAt(new Vector3(0f, 0f, -1f));

            Assert.AreSame(behindButNear, scorer.BestTarget(Vector3.zero, Vector3.zero));
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
