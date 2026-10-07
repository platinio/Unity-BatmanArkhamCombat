using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Decides who the next action is aimed at. Asked once per frame with where the character is and
    /// where the player is pointing; answers null when nothing qualifies. The spec 04 picker replaces
    /// the stand-in behind this without touching the brain.
    /// </summary>
    public interface ITargetPicker
    {
        /// <param name="origin">The character's position.</param>
        /// <param name="preferredDirection">Planar direction the pick should favour: the stick, or the facing when idle.</param>
        IActionTarget Pick(Vector3 origin, Vector3 preferredDirection);
    }
}
