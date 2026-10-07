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
        [SerializeField] private List<int> tiers = new List<int> { 3, 5, 8 };

        [Tooltip("Seconds without an increment before the meter resets. Tuned separately from the chain reset.")]
        [SerializeField, Min(0f)] private float meterTimeoutSeconds = 2.5f;

        public ComboMeterSettings() { }

        public ComboMeterSettings(float meterTimeoutSeconds, params int[] tiers)
        {
            this.meterTimeoutSeconds = meterTimeoutSeconds;
            this.tiers = new List<int>(tiers);
        }

        public IReadOnlyList<int> Tiers => tiers;
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
        private float sinceIncrement;

        public ComboMeter(ComboMeterSettings settings, ICombatEvents events)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.events = events ?? new NullCombatEvents();
        }

        public int Count { get; private set; }

        /// <summary>The number of tier thresholds the count has reached.</summary>
        public int Tier { get; private set; }

        public float SecondsSinceIncrement => sinceIncrement;

        public void Increment(ComboIncrementReason reason)
        {
            Count++;
            sinceIncrement = 0f;
            Tier = TierFor(Count);
            events.ComboChanged(Count, Tier);
        }

        /// <summary>Back to zero. Raises nothing when already at zero, so a whiff on an empty meter is silent.</summary>
        public void Reset(ComboResetReason reason)
        {
            sinceIncrement = 0f;
            if (Count == 0)
            {
                return;
            }

            Count = 0;
            Tier = 0;
            events.ComboReset(reason);
            events.ComboChanged(0, 0);
        }

        /// <summary>Runs the timeout. Only counts while there is something to lose.</summary>
        public void Tick(float deltaTime)
        {
            if (Count == 0)
            {
                return;
            }

            sinceIncrement += Mathf.Max(0f, deltaTime);
            if (sinceIncrement >= settings.MeterTimeoutSeconds)
            {
                Reset(ComboResetReason.Timeout);
            }
        }

        private int TierFor(int count)
        {
            int tier = 0;
            IReadOnlyList<int> tiers = settings.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (count >= tiers[i])
                {
                    tier++;
                }
            }

            return tier;
        }
    }
}
