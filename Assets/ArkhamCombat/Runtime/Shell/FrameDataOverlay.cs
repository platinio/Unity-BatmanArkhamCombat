using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Shell
{
    /// <summary>
    /// The frame-data overlay from spec 01: one bar per recent action with its four windows as
    /// coloured bands, cue marks, a playhead, and a tick for every press with what became of it.
    /// Screen-space IMGUI on purpose; it is a tuning tool, not HUD. Everything it shows is read from
    /// the runner's trace, so it never influences what it measures.
    /// </summary>
    public sealed class FrameDataOverlay : MonoBehaviour
    {
        private const float BarWidth = 420f;
        private const float BarHeight = 12f;
        private const float RowHeight = 40f;
        private const float IdleSecondsShown = 2f;

        private static readonly Color WarpColour = new Color(1f, 0.85f, 0.2f);
        private static readonly Color ActiveColour = new Color(1f, 0.25f, 0.2f);
        private static readonly Color CancelAttackColour = new Color(0.3f, 1f, 0.4f);
        private static readonly Color CancelEvadeColour = new Color(0.4f, 0.6f, 1f);
        private static readonly Color CueColour = new Color(1f, 0.4f, 1f);
        private static readonly Color BarBackground = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        [Tooltip("Top-left corner of the strip.")]
        [SerializeField] private Vector2 origin = new Vector2(12f, 80f);

        [Inject] private ActionRunner runner;
        [Inject] private ComboMeter meter;

        private void OnGUI()
        {
            if (runner == null)
            {
                return;
            }

            float y = origin.y;
            GUI.Label(new Rect(origin.x, y, 900f, 20f), Header());
            y += 24f;

            IReadOnlyList<ActionTrace.Record> records = runner.Trace.Records;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                DrawRecord(records[i], ReferenceEquals(records[i], runner.Trace.Current), origin.x, y);
                y += RowHeight;
            }
        }

        private string Header()
        {
            string node = runner.CurrentNode != null ? runner.CurrentNode.Id : "-";
            string flags = (runner.HitArmed ? " ARMED" : string.Empty)
                           + (runner.CancelWindowOpen ? " CANCEL" : string.Empty)
                           + (runner.WarpOpen ? " WARP" : string.Empty)
                           + (runner.WarpRefused ? " warp-refused" : string.Empty);

            return $"Combo {meter.Count} (tier {meter.Tier})   node {node}   t {runner.NormalizedTime:0.00}{flags}";
        }

        private void DrawRecord(ActionTrace.Record record, bool isCurrent, float x, float y)
        {
            GUI.Label(new Rect(x, y, 900f, 18f), Caption(record, isCurrent));
            Rect bar = new Rect(x, y + 18f, BarWidth, BarHeight);

            Fill(bar, BarBackground);

            AttackDefinition attack = record.Attack;
            if (attack != null)
            {
                float band = BarHeight / 4f;
                Band(bar, 0f, band, attack.Warp, WarpColour);
                Band(bar, band, band, attack.Active, ActiveColour);
                Band(bar, band * 2f, band, attack.CancelAttack, CancelAttackColour);
                Band(bar, band * 3f, band, attack.CancelEvade, CancelEvadeColour);
            }

            if (record.Action != null)
            {
                IReadOnlyList<PresentationCue> cues = record.Action.Cues;
                for (int i = 0; i < cues.Count; i++)
                {
                    if (cues[i] != null)
                    {
                        Fill(new Rect(bar.x + cues[i].At * BarWidth - 1f, bar.y - 2f, 2f, BarHeight + 4f), CueColour);
                    }
                }
            }

            float length = record.IsIdle ? Mathf.Max(IdleSecondsShown, record.Ended ? record.EndedAt : runner.Trace.CurrentTime) : 1f;

            if (isCurrent && !record.IsIdle && runner.IsPlaying)
            {
                Fill(new Rect(bar.x + runner.NormalizedTime * BarWidth - 1f, bar.y - 3f, 2f, BarHeight + 6f), Color.white);
            }
            else if (record.Ended && !record.IsIdle && record.EndedAt < 1f)
            {
                Fill(new Rect(bar.x + record.EndedAt * BarWidth - 1f, bar.y - 3f, 2f, BarHeight + 6f), new Color(1f, 1f, 1f, 0.5f));
            }

            for (int i = 0; i < record.Marks.Count; i++)
            {
                ActionTrace.Mark mark = record.Marks[i];
                float at = Mathf.Clamp01(mark.At / length);
                Color colour = mark.Status == ActionTrace.MarkStatus.Consumed ? Color.green
                    : mark.Status == ActionTrace.MarkStatus.Expired ? Color.red
                    : Color.yellow;

                Fill(new Rect(bar.x + at * BarWidth - 1f, bar.y - 6f, 2f, 6f), colour);
                GUI.color = colour;
                GUI.Label(new Rect(bar.x + at * BarWidth + 2f, bar.y - 16f, 40f, 16f), MarkLabel(mark));
                GUI.color = Color.white;
            }
        }

        private string Caption(ActionTrace.Record record, bool isCurrent)
        {
            if (record.IsIdle)
            {
                float seconds = record.Ended ? record.EndedAt : runner.Trace.CurrentTime;
                return $"idle {seconds:0.00}s";
            }

            string status = !record.Ended ? "playing"
                : record.Interrupted ? $"interrupted at {record.EndedAt:0.00}"
                : record.EndedAt < 1f ? $"cancelled at {record.EndedAt:0.00}"
                : "ended";
            string warp = record.WarpRefused ? "  warp refused" : string.Empty;
            string node = record.NodeId ?? "-";

            return $"{node}  {record.Action.name}  {record.Action.Duration:0.00}s  {status}{warp}";
        }

        private static string MarkLabel(ActionTrace.Mark mark)
        {
            string kind = mark.Intent.Kind.ToString().Substring(0, 2);
            switch (mark.Status)
            {
                case ActionTrace.MarkStatus.Consumed: return kind + "+";
                case ActionTrace.MarkStatus.Expired: return kind + "x";
                default: return kind + "?";
            }
        }

        private static void Band(Rect bar, float offsetY, float height, Window window, Color colour)
        {
            float width = Mathf.Max(1f, window.Length * bar.width);
            Fill(new Rect(bar.x + window.Start * bar.width, bar.y + offsetY, width, height), colour);
        }

        private static void Fill(Rect rect, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
