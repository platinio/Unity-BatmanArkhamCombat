using System;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A span of normalized action time. The runner does not ask "is the window open" each tick; it
    /// asks whether the window's start or end was crossed between the last tick and this one, so an
    /// open and a close fire exactly once each however coarse the frame rate, and a zero-length
    /// window still fires both.
    /// </summary>
    [Serializable]
    public struct Window
    {
        [SerializeField, Range(0f, 1f)] private float start;
        [SerializeField, Range(0f, 1f)] private float end;

        public Window(float start, float end)
        {
            this.start = start;
            this.end = end;
        }

        public float Start => start;
        public float End => end;
        public float Length => Mathf.Max(0f, end - start);
        public bool IsZeroLength => end <= start;

        /// <summary>Half-open: a zero-length window contains nothing, so callers that need it use the crossing checks.</summary>
        public bool Contains(float time) => start <= time && time < end;

        public bool IsOpen(float previousTime, float currentTime) => IsCrossedBetween(previousTime, currentTime, start);

        public bool IsClosed(float previousTime, float currentTime) => IsCrossedBetween(previousTime, currentTime, end);

        /// <summary>
        /// Whether <paramref name="moment"/> lies in (previousTime, currentTime]. Exclusive at the
        /// previous end so a moment sitting exactly on the last tick's time does not fire twice. When
        /// the current time is behind the previous one the action looped, and the pass runs
        /// previousTime to 1, then 0 to currentTime.
        /// </summary>
        public static bool IsCrossedBetween(float previousTime, float currentTime, float moment)
        {
            bool hasLooped = currentTime < previousTime;
            if (!hasLooped)
            {
                return previousTime < moment && moment <= currentTime;
            }

            bool isCrossedBeforeLoop = previousTime < moment && moment <= 1f;
            bool isCrossedAfterLoop = 0f <= moment && moment <= currentTime;
            return isCrossedBeforeLoop || isCrossedAfterLoop;
        }

        public bool IsValid(out string error)
        {
            if (end < start)
            {
                error = $"window ends ({end:0.00}) before it starts ({start:0.00})";
                return false;
            }

            error = null;
            return true;
        }

        public override string ToString() => $"[{start:0.00}, {end:0.00}]";
    }
}
