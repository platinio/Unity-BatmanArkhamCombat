using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>A scene object as an action target. Reused frame to frame so picking allocates nothing.</summary>
    public sealed class TransformTarget : IActionTarget
    {
        public Transform Transform { get; private set; }
        public CombatDummy Dummy { get; private set; }

        public bool IsValid => Transform != null && Transform.gameObject.activeInHierarchy;
        public Vector3 Position => Transform.position;

        public void Set(CombatDummy dummy)
        {
            Dummy = dummy;
            Transform = dummy != null ? dummy.transform : null;
        }
    }

    /// <summary>
    /// Nearest dummy roughly along the preferred direction. A stand-in for spec 04's scored pick
    /// over the director's roster, behind <see cref="ITargetPicker"/> so swapping it is an installer
    /// change. Reads the scene on first use; <see cref="Refresh"/> re-reads it after spawning.
    /// </summary>
    public sealed class StandInTargetPicker : ITargetPicker
    {
        private readonly CombatConfig config;
        private readonly List<CombatDummy> candidates = new List<CombatDummy>();
        private readonly TransformTarget current = new TransformTarget();
        private bool scanned;

        public StandInTargetPicker(CombatConfig config)
        {
            this.config = config;
        }

        /// <summary>Re-reads the scene's dummies. Called on first pick, and by whoever spawns more.</summary>
        public void Refresh()
        {
            candidates.Clear();
            candidates.AddRange(Object.FindObjectsByType<CombatDummy>(FindObjectsInactive.Exclude));
            scanned = true;
        }

        public IActionTarget Pick(Vector3 origin, Vector3 preferredDirection)
        {
            if (!scanned)
            {
                Refresh();
            }

            preferredDirection.y = 0f;

            CombatDummy best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                CombatDummy candidate = candidates[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 toCandidate = candidate.transform.position - origin;
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

            current.Set(best);
            return current;
        }
    }
}
