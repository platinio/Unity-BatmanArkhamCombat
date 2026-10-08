using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Nearest roster target roughly along the preferred direction. A stand-in for spec 04's scored
    /// pick, behind <see cref="ITargetPicker"/> so swapping it is an installer change, and reading
    /// an <see cref="ITargetRoster"/> so it never finds targets on its own.
    /// </summary>
    public sealed class StandInTargetPicker : ITargetPicker
    {
        private const float NegligibleSqrMagnitude = 1e-4f;

        // A target this many degrees off the preferred direction scores as if it were twice as far.
        private const float AngleThatDoublesTheScore = 90f;

        private readonly CombatConfig config;
        private readonly ITargetRoster roster;

        public StandInTargetPicker(CombatConfig config, ITargetRoster roster)
        {
            this.config = config;
            this.roster = roster;
        }

        public IActionTarget Pick(Vector3 origin, Vector3 preferredDirection)
        {
            preferredDirection.y = 0f;

            ICombatTarget bestTarget = null;
            float lowestScore = float.MaxValue;

            IReadOnlyList<ICombatTarget> targets = roster.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                ICombatTarget candidate = targets[i];
                if (TryScore(candidate, origin, preferredDirection, out float score) && score < lowestScore)
                {
                    bestTarget = candidate;
                    lowestScore = score;
                }
            }

            return bestTarget;
        }

        /// <summary>Lower is better. False when the candidate is gone, too far, or too far off the preferred direction.</summary>
        private bool TryScore(ICombatTarget candidate, Vector3 origin, Vector3 preferredDirection, out float score)
        {
            score = float.MaxValue;
            if (candidate == null || !candidate.IsValid)
            {
                return false;
            }

            Vector3 toCandidate = candidate.Position - origin;
            toCandidate.y = 0f;

            float distance = toCandidate.magnitude;
            if (distance > config.MaxTargetDistance)
            {
                return false;
            }

            bool hasPreferredDirection = preferredDirection.sqrMagnitude > NegligibleSqrMagnitude;
            float angle = hasPreferredDirection ? Vector3.Angle(preferredDirection, toCandidate) : 0f;
            if (angle > config.MaxTargetAngle)
            {
                return false;
            }

            score = distance * (1f + angle / AngleThatDoublesTheScore);
            return true;
        }
    }
}
