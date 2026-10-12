using System;

namespace ArkhamCombat.Combat
{
    public static class ReactionRules
    {
        public static AppliedReaction Resolve(HitReaction requestedReaction, HitReceiverProfile profile, float healthAfterDamage)
        {
            if (healthAfterDamage <= 0f)
            {
                return AppliedReaction.Death;
            }

            switch (requestedReaction)
            {
                case HitReaction.Flinch:
                    return profile.IsArmored ? AppliedReaction.None : AppliedReaction.Flinch;

                case HitReaction.Stagger:
                    return profile.IsArmored ? AppliedReaction.Flinch : AppliedReaction.Stagger;

                case HitReaction.Knockdown:
                    return profile.CanBeKnockedDown ? AppliedReaction.Knockdown : AppliedReaction.Stagger;

                case HitReaction.Stun:
                    return AppliedReaction.Stun;

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedReaction), requestedReaction, "No rule for this reaction.");
            }
        }

        public static AppliedReaction ResolveForAStunnedReceiver(HitReaction requestedReaction, HitReceiverProfile profile, float healthAfterDamage)
        {
            if (healthAfterDamage <= 0f)
            {
                return AppliedReaction.Death;
            }

            switch (requestedReaction)
            {
                case HitReaction.Flinch:
                case HitReaction.Stagger:
                    return AppliedReaction.Stagger;

                case HitReaction.Knockdown:
                    return profile.CanBeKnockedDown ? AppliedReaction.Knockdown : AppliedReaction.Stagger;

                case HitReaction.Stun:
                    return AppliedReaction.Stun;

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedReaction), requestedReaction, "No rule for this reaction.");
            }
        }

        // Its target started this reaction as the attack started. A lighter one now would cut that short.
        public static AppliedReaction ResolveForAPairedAttack(HitReaction requestedReaction, float health)
        {
            if (health <= 0f)
            {
                return AppliedReaction.Death;
            }

            switch (requestedReaction)
            {
                case HitReaction.Flinch:
                    return AppliedReaction.Flinch;

                case HitReaction.Stagger:
                    return AppliedReaction.Stagger;

                case HitReaction.Knockdown:
                    return AppliedReaction.Knockdown;

                case HitReaction.Stun:
                    return AppliedReaction.Stun;

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedReaction), requestedReaction, "No rule for this reaction.");
            }
        }
    }
}
