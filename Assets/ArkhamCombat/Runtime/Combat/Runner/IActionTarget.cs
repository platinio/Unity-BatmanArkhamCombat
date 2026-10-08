using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Who an action is aimed at. The runner reads a position for the warp and hands the target to
    /// the hit window listener when the hit window opens; target selection decides who it is.
    /// </summary>
    public interface IActionTarget
    {
        bool IsValid { get; }

        Vector3 Position { get; }
    }
}
