using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Collects the runner's warp movement for the frame so the attacking state can hand it to the
    /// motor as that frame's planar velocity. The runner never touches the motor; this is the only
    /// bridge.
    /// </summary>
    public sealed class MotorWarpMover : IWarpMover
    {
        private Vector3 pendingMovement;

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
