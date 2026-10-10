using UnityEngine;

namespace ArkhamCombat.Combat
{
    public readonly struct StrikeOrigin
    {
        public readonly Vector3 Position;
        public readonly Vector3 Forward;

        public StrikeOrigin(Vector3 position, Vector3 forward)
        {
            Position = position;
            Forward = forward;
        }
    }
}
