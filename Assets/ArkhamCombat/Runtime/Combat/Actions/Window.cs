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

        /// <summary>Half-open: a zero-length window contains nothing, so callers that need it use the crossing helpers.</summary>
        public bool Contains(float t) => start <= t && t < end;

        public bool Opened(float previousT, float t) => Crossed(previousT, t, start);

        public bool Closed(float previousT, float t) => Crossed(previousT, t, end);

        /// <summary>
        /// Whether <paramref name="edge"/> lies in (previous, t]. Exclusive at the previous end so an
        /// edge sitting exactly on the last tick's time does not fire twice. When t is behind
        /// previous the action looped, and the pass runs previous→1 then 0→t.
        /// </summary>
        public static bool Crossed(float previousT, float t, float edge)
        {
            if (t >= previousT)
            {
                return previousT < edge && edge <= t;
            }

            return (previousT < edge && edge <= 1f) || (0f <= edge && edge <= t);
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
