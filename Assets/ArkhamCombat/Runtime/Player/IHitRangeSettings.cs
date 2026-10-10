namespace ArkhamCombat.Player
{
    /// <summary>
    /// The stand-in hit check asks for this one value, so whoever holds the game's tuning can provide
    /// it without the check knowing where it is kept.
    /// </summary>
    public interface IHitRangeSettings
    {
        float HitRangeMargin { get; }
    }
}
