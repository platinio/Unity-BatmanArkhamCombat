using UnityEngine;

namespace ArkhamCombat.Encounter
{
    /// <summary>The game decides what "in front of the target" means.</summary>
    public interface ITargetForwardSource
    {
        Vector3 TargetForward { get; }
    }
}
