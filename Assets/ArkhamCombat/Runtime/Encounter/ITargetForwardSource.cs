using UnityEngine;

namespace ArkhamCombat.Encounter
{
    /// <summary>
    /// The game decides what "in front of the target" means: where the camera looks, so attacks
    /// come from on screen, or where the target itself faces.
    /// </summary>
    public interface ITargetForwardSource
    {
        Vector3 TargetForward { get; }
    }
}
