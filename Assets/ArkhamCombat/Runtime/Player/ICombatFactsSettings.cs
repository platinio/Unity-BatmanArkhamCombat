namespace ArkhamCombat.Player
{
    /// <summary>
    /// The facts updater asks for these two values only, so whoever holds the game's tuning can
    /// provide them without the updater knowing where they are kept.
    /// </summary>
    public interface ICombatFactsSettings
    {
        float DeadAheadAngle { get; }
        float MaxLungeWhileIdle { get; }
    }
}
