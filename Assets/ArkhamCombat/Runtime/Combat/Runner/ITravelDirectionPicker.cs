using UnityEngine;

namespace ArkhamCombat.Combat
{
    public interface ITravelDirectionPicker
    {
        /// <param name="direction">
        /// Preferred direction in the character's movement frame, as for
        /// <see cref="IActionTargetPicker.PickTarget"/>: the stick for the player. Zero means none was given.
        /// </param>
        /// <returns>A direction in the world. Zero leaves the character where it is.</returns>
        Vector3 PickTravelDirection(Vector2 direction);
    }

    public sealed class NullTravelDirectionPicker : ITravelDirectionPicker
    {
        public Vector3 PickTravelDirection(Vector2 direction) => Vector3.zero;
    }
}
