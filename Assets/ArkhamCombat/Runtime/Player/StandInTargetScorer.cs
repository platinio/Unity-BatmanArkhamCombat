using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// A stand-in for spec 04's scoring, behind <see cref="ITargetScorer"/> so swapping it is an
    /// installer change, and reading an <see cref="ITargetRoster"/> so it never finds targets on
    /// its own.
    /// </summary>
    public sealed class StandInTargetScorer : ITargetScorer
    {
        private const float NegligibleSqrMagnitude = 1e-4f;

        private readonly ITargetingSettings settings;
        private readonly ITargetRoster roster;

        public StandInTargetScorer(ITargetingSettings settings, ITargetRoster roster)
        {
            this.settings = settings;
            this.roster = roster;
        }

        public IActionTarget BestTarget(Vector3 position, Vector3 direction)
        {
            direction.y = 0f;

            ICombatTarget bestTarget = null;
            float lowestScore = float.MaxValue;

            IReadOnlyList<ICombatTarget> targets = roster.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                ICombatTarget candidate = targets[i];
                if (TryScore(candidate, position, direction, out float score) && score < lowestScore)
                {
                    bestTarget = candidate;
                    lowestScore = score;
                }
            }

            return bestTarget;
        }

        /// <summary>Lower is better.</summary>
        private bool TryScore(ICombatTarget candidate, Vector3 position, Vector3 direction, out float score)
        {
            score = float.MaxValue;
            if (candidate == null || !candidate.IsValid)
            {
                return false;
            }

            Vector3 toCandidate = candidate.Position - position;
            toCandidate.y = 0f;

            float distance = toCandidate.magnitude;
            if (distance > settings.MaxTargetDistance)
            {
                return false;
            }

            bool hasDirection = direction.sqrMagnitude > NegligibleSqrMagnitude;
            float angle = hasDirection ? Vector3.Angle(direction, toCandidate) : 0f;
            if (angle > settings.MaxTargetAngle)
            {
                return false;
            }

            score = distance * (1f + angle / settings.AngleCountingAsDoubleDistance);
            return true;
        }
    }
}
