using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>A strike that passed the land check, as its target receives it.</summary>
    public readonly struct HitInfo
    {
        public readonly AttackDefinition Attack;

        /// <summary>
        /// From the attacker toward the target, flat and of length one. Zero when the attacker
        /// stands on the target.
        /// </summary>
        public readonly Vector3 Direction;

        public HitInfo(AttackDefinition attack, Vector3 direction)
        {
            Attack = attack;
            Direction = direction;
        }
    }
}
