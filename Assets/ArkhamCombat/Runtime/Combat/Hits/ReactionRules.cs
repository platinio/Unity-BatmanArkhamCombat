using System;

namespace ArkhamCombat.Combat
{
    /// <summary>The attacker asks for a reaction; the receiver's profile decides the one that plays.</summary>
    public static class ReactionRules
    {
        public static AppliedReaction Resolve(HitReaction requested, HitReceiverProfile profile, float healthAfterDamage)
        {
            if (healthAfterDamage <= 0f)
            {
                return AppliedReaction.Death;
            }

            switch (requested)
            {
                case HitReaction.Flinch:
                    return profile.IsArmored ? AppliedReaction.None : AppliedReaction.Flinch;

                case HitReaction.Stagger:
                    return profile.IsArmored ? AppliedReaction.Flinch : AppliedReaction.Stagger;

                case HitReaction.Knockdown:
                    return profile.CanBeKnockedDown ? AppliedReaction.Knockdown : AppliedReaction.Stagger;

                default:
                    throw new ArgumentOutOfRangeException(nameof(requested), requested, "No rule for this reaction.");
            }
        }
    }
}
