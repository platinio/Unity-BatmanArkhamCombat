using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The movement frame follows the combat camera, so "in front of the target" is what the
    /// player sees on screen rather than where the character happens to face.
    /// </summary>
    public sealed class CameraTargetForward : ITargetForwardSource
    {
        private readonly IMovementFrame movementFrame;

        public CameraTargetForward(IMovementFrame movementFrame) => this.movementFrame = movementFrame;

        public Vector3 TargetForward => movementFrame.Frame * Vector3.forward;
    }
}
