namespace ArkhamCombat.Player
{
    /// <summary>
    /// The attacking state asks for this one value, so whoever holds the game's tuning can provide
    /// it without the state knowing where it is kept.
    /// </summary>
    public interface IFacingSettings
    {
        float FaceTargetSmoothTime { get; }
    }
}
