using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Movement;
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
    /// Nearest dummy roughly where the stick points, or ahead when the stick is idle. A stand-in
    /// for spec 04's scored pick over the director's roster; the runner only ever sees an
    /// <see cref="IActionTarget"/>, so swapping it changes nothing downstream.
    /// </summary>
    public sealed class StandInTargetPicker
    {
        private readonly CombatConfig config;
        private readonly IMovementFrame frame;
        private readonly List<CombatDummy> candidates = new List<CombatDummy>();
        private readonly TransformTarget current = new TransformTarget();

        public StandInTargetPicker(CombatConfig config, IMovementFrame frame)
        {
            this.config = config;
            this.frame = frame;
        }

        /// <summary>Re-reads the scene. Call once at start and whenever dummies are spawned.</summary>
        public void Refresh()
        {
            candidates.Clear();
            candidates.AddRange(Object.FindObjectsByType<CombatDummy>(FindObjectsInactive.Exclude));
        }

        public TransformTarget Pick(Transform character, Vector2 moveInput)
        {
            Vector3 origin = character.position;
            Vector3 preferred = moveInput.sqrMagnitude > 0.01f
                ? frame.Frame * new Vector3(moveInput.x, 0f, moveInput.y)
                : character.forward;
            preferred.y = 0f;

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

                float angle = preferred.sqrMagnitude > 1e-4f ? Vector3.Angle(preferred, toCandidate) : 0f;
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
