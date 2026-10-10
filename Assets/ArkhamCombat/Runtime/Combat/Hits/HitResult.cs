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
