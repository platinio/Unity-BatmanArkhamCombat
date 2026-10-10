using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// What a combo meter is tuned by. The meter asks for these two values only, so whoever holds
    /// the game's tuning can provide them without the meter knowing where they are kept.
    /// </summary>
    public interface IComboMeterSettings
    {
        /// <summary>Counts at which the tier rises. The tier is the number of thresholds reached.</summary>
        IReadOnlyList<int> TierThresholds { get; }

        /// <summary>Seconds without an increment before the meter resets.</summary>
        float MeterTimeoutSeconds { get; }
    }
}
