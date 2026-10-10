using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The meter asks for these two values only, so whoever holds the game's tuning can provide
    /// them without the meter knowing where they are kept.
    /// </summary>
    public interface IComboMeterSettings
    {
        /// <summary>Counts at which the tier rises.</summary>
        IReadOnlyList<int> TierThresholds { get; }

        float MeterTimeoutSeconds { get; }
    }
}
