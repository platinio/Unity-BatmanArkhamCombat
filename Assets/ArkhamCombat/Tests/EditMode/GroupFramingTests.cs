using ArcaneOnyx.TPCharacterController.Configuration;
using ArkhamCombat.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class GroupFramingTests
    {
        private const float BaseDistance = 4f;
        private const float Tolerance = 1e-4f;

        private static readonly Vector3 CharacterPosition = Vector3.zero;
        private static readonly GroupFramingSettings Settings = new GroupFramingSettings();

        private static GroupFraming.Result Frame(params Vector3[] enemyPositions) =>
            GroupFraming.Compute(CharacterPosition, enemyPositions, Settings, BaseDistance);

        [Test]
        public void NoEnemies_GivesTheBaseDistance_AndNoGroup()
        {
            GroupFraming.Result result = Frame();

            Assert.IsFalse(result.HasGroup);
            Assert.IsFalse(result.HasYawTarget);
            Assert.AreEqual(BaseDistance, result.Distance, Tolerance);
            Assert.AreEqual(Vector3.zero, result.PivotOffset);
        }

        [Test]
        public void OneEnemy_IsTheCentroid_AndTheYawLooksAtIt()
        {
            GroupFraming.Result result = Frame(new Vector3(3f, 0f, 3f));

            Assert.IsTrue(result.HasGroup);
            Assert.IsTrue(result.HasYawTarget);
            Assert.AreEqual(45f, result.CentroidYaw, Tolerance);
        }

        [Test]
        public void ThePivotDrifts_TowardTheCentroid_ByTheCentroidWeight()
        {
            GroupFraming.Result result = Frame(new Vector3(0f, 0f, 4f));

            Assert.AreEqual(4f * Settings.CentroidWeight, result.PivotOffset.z, Tolerance);
        }

        [Test]
        public void ThePivotDrift_IsCappedAtMaxPivotOffset()
        {
            GroupFraming.Result result = Frame(new Vector3(0f, 0f, 100f));

            Assert.AreEqual(Settings.MaxPivotOffset, result.PivotOffset.magnitude, Tolerance);
        }

        [Test]
        public void TheDistance_ReachesDistanceMax_WhenTheFarthestEnemyIsAtRadiusForMaxDistance()
        {
            GroupFraming.Result result = Frame(new Vector3(0f, 0f, Settings.RadiusForMaxDistance));

            Assert.AreEqual(Settings.DistanceMax, result.Distance, Tolerance);
        }

        [Test]
        public void ARingAroundTheCharacter_WithholdsTheYawTarget()
        {
            GroupFraming.Result result = Frame(
                new Vector3(3f, 0f, 0f), new Vector3(-3f, 0f, 0f), new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, -3f));

            Assert.IsTrue(result.HasGroup);
            Assert.IsFalse(result.HasYawTarget);
        }

        [Test]
        public void EnemyHeight_DoesNotCountTowardTheRadius()
        {
            GroupFraming.Result onTheGround = Frame(new Vector3(0f, 0f, 5f));
            GroupFraming.Result raised = Frame(new Vector3(0f, 10f, 5f));

            Assert.AreEqual(onTheGround.Distance, raised.Distance, Tolerance);
        }
    }
}
