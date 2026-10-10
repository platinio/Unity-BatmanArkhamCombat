using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Null when nothing qualifies. It knows nothing about the character or its actions. The spec
    /// 04 scorer replaces the stand-in behind this without touching its callers.
    /// </summary>
    public interface ITargetScorer
    {
        /// <param name="position">Where the character stands.</param>
        /// <param name="direction">Planar direction the choice should favour. Zero lets distance alone decide.</param>
        IActionTarget BestTarget(Vector3 position, Vector3 direction);
    }
}
