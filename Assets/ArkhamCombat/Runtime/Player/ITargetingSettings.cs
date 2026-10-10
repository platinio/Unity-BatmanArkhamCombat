namespace ArkhamCombat.Player
{
    /// <summary>
    /// The stand-in target selection asks for these values only, so whoever holds the game's tuning
    /// can provide them without the scorer or the targeting component knowing where they are kept.
    /// </summary>
    public interface ITargetingSettings
    {
        float MaxTargetDistance { get; }
        float MaxTargetAngle { get; }
        float AngleCountingAsDoubleDistance { get; }

        /// <summary>A stick pushed less than this is at rest, and the facing stands in for it.</summary>
        float StickPushedMagnitude { get; }
    }
}
