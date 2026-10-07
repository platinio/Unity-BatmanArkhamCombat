using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// What the frame-data overlay draws: the last few actions, each with the presses that arrived
    /// while it played and what became of them. Pure bookkeeping fed by the runner and the buffer's
    /// events, so the overlay only reads. An idle stretch is a record too, so a press that arrives
    /// between actions is not lost from the picture.
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
            public float At;

            public MarkStatus Status;
        }

        public sealed class Record
        {
            public string NodeId;

            /// <summary>Null for an idle stretch.</summary>
            public ActionDefinition Action;

            public AttackDefinition Attack => Action as AttackDefinition;
            public bool IsIdle => Action == null;

            /// <summary>How far the action got before it ended, 1 when it ran out, or seconds idled.</summary>
            public float EndedAt;

            public bool Ended;
            public bool Interrupted;

            /// <summary>Whether the warp refused because the target was beyond the lunge limit.</summary>
            public bool WarpRefused;

            public readonly List<Mark> Marks = new List<Mark>();
        }

        private readonly List<Record> records = new List<Record>();
        private readonly int capacity;
        private readonly Dictionary<Intent, Mark> marksByIntent = new Dictionary<Intent, Mark>();

        /// <summary>Where a press arriving now would be marked: the runner updates it every tick.</summary>
        public float CurrentTime { get; set; }

        public Record Current { get; private set; }

        /// <summary>Oldest first, the current record last.</summary>
        public IReadOnlyList<Record> Records => records;

        public ActionTrace(IntentBuffer intents, int capacity = 8)
        {
            this.capacity = capacity;
            if (intents != null)
            {
                intents.Pushed += OnPushed;
                intents.Consumed += OnConsumed;
                intents.Expired += OnExpired;
            }

            BeginIdle();
        }

        public void BeginAction(ChainNode node, ActionDefinition action)
        {
            Push(new Record { NodeId = node?.Id, Action = action });
        }

        public void EndAction(float normalizedTime, bool interrupted)
        {
            if (Current == null || Current.IsIdle)
            {
                return;
            }

            Current.Ended = true;
            Current.EndedAt = normalizedTime;
            Current.Interrupted = interrupted;
        }

        public void BeginIdle()
        {
            if (Current != null && Current.IsIdle)
            {
                return;
            }

            Push(new Record());
        }

        public void MarkWarpRefused()
        {
            if (Current != null)
            {
                Current.WarpRefused = true;
            }
        }

        private void Push(Record record)
        {
            if (Current != null && Current.IsIdle)
            {
                Current.Ended = true;
                Current.EndedAt = CurrentTime;
            }

            records.Add(record);
            Current = record;
            CurrentTime = 0f;

            while (records.Count > capacity)
            {
                Record dropped = records[0];
                records.RemoveAt(0);
                for (int i = 0; i < dropped.Marks.Count; i++)
                {
                    marksByIntent.Remove(dropped.Marks[i].Intent);
                }
            }
        }

        private void OnPushed(Intent intent)
        {
            if (Current == null)
            {
                return;
            }

            Mark mark = new Mark { Intent = intent, At = CurrentTime, Status = MarkStatus.Live };
            Current.Marks.Add(mark);
            marksByIntent[intent] = mark;
        }

        private void OnConsumed(Intent intent) => SetStatus(intent, MarkStatus.Consumed);

        private void OnExpired(Intent intent) => SetStatus(intent, MarkStatus.Expired);

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
