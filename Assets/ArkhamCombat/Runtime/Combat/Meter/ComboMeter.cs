using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    public enum ComboResetReason
    {
        PlayerHit,
        Whiff,
        Timeout
    }

    /// <summary>
    /// Not the chain: a chain can continue after a whiff and the meter will not, and the meter
    /// keeps counting across a counter and an evade that the chain never sees.
    /// </summary>
    public sealed class ComboMeter
    {
        private readonly IComboMeterSettings settings;
        private float secondsSinceIncrement;

        public ComboMeter(IComboMeterSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>The new count and tier, after every increment and after a reset.</summary>
        public event Action<int, int> ComboChanged;

        /// <summary>Why a combo was lost, raised just before the change to zero.</summary>
        public event Action<ComboResetReason> ComboReset;

        public int Count { get; private set; }

        /// <summary>The number of tier thresholds the count has reached.</summary>
        public int Tier { get; private set; }

        public float SecondsSinceIncrement => secondsSinceIncrement;

        private bool IsEmpty => Count == 0;

        private bool HasTimedOut => secondsSinceIncrement >= settings.MeterTimeoutSeconds;

        public void Increment()
        {
            Count++;
            secondsSinceIncrement = 0f;
            Tier = TierReachedAt(Count);
            ComboChanged?.Invoke(Count, Tier);
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
            ComboReset?.Invoke(reason);
            ComboChanged?.Invoke(Count, Tier);
        }

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
