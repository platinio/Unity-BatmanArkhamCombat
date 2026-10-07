using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// The duration clock with the cue schedule on it: advances normalized time by speed and reports
    /// each cue once when its time is crossed. Pure so cue timing is tested without a tween library;
    /// a driver composes it and turns <see cref="CueDue"/> into tweens.
    /// </summary>
    public sealed class ActionClock
    {
        private readonly List<PresentationCue> schedule = new List<PresentationCue>();
        private float elapsed;
        private float previousT;
        private float speed = 1f;

        public ActionDefinition Action { get; private set; }

        public bool IsPlaying => Action != null;

        public float NormalizedTime { get; private set; }

        /// <summary>True once the clock has reached the end of the action. Stays true until Play or Stop.</summary>
        public bool Finished => IsPlaying && NormalizedTime >= 1f;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        /// <summary>Raised from <see cref="Play"/> for cues at zero and from <see cref="Tick"/> for the rest, in time order.</summary>
        public event Action<PresentationCue> CueDue;

        /// <summary>
        /// Starts the action. Cues at time zero fire here rather than on the first tick so a frozen
        /// clock (speed zero) fires nothing, which is what hit-stop relies on.
        /// </summary>
        public void Play(ActionDefinition action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            elapsed = 0f;
            NormalizedTime = 0f;
            previousT = 0f;

            schedule.Clear();
            IReadOnlyList<PresentationCue> cues = action.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null)
                {
                    schedule.Add(cues[i]);
                }
            }

            // Insertion sort by time: stable, so two cues at the same time keep authored order.
            for (int i = 1; i < schedule.Count; i++)
            {
                PresentationCue cue = schedule[i];
                int j = i - 1;
                while (j >= 0 && schedule[j].At > cue.At)
                {
                    schedule[j + 1] = schedule[j];
                    j--;
                }

                schedule[j + 1] = cue;
            }

            int fired = 0;
            while (fired < schedule.Count && schedule[fired].At <= 0f)
            {
                CueDue?.Invoke(schedule[fired]);
                fired++;
            }

            schedule.RemoveRange(0, fired);
        }

        public void Stop()
        {
            Action = null;
            schedule.Clear();
            elapsed = 0f;
            NormalizedTime = 0f;
            previousT = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying)
            {
                return;
            }

            elapsed += Mathf.Max(0f, deltaTime) * speed;
            previousT = NormalizedTime;
            NormalizedTime = Mathf.Clamp01(elapsed / Action.Duration);

            int fired = 0;
            while (fired < schedule.Count && Window.Crossed(previousT, NormalizedTime, schedule[fired].At))
            {
                CueDue?.Invoke(schedule[fired]);
                fired++;
            }

            if (fired > 0)
            {
                schedule.RemoveRange(0, fired);
            }
        }
    }
}
