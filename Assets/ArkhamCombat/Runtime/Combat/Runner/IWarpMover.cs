using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The runner never touches a transform: it asks for the movement here, and whoever moves the
    /// character applies it.
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
