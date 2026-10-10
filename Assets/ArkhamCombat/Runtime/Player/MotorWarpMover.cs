using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>The runner never touches the motor; this is the only bridge to it.</summary>
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
