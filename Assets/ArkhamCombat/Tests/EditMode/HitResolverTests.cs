using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class HitResolverTests
    {
        private const float StrikeDistance = 1f;
        private const float HitRangeMargin = 0.5f;
        private const float Range = StrikeDistance + HitRangeMargin;
        private const float HitAngle = 60f;
        private const float ALittle = 0.1f;
        private const float ADegree = 1f;
        private const float DirectionTolerance = 1e-4f;

        private static readonly StrikeOrigin AtTheOriginFacingForward = new StrikeOrigin(Vector3.zero, Vector3.forward);

        private AttackDefinition jab;
        private HitResolver resolver;

        [SetUp]
        public void SetUp()
        {
            jab = Attack("Jab", strikeDistance: StrikeDistance);
            resolver = new HitResolver(new HitCheckSettings { HitRangeMargin = HitRangeMargin, HitAngle = HitAngle });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(jab);
        }

        private bool CanLandOn(Vector3 targetPosition) => resolver.TryLand(jab, AtTheOriginFacingForward, targetPosition, out _);

        private static Vector3 PositionAt(float degreesOffTheFacing, float distance) =>
            Quaternion.Euler(0f, degreesOffTheFacing, 0f) * Vector3.forward * distance;

        [Test]
        public void ATargetWithinStrikeDistancePlusTheMargin_IsHit()
        {
            Assert.IsTrue(CanLandOn(Vector3.forward * (Range - ALittle)));
        }

        [Test]
        public void ATargetExactlyAtTheEdgeOfTheRange_IsHit()
        {
            Assert.IsTrue(CanLandOn(Vector3.forward * Range));
        }

        [Test]
        public void ATargetBeyondTheRange_IsAWhiff()
        {
            Assert.IsFalse(CanLandOn(Vector3.forward * (Range + ALittle)));
        }

        [Test]
        public void ATargetBehindTheAttacker_IsAWhiff()
        {
            Assert.IsFalse(CanLandOn(Vector3.back * StrikeDistance));
        }

        [Test]
        public void ATargetJustInsideTheHitAngle_IsHit()
        {
            Assert.IsTrue(CanLandOn(PositionAt(HitAngle - ADegree, StrikeDistance)));
            Assert.IsTrue(CanLandOn(PositionAt(-(HitAngle - ADegree), StrikeDistance)));
        }

        [Test]
        public void ATargetJustOutsideTheHitAngle_IsAWhiff()
        {
            Assert.IsFalse(CanLandOn(PositionAt(HitAngle + ADegree, StrikeDistance)));
            Assert.IsFalse(CanLandOn(PositionAt(-(HitAngle + ADegree), StrikeDistance)));
        }

        [Test]
        public void TheRangeIsMeasuredOnTheGround_WhateverTheHeightDifference()
        {
            Vector3 inRangeButHighAbove = new Vector3(0f, 5f, StrikeDistance);

            Assert.IsTrue(CanLandOn(inRangeButHighAbove));
        }

        [Test]
        public void TheFacingIsMeasuredOnTheGround_HoweverSteeplyTheAttackerLooksDown()
        {
            StrikeOrigin lookingDownAndAhead = new StrikeOrigin(Vector3.zero, new Vector3(0f, -0.9f, 0.4f).normalized);

            bool hasLanded = resolver.TryLand(jab, lookingDownAndAhead, Vector3.forward * StrikeDistance, out _);

            Assert.IsTrue(hasLanded);
        }

        [Test]
        public void AnAttackerStandingOnItsTarget_Hits_WhicheverWayItFaces()
        {
            StrikeOrigin facingAway = new StrikeOrigin(Vector3.zero, Vector3.back);

            bool hasLanded = resolver.TryLand(jab, facingAway, Vector3.zero, out _);

            Assert.IsTrue(hasLanded);
        }

        [Test]
        public void TheHit_OnATargetTheAttackerStandsOn_HasNoDirection()
        {
            resolver.TryLand(jab, AtTheOriginFacingForward, Vector3.zero, out HitInfo hit);

            Assert.AreEqual(Vector3.zero, hit.DirectionFromAttackerToTarget);
        }

        [Test]
        public void TheHit_CarriesTheAttack()
        {
            resolver.TryLand(jab, AtTheOriginFacingForward, Vector3.forward * StrikeDistance, out HitInfo hit);

            Assert.AreSame(jab, hit.Attack);
        }

        [Test]
        public void TheHit_PointsFromTheAttackerTowardTheTarget_OnTheGround_WithLengthOne()
        {
            Vector3 aheadToTheRightAndAbove = new Vector3(0.6f, 2f, 0.8f);

            resolver.TryLand(jab, AtTheOriginFacingForward, aheadToTheRightAndAbove, out HitInfo hit);

            Vector3 direction = hit.DirectionFromAttackerToTarget;
            Assert.AreEqual(0.6f, direction.x, DirectionTolerance);
            Assert.AreEqual(0f, direction.y, DirectionTolerance);
            Assert.AreEqual(0.8f, direction.z, DirectionTolerance);
        }
    }
}
