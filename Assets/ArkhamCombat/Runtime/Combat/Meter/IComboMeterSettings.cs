using System.Collections.Generic;

namespace ArkhamCombat.Combat
{
    public interface IComboMeterSettings
    {
        /// <summary>Counts at which the tier rises.</summary>
        IReadOnlyList<int> TierThresholds { get; }

        float MeterTimeoutSeconds { get; }
    }
}
