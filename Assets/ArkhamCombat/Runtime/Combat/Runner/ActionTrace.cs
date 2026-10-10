using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Pure bookkeeping fed by the runner and the buffer's events, so the overlay only reads. An
    /// idle stretch is a record too, so a press that arrives between actions is not lost from the
    /// picture.
    /// </summary>
    public sealed class ActionTrace
    {
        public enum MarkStatus
        {
            Live,
            Consumed,
            Expired
        }

        public sealed class Mark
        {
            public Intent Intent;

            /// <summary>Normalized action time of the press, or seconds into the idle stretch.</summary>
            public float PressedAt;

            public MarkStatus Status;
        }

        public sealed class Record
        {
            public string NodeId;

            public ActionDefinition Action;

            public AttackDefinition Attack => Action as AttackDefinition;
            public bool IsIdle => Action == null;

            /// <summary>How far the action got before it ended, 1 when it ran out, or seconds idled.</summary>
            public float EndedAt;

            public bool HasEnded;
            public bool WasInterrupted;

            public bool WasWarpRefused;

            public readonly List<Mark> Marks = new List<Mark>();
        }

        private readonly List<Record> records = new List<Record>();
        private readonly int capacity;
        private readonly IntentBuffer intents;
        private readonly Dictionary<Intent, Mark> marksByIntent = new Dictionary<Intent, Mark>();

        /// <summary>Where a press arriving now would be marked: the runner updates it every tick.</summary>
        public float CurrentTime { get; set; }

        public Record Current { get; private set; }

        /// <summary>Oldest first, the current record last.</summary>
        public IReadOnlyList<Record> Records => records;

        private bool IsCurrentRecordIdle => Current != null && Current.IsIdle;

        public ActionTrace(IntentBuffer intents, int capacity = 8)
        {
            this.capacity = capacity;
            this.intents = intents;
            if (intents != null)
            {
                intents.Pushed += OnIntentPushed;
                intents.Consumed += OnIntentConsumed;
                intents.Expired += OnIntentExpired;
            }

            BeginIdle();
        }

        // The buffer outlives the character it belongs to, so a trace left listening keeps a dead
        // runner alive and marks presses on it forever.
        public void StopListening()
        {
            if (intents == null)
            {
                return;
            }

            intents.Pushed -= OnIntentPushed;
            intents.Consumed -= OnIntentConsumed;
            intents.Expired -= OnIntentExpired;
        }

        public void BeginAction(ChainNode node, ActionDefinition action)
        {
            StartRecord(new Record { NodeId = node?.Id, Action = action });
        }

        public void EndAction(float normalizedTime, bool wasInterrupted)
        {
            if (Current == null || Current.IsIdle)
            {
                return;
            }

            Current.HasEnded = true;
            Current.EndedAt = normalizedTime;
            Current.WasInterrupted = wasInterrupted;
        }

        public void BeginIdle()
        {
            if (IsCurrentRecordIdle)
            {
                return;
            }

            StartRecord(new Record());
        }

        public void MarkWarpRefused()
        {
            if (Current != null)
            {
                Current.WasWarpRefused = true;
            }
        }

        private void StartRecord(Record record)
        {
            if (IsCurrentRecordIdle)
            {
                EndIdleStretch();
            }

            records.Add(record);
            Current = record;
            CurrentTime = 0f;

            DropRecordsOverCapacity();
        }

        private void EndIdleStretch()
        {
            Current.HasEnded = true;
            Current.EndedAt = CurrentTime;
        }

        private void DropRecordsOverCapacity()
        {
            while (records.Count > capacity)
            {
                Record oldest = records[0];
                records.RemoveAt(0);
                for (int i = 0; i < oldest.Marks.Count; i++)
                {
                    marksByIntent.Remove(oldest.Marks[i].Intent);
                }
            }
        }

        private void OnIntentPushed(Intent intent)
        {
            if (Current == null)
            {
                return;
            }

            Mark mark = new Mark { Intent = intent, PressedAt = CurrentTime, Status = MarkStatus.Live };
            Current.Marks.Add(mark);
            marksByIntent[intent] = mark;
        }

        private void OnIntentConsumed(Intent intent) => SetStatus(intent, MarkStatus.Consumed);

        private void OnIntentExpired(Intent intent) => SetStatus(intent, MarkStatus.Expired);

        private void SetStatus(Intent intent, MarkStatus status)
        {
            if (marksByIntent.TryGetValue(intent, out Mark mark))
            {
                mark.Status = status;
                marksByIntent.Remove(intent);
            }
        }
    }
}
