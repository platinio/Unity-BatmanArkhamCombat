using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>It knows nothing about combos, presses or who is asking.</summary>
    public interface IActionTargetPicker
    {
        /// <param name="direction">
        /// Preferred direction in the character's movement frame: the stick for the player, wherever
        /// an agent aims for an enemy. Zero means straight ahead.
        /// </param>
        IActionTarget PickTarget(Vector2 direction);
    }

    public sealed class NullActionTargetPicker : IActionTargetPicker
    {
        public IActionTarget PickTarget(Vector2 direction) => null;
    }
}
