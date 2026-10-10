using UnityEngine;

namespace ArkhamCombat.Combat
{
    public sealed class HitResolver
    {
        private static readonly Vector3 NoDirection = Vector3.zero;

        private readonly IHitCheckSettings settings;

        public HitResolver(IHitCheckSettings settings)
        {
            this.settings = settings;
        }

        public bool TryLand(AttackDefinition attack, StrikeOrigin origin, Vector3 targetPosition, out HitInfo hit)
        {
            Vector3 toTarget = OnTheGround(targetPosition - origin.Position);

            bool isStandingOnTarget = toTarget.sqrMagnitude < AttackDefinition.StandingOnTargetSqrDistance;
            if (isStandingOnTarget)
            {
                hit = new HitInfo(attack, NoDirection);
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
            Vector3 facing = OnTheGround(origin.Forward);
            return Vector3.Angle(facing, toTarget) <= settings.HitAngle;
        }

        private static Vector3 OnTheGround(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
    }
}
