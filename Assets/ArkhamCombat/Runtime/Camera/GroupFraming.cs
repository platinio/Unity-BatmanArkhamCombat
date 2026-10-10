using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Configuration;
using UnityEngine;

namespace ArkhamCombat.Cameras
{
    /// <summary>
    /// Pure group framing math (spec 07): how a set of enemy positions bends the combat framing.
    /// </summary>
    public static class GroupFraming
    {
        public readonly struct Result
        {
            /// <summary>False when there were no enemies; every other field then holds the fallback.</summary>
            public readonly bool HasGroup;

            /// <summary>Planar drift of the pivot from the character toward the centroid, already capped.</summary>
            public readonly Vector3 PivotOffset;

            public readonly float Distance;

            /// <summary>Yaw, in degrees, that looks from the character toward the centroid.</summary>
            public readonly float CentroidYaw;

            /// <summary>
            /// False when the centroid sits too close to the character for its bearing to mean
            /// anything, as when a ring surrounds them. The camera should hold its heading then.
            /// </summary>
            public readonly bool HasYawTarget;

            public Result(bool hasGroup, Vector3 pivotOffset, float distance, float centroidYaw, bool hasYawTarget)
            {
                HasGroup = hasGroup;
                PivotOffset = pivotOffset;
                Distance = distance;
                CentroidYaw = centroidYaw;
                HasYawTarget = hasYawTarget;
            }
        }

        /// <summary>
        /// One enemy gives a centroid at that enemy and a radius equal to its distance, which
        /// frames a duel correctly. An empty list returns <paramref name="baseDistance"/> with no
        /// offset.
        /// </summary>
        public static Result Compute(
            Vector3 characterPosition,
            IReadOnlyList<Vector3> enemyPositions,
            GroupFramingSettings settings,
            float baseDistance)
        {
            int count = enemyPositions?.Count ?? 0;
            if (count == 0)
            {
                return new Result(false, Vector3.zero, baseDistance, 0f, false);
            }

            Vector3 sum = Vector3.zero;
            float radius = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = enemyPositions[i];
                sum += position;

                Vector3 toEnemy = position - characterPosition;
                toEnemy.y = 0f;
                radius = Mathf.Max(radius, toEnemy.magnitude);
            }

            Vector3 centroid = sum / count;
            Vector3 toCentroid = centroid - characterPosition;
            toCentroid.y = 0f;

            Vector3 pivotOffset = Vector3.ClampMagnitude(
                toCentroid * settings.CentroidWeight, settings.MaxPivotOffset);

            float spread = settings.RadiusForMaxDistance > 0f
                ? Mathf.Clamp01(radius / settings.RadiusForMaxDistance)
                : 1f;
            float distance = Mathf.Lerp(baseDistance, settings.DistanceMax, spread);

            float minYawDistance = Mathf.Max(0f, settings.YawMinCentroidDistance);
            bool hasYawTarget = toCentroid.sqrMagnitude > minYawDistance * minYawDistance
                                && toCentroid.sqrMagnitude > 1e-6f;

            float yaw = hasYawTarget
                ? Mathf.Atan2(toCentroid.x, toCentroid.z) * Mathf.Rad2Deg
                : 0f;

            return new Result(true, pivotOffset, distance, yaw, hasYawTarget);
        }
    }
}
