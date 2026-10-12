using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Player
{
    public sealed class MovementFrameTargetForward : ITargetForwardSource
    {
        private readonly IMovementFrame movementFrame;

        public MovementFrameTargetForward(IMovementFrame movementFrame) => this.movementFrame = movementFrame;

        public Vector3 TargetForward => movementFrame.Frame * Vector3.forward;
    }
}
