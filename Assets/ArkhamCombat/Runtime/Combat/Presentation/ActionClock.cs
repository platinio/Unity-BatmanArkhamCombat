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
        /// <summary>A time before every cue, so the cues at zero count as crossed when the action starts.</summary>
        private const float BeforeStart = float.NegativeInfinity;

        /// <summary>Cues not fired yet, earliest first.</summary>
        private readonly List<PresentationCue> schedule = new List<PresentationCue>();

        private float elapsedSeconds;
        private float speed = 1f;

        public ActionDefinition Action { get; private set; }

        public bool IsPlaying => Action != null;

        public float NormalizedTime { get; private set; }

        /// <summary>Stays true until Play or Stop.</summary>
        public bool IsFinished => IsPlaying && NormalizedTime >= 1f;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        /// <summary>Raised from <see cref="Play"/> for cues at zero and from <see cref="Tick"/> for the rest, in time order.</summary>
        public event Action<PresentationCue> CueDue;

        /// <summary>
        /// Cues at time zero fire here rather than on the first tick so a frozen clock (speed zero)
        /// fires nothing, which is what hit-stop relies on.
        /// </summary>
        public void Play(ActionDefinition action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            elapsedSeconds = 0f;
            NormalizedTime = 0f;

            ScheduleCues(action);
            FireCuesCrossedBetween(BeforeStart, NormalizedTime);
        }

        public void Stop()
        {
            Action = null;
            schedule.Clear();
            elapsedSeconds = 0f;
            NormalizedTime = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying)
            {
                return;
            }

            float previousTime = NormalizedTime;
            elapsedSeconds += Mathf.Max(0f, deltaTime) * speed;
            NormalizedTime = Mathf.Clamp01(elapsedSeconds / Action.Duration);

            FireCuesCrossedBetween(previousTime, NormalizedTime);
        }

        private void ScheduleCues(ActionDefinition action)
        {
            schedule.Clear();
            IReadOnlyList<PresentationCue> cues = action.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null)
                {
                    schedule.Add(cues[i]);
                }
            }

            SortScheduleByTime();
        }

        /// <summary>An insertion sort because it is stable: two cues at the same time keep authored order.</summary>
        private void SortScheduleByTime()
        {
            for (int i = 1; i < schedule.Count; i++)
            {
                PresentationCue cue = schedule[i];
                int j = i - 1;
                while (j >= 0 && schedule[j].FiresAt > cue.FiresAt)
                {
                    schedule[j + 1] = schedule[j];
                    j--;
                }

                schedule[j + 1] = cue;
            }
        }

        private void FireCuesCrossedBetween(float previousTime, float currentTime)
        {
            int firedCount = 0;
            while (firedCount < schedule.Count && Window.IsCrossedBetween(previousTime, currentTime, schedule[firedCount].FiresAt))
            {
                CueDue?.Invoke(schedule[firedCount]);
                firedCount++;
            }

            schedule.RemoveRange(0, firedCount);
        }
    }
}
