using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Hits are targeted: a strike lands on the one target its action picked when that target is
    /// within range and in front. Nothing is swept and no collider is asked.
    /// </summary>
    public sealed class HitResolver
    {
        private readonly IHitCheckSettings settings;

        public HitResolver(IHitCheckSettings settings)
        {
            this.settings = settings;
        }

        public bool TryLand(AttackDefinition attack, StrikeOrigin origin, Vector3 targetPosition, out HitInfo hit)
        {
            Vector3 toTarget = targetPosition - origin.Position;
            toTarget.y = 0f;

            bool isStandingOnTarget = toTarget.sqrMagnitude < AttackDefinition.StandingOnTargetSqrDistance;
            if (isStandingOnTarget)
            {
                hit = new HitInfo(attack, Vector3.zero);
                return true;
            }

            if (IsWithinRange(attack, toTarget) && IsInFront(origin, toTarget))
            {
                hit = new HitInfo(attack, toTarget.normalized);
                return true;
            }

            hit = default;
            return false;
        }

        private bool IsWithinRange(AttackDefinition attack, Vector3 toTarget) =>
            toTarget.magnitude <= attack.StrikeDistance + settings.HitRangeMargin;

        private bool IsInFront(StrikeOrigin origin, Vector3 toTarget)
        {
            Vector3 facing = origin.Forward;
            facing.y = 0f;
            return Vector3.Angle(facing, toTarget) <= settings.HitAngle;
        }
    }
}
