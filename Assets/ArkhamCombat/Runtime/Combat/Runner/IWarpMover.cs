using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The runner never touches a transform: the player's mover turns the offset into the frame's
    /// motion intent, an enemy's into a NavMeshAgent move.
    /// </summary>
    public interface IWarpMover
    {
        void MoveBy(Vector3 planarOffset);
    }

    public sealed class NullWarpMover : IWarpMover
    {
        public void MoveBy(Vector3 planarOffset) { }
    }
}
