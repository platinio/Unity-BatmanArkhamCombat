using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class DemoHitWindowListenerTests
    {
        private sealed class HitRangeSettings : IHitRangeSettings
        {
            public float HitRangeMargin => 0.5f;
        }

        private const float StrikeDistance = 1f;

        private GameObject character;
        private MotorWarpMover warpMover;
        private DemoHitWindowListener listener;
        private AttackDefinition jab;

        [SetUp]
        public void SetUp()
        {
            character = HiddenObject("Character");
            CharacterMotor motor = character.AddComponent<CharacterMotor>();
            warpMover = new MotorWarpMover();
            listener = new DemoHitWindowListener(new HitRangeSettings(), motor, warpMover, null);
            jab = Attack("Jab", strikeDistance: StrikeDistance);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(jab);
        }

        private static PointCombatTarget TargetAhead(float distance) => new PointCombatTarget(new Vector3(0f, 0f, distance));

        [Test]
        public void ATargetWithinStrikeDistancePlusTheMargin_IsHit()
        {
            PointCombatTarget target = TargetAhead(1.4f);

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(1, target.HitsTaken);
            Assert.AreSame(jab, target.LastReceived);
        }

        [Test]
        public void ATargetBeyondTheMargin_IsAWhiff()
        {
            PointCombatTarget target = TargetAhead(1.6f);

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(0, target.HitsTaken);
        }

        [Test]
        public void TheWarpStillPendingThisTick_CountsTowardTheRange()
        {
            PointCombatTarget target = TargetAhead(3f);
            warpMover.MoveBy(new Vector3(0f, 0f, 2f));

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(1, target.HitsTaken, "the character lands 1 m from the target this tick");
        }

        [Test]
        public void AnInvalidTarget_IsAWhiff()
        {
            PointCombatTarget target = TargetAhead(1f);
            target.IsValid = false;

            listener.HitWindowOpened(jab, target);

            Assert.AreEqual(0, target.HitsTaken);
        }

        [Test]
        public void ATargetThatCannotBeHit_IsAWhiff()
        {
            listener.HitWindowOpened(jab, new PointTarget(new Vector3(0f, 0f, 1f)));
        }
    }
}
