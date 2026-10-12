using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Characters
{
    /// <summary>
    /// Until whoever moves the character takes the movement, the transform is behind by
    /// <see cref="Movement"/>.
    /// </summary>
    public sealed class PendingWarpMovement : IWarpMover
    {
        private Vector3 movement;

        public Vector3 Movement => movement;

        public void MoveBy(Vector3 planarOffset)
        {
            planarOffset.y = 0f;
            movement += planarOffset;
        }

        public Vector3 TakeMovement()
        {
            Vector3 movementTaken = movement;
            movement = Vector3.zero;
            return movementTaken;
        }
    }
}
