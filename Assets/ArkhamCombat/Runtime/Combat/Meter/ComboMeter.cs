using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>Tunables for the meter, held on a config asset.</summary>
    [Serializable]
    public sealed class ComboMeterSettings
    {
        [Tooltip("Counts at which the tier rises. The tier is the number of thresholds reached.")]
        [SerializeField] private List<int> tierThresholds = new List<int> { 3, 5, 8 };

        [Tooltip("Seconds without an increment before the meter resets. Tuned separately from the chain reset.")]
        [SerializeField, Min(0f)] private float meterTimeoutSeconds = 2.5f;

        public ComboMeterSettings() { }

        public ComboMeterSettings(float meterTimeoutSeconds, params int[] tierThresholds)
        {
            this.meterTimeoutSeconds = meterTimeoutSeconds;
            this.tierThresholds = new List<int>(tierThresholds);
        }

        public IReadOnlyList<int> TierThresholds => tierThresholds;
        public float MeterTimeoutSeconds => meterTimeoutSeconds;
    }

    /// <summary>
    /// The combo count beside the chain, with its own increment and reset rules and its own timeout.
    /// Not the chain: a chain can continue after a whiff and the meter will not, and the meter keeps
    /// counting across a counter and an evade that the chain never sees.
    /// </summary>
    public sealed class ComboMeter
    {
        private readonly ComboMeterSettings settings;
        private readonly ICombatEvents events;
        private float secondsSinceIncrement;

        public ComboMeter(ComboMeterSettings settings, ICombatEvents events)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.events = events ?? new NullCombatEvents();
        }

        public int Count { get; private set; }

        /// <summary>The number of tier thresholds the count has reached.</summary>
        public int Tier { get; private set; }

        public float SecondsSinceIncrement => secondsSinceIncrement;

        private bool IsEmpty => Count == 0;

        private bool HasTimedOut => secondsSinceIncrement >= settings.MeterTimeoutSeconds;

        public void Increment(ComboIncrementReason reason)
        {
            Count++;
            secondsSinceIncrement = 0f;
            Tier = TierReachedAt(Count);
            events.ComboChanged(Count, Tier);
        }

        /// <summary>Raises nothing when already at zero, so a whiff on an empty meter is silent.</summary>
        public void Reset(ComboResetReason reason)
        {
            secondsSinceIncrement = 0f;
            if (IsEmpty)
            {
                return;
            }

            Count = 0;
            Tier = 0;
            events.ComboReset(reason);
            events.ComboChanged(0, 0);
        }

        /// <summary>The timeout only runs while there is a combo to lose.</summary>
        public void Tick(float deltaTime)
        {
            if (IsEmpty)
            {
                return;
            }

            secondsSinceIncrement += Mathf.Max(0f, deltaTime);
            if (HasTimedOut)
            {
                Reset(ComboResetReason.Timeout);
            }
        }

        private int TierReachedAt(int count)
        {
            int tier = 0;
            IReadOnlyList<int> thresholds = settings.TierThresholds;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (count >= thresholds[i])
                {
                    tier++;
                }
            }

            return tier;
        }
    }
}
