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

            ICombatTarget best = null;
            float bestScore = float.MaxValue;

            IReadOnlyList<ICombatTarget> targets = roster.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                ICombatTarget candidate = targets[i];
                if (candidate == null || !candidate.IsValid)
                {
                    continue;
                }

                Vector3 toCandidate = candidate.Position - origin;
                toCandidate.y = 0f;

                float distance = toCandidate.magnitude;
                if (distance > config.TargetMaxDistance)
                {
                    continue;
                }

                float angle = preferredDirection.sqrMagnitude > 1e-4f ? Vector3.Angle(preferredDirection, toCandidate) : 0f;
                if (angle > config.TargetMaxAngle)
                {
                    continue;
                }

                float score = distance * (1f + angle / 90f);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
