using System;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    public sealed class HitReceiver
    {
        private readonly HitReceiverProfile profile;
        private readonly ActionRunner runnerOfWhoeverIsHit;

        /// <summary>Without a runner the receiver plays no actions, so it is never invulnerable.</summary>
        public HitReceiver(HitReceiverProfile profile, ActionRunner runnerOfWhoeverIsHit = null)
        {
            this.profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            this.runnerOfWhoeverIsHit = runnerOfWhoeverIsHit;
            Health = profile.MaxHealth;
        }

        public float Health { get; private set; }

        public bool IsAlive => Health > 0f;

        public HitResult Receive(HitInfo hit)
        {
            if (!IsAlive)
            {
                return HitResult.Ignored;
            }

            if (IsInvulnerable)
            {
                return HitResult.Evaded;
            }

            TakeDamage(hit.Damage);
            AppliedReaction appliedReaction = ReactionRules.Resolve(hit.RequestedReaction, profile, Health);
            return HitResult.Landed(appliedReaction);
        }

        private bool IsInvulnerable => runnerOfWhoeverIsHit != null && runnerOfWhoeverIsHit.IsInvulnerable;

        private void TakeDamage(float damage)
        {
            Health = Mathf.Max(0f, Health - damage);
        }
    }
}
