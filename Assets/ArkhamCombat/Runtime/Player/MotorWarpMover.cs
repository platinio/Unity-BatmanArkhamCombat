using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The runner asks for movement here and never touches the motor. The attacking state hands
    /// the pending movement to the motor as the frame's velocity, after every character component
    /// has ticked, so until then the transform is behind by <see cref="PendingMovement"/>.
    /// </summary>
    public sealed class MotorWarpMover : IWarpMover
    {
        private Vector3 pendingMovement;

        public Vector3 PendingMovement => pendingMovement;

        public void MoveBy(Vector3 planarOffset)
        {
            planarOffset.y = 0f;
            pendingMovement += planarOffset;
        }

        public Vector3 TakePendingMovement()
        {
            Vector3 movement = pendingMovement;
            pendingMovement = Vector3.zero;
            return movement;
        }
    }
}
