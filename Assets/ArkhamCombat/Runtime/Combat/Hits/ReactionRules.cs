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

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedReaction), requestedReaction, "No rule for this reaction.");
            }
        }

        /// <summary>
        /// The target of a paired attack reacts as the attack starts, with the reaction that was
        /// asked for: neither armor nor sure footing changes it. Only death does.
        /// </summary>
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

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedReaction), requestedReaction, "No rule for this reaction.");
            }
        }
    }
}
