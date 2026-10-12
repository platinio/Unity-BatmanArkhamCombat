namespace ArkhamCombat.Combat
{
    public enum AppliedReaction
    {
        None,
        Flinch,
        Stagger,
        Knockdown,
        Death
    }

    public static class AppliedReactionWeight
    {
        public static bool IsHeavierThan(this AppliedReaction reaction, AppliedReaction other) =>
            WeightOf(reaction) > WeightOf(other);

        // Spelled out rather than read from the enum's order, so a reaction added later has to be given its place.
        private static int WeightOf(AppliedReaction reaction)
        {
            switch (reaction)
            {
                case AppliedReaction.None:
                    return 0;
                case AppliedReaction.Flinch:
                    return 1;
                case AppliedReaction.Stagger:
                    return 2;
                case AppliedReaction.Knockdown:
                    return 3;
                case AppliedReaction.Death:
                    return 4;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(reaction), reaction, "No weight for this reaction.");
            }
        }
    }

    public readonly struct HitResult
    {
        public static readonly HitResult Ignored = default;

        public readonly bool HasLanded;
        public readonly AppliedReaction AppliedReaction;

        private HitResult(AppliedReaction appliedReaction)
        {
            HasLanded = true;
            AppliedReaction = appliedReaction;
        }

        public static HitResult Landed(AppliedReaction appliedReaction) => new HitResult(appliedReaction);

        public bool WasKilled => AppliedReaction == AppliedReaction.Death;
    }
}
