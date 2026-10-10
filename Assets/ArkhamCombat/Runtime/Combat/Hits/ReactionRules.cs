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
    }
}
