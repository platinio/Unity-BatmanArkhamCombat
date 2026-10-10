using System;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>One character's health, and what each hit does to it.</summary>
    public sealed class HitReceiver
    {
        private readonly HitReceiverProfile profile;

        public HitReceiver(HitReceiverProfile profile)
        {
            this.profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
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

            TakeDamage(hit.Attack.Damage);
            return HitResult.Landed(ReactionRules.Resolve(hit.Attack.Reaction, profile, Health));
        }

        private void TakeDamage(float damage)
        {
            Health = Mathf.Max(0f, Health - damage);
        }
    }
}
