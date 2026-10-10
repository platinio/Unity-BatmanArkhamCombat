using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Hands a target to whoever asks: the runner when an action starts, the facts when they measure
    /// what the next press would hit. It knows nothing about combos, presses or who is asking.
    /// </summary>
    public interface IActionTargetPicker
    {
        /// <param name="direction">
        /// Preferred direction in the character's movement frame: the stick for the player, wherever
        /// an agent aims for an enemy. Zero means straight ahead.
        /// </param>
        IActionTarget PickTarget(Vector2 direction);
    }

    /// <summary>Never finds a target. For a character with no targeting, and for tests.</summary>
    public sealed class NullActionTargetPicker : IActionTargetPicker
    {
        public IActionTarget PickTarget(Vector2 direction) => null;
    }
}
