using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class CombatFactsUpdaterTests
    {
        private const float DeadAheadAngle = 6f;

        private const int OnTheLeft = -1;
        private const int DeadAhead = 0;
        private const int OnTheRight = 1;

        private static readonly Vector3 Facing = Vector3.forward;

        private static Vector3 TargetAtDegreesToTheRight(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Facing * 3f;

        [Test]
        public void ATargetWithinTheDeadAheadAngle_IsDeadAhead()
        {
            Assert.AreEqual(DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(3f), DeadAheadAngle));
            Assert.AreEqual(DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(-3f), DeadAheadAngle));
        }

        [Test]
        public void ATargetBeyondTheDeadAheadAngle_IsOnItsSide()
        {
            Assert.AreEqual(OnTheRight, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(10f), DeadAheadAngle));
            Assert.AreEqual(OnTheLeft, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(-10f), DeadAheadAngle));
        }

        [Test]
        public void AWiderDeadAheadAngle_TakesInATargetFartherToTheSide()
        {
            Assert.AreEqual(DeadAhead, CombatFactsUpdater.SideOf(Facing, TargetAtDegreesToTheRight(10f), deadAheadAngle: 20f));
        }
    }
}
