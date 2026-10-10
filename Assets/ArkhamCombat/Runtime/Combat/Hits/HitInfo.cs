using UnityEngine;

namespace ArkhamCombat.Combat
{
    public readonly struct HitInfo
    {
        public readonly AttackDefinition Attack;
        public readonly Vector3 DirectionFromAttackerToTarget;

        public HitInfo(AttackDefinition attack, Vector3 directionFromAttackerToTarget)
        {
            Attack = attack;
            DirectionFromAttackerToTarget = directionFromAttackerToTarget;
        }

        public float Damage => Attack.Damage;

        public HitReaction RequestedReaction => Attack.Reaction;
    }
}
