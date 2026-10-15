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
            return HitResult.Landed(ReactionTo(hit));
        }

        /// <summary>What a paired attack asks its target to play the moment it starts, before any hit of it lands.</summary>
        public AppliedReaction ReactionToAPairedAttack(AttackDefinition attack) =>
            ReactionRules.ResolveForAPairedAttack(attack.Reaction, Health);

        private AppliedReaction ReactionTo(HitInfo hit) =>
            hit.IsPaired
                ? ReactionRules.ResolveForAPairedAttack(hit.RequestedReaction, Health)
                : ReactionRules.Resolve(hit.RequestedReaction, profile, Health);

        private bool IsInvulnerable => runnerOfWhoeverIsHit != null && runnerOfWhoeverIsHit.IsInvulnerable;

        private void TakeDamage(float damage)
        {
            Health = Mathf.Max(0f, Health - damage);
        }
    }
}
