namespace ArkhamCombat.Combat
{
    /// <summary>
    /// What the target of a hit does. An attack asks for a <see cref="HitReaction"/>; the target's
    /// profile and the health it has left decide this.
    /// </summary>
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
        /// <summary>The dead take nothing: no damage and no reaction.</summary>
        public static readonly HitResult Ignored = default;

        public readonly bool HasLanded;
        public readonly AppliedReaction Reaction;

        private HitResult(bool hasLanded, AppliedReaction reaction)
        {
            HasLanded = hasLanded;
            Reaction = reaction;
        }

        public static HitResult Landed(AppliedReaction reaction) => new HitResult(true, reaction);

        public bool WasKilled => Reaction == AppliedReaction.Death;
    }
}
