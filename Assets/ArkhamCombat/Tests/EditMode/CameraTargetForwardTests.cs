using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class CameraTargetForwardTests
    {
        private const float Tolerance = 1e-4f;

        private sealed class TurnedMovementFrame : IMovementFrame
        {
            public Quaternion Frame { get; set; }
        }

        [Test]
        public void TheTargetsFront_IsWhereTheMovementFrameLooks()
        {
            TurnedMovementFrame lookingEast = new TurnedMovementFrame { Frame = Quaternion.Euler(0f, 90f, 0f) };

            Vector3 forward = new CameraTargetForward(lookingEast).TargetForward;

            Assert.Less((forward - Vector3.right).magnitude, Tolerance);
        }
    }
}
