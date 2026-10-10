using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Shell
{
    /// <summary>
    /// The frame-data overlay from spec 01: one bar per recent action with its four windows as
    /// coloured bands, cue marks, a playhead, and a tick for every press with what became of it.
    /// Screen-space IMGUI on purpose; it is a tuning tool, not HUD. Everything it shows is read from
    /// the trace of the runner on the player's <see cref="CombatActions"/>, so it never influences
    /// what it measures. A player without one shows nothing.
    /// </summary>
    public sealed class FrameDataOverlay : MonoBehaviour
    {
        private const float LabelWidth = 900f;
        private const float HeaderHeight = 20f;
        private const float HeaderSpacing = 24f;
        private const float CaptionHeight = 18f;
        private const float RowHeight = 40f;

        private const float BarWidth = 420f;
        private const float BarHeight = 12f;
        private const int WindowBandCount = 4;
        private const float MinimumBandWidth = 1f;

        private const float TickWidth = 2f;
        private const float CueTickOverhang = 2f;
        private const float PlayheadOverhang = 3f;
        private const float PressTickHeight = 6f;
        private const float PressLabelWidth = 40f;
        private const float PressLabelHeight = 16f;
        private const int PressLabelKindLetters = 2;

        private const float ActionLength = 1f;
        private const float IdleSecondsShown = 2f;

        private static readonly Color WarpWindowColour = new Color(1f, 0.85f, 0.2f);
        private static readonly Color HitWindowColour = new Color(1f, 0.25f, 0.2f);
        private static readonly Color ComboWindowColour = new Color(0.3f, 1f, 0.4f);
        private static readonly Color EvadeWindowColour = new Color(0.4f, 0.6f, 1f);
        private static readonly Color CueColour = new Color(1f, 0.4f, 1f);
        private static readonly Color BarBackground = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        private static readonly Color PlayheadColour = Color.white;
        private static readonly Color EarlyEndColour = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color ConsumedPressColour = Color.green;
        private static readonly Color ExpiredPressColour = Color.red;
        private static readonly Color WaitingPressColour = Color.yellow;

        [Tooltip("Top-left corner of the strip.")]
        [SerializeField] private Vector2 origin = new Vector2(12f, 80f);

        private CharacterBrain player;
        private ActionRunner runner;
        private ComboTracker comboTracker;

        private int ComboCount => comboTracker != null ? comboTracker.Count : 0;

        private int ComboTier => comboTracker != null ? comboTracker.Tier : 0;

        [Inject]
        private void Construct(CharacterBrain player) => this.player = player;

        // The runner and the tracker live in the player's own context, which the scene container cannot
        // see into, so they are taken from the player once that context has built them.
        private void Start()
        {
            if (player == null)
            {
                Debug.LogError(
                    $"[{nameof(FrameDataOverlay)}] No player was injected. The scene needs a SceneContext and the player installer.", this);
                return;
            }

            if (player.TryGetComponent(out CombatActions combatActions))
            {
                runner = combatActions.Runner;
            }

            player.TryGetComponent(out comboTracker);
        }

        private void OnGUI()
        {
            // Repaint only: nothing here needs a Layout pass, and the strings are built per event.
            if (runner == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float y = origin.y;
            GUI.Label(new Rect(origin.x, y, LabelWidth, HeaderHeight), HeaderText());
            y += HeaderSpacing;

            DrawRecordsNewestFirst(origin.x, y);
        }

        private string HeaderText()
        {
            string node = runner.CurrentNode != null ? runner.CurrentNode.Id : "-";
            return $"Combo {ComboCount} (tier {ComboTier})   node {node}   t {runner.NormalizedTime:0.00}{OpenWindowsText()}";
        }

        private string OpenWindowsText()
        {
            return (runner.IsHitWindowOpen ? " HIT" : string.Empty)
                   + (runner.IsComboWindowOpen ? " COMBO" : string.Empty)
                   + (runner.IsWarpWindowOpen ? " WARP" : string.Empty)
                   + (runner.WasWarpRefused ? " warp-refused" : string.Empty);
        }

        private void DrawRecordsNewestFirst(float x, float y)
        {
            IReadOnlyList<ActionTrace.Record> records = runner.Trace.Records;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                DrawRecord(records[i], x, y);
                y += RowHeight;
            }
        }

        private void DrawRecord(ActionTrace.Record record, float x, float y)
        {
            GUI.Label(new Rect(x, y, LabelWidth, CaptionHeight), CaptionText(record));

            Rect bar = new Rect(x, y + CaptionHeight, BarWidth, BarHeight);
            Fill(bar, BarBackground);

            if (record.Attack != null)
            {
                DrawWindowBands(bar, record.Attack);
            }

            if (record.Action != null)
            {
                DrawCueTicks(bar, record.Action);
            }

            if (IsPlayingNow(record))
            {
                DrawTickAcrossBar(bar, runner.NormalizedTime, PlayheadOverhang, PlayheadColour);
            }
            else if (HasEndedEarly(record))
            {
                DrawTickAcrossBar(bar, record.EndedAt, PlayheadOverhang, EarlyEndColour);
            }

            DrawPresses(bar, record);
        }

        private string CaptionText(ActionTrace.Record record)
        {
            if (record.IsIdle)
            {
                return $"idle {IdleSeconds(record):0.00}s";
            }

            string warp = record.WasWarpRefused ? "  warp refused" : string.Empty;
            string node = record.NodeId ?? "-";

            return $"{node}  {record.Action.name}  {record.Action.Duration:0.00}s  {EndingText(record)}{warp}";
        }

        private static string EndingText(ActionTrace.Record record)
        {
            if (!record.HasEnded)
            {
                return "playing";
            }

            if (record.WasInterrupted)
            {
                return $"interrupted at {record.EndedAt:0.00}";
            }

            return record.EndedAt < ActionLength ? $"ended early at {record.EndedAt:0.00}" : "ended";
        }

        private bool IsPlayingNow(ActionTrace.Record record)
        {
            return ReferenceEquals(record, runner.Trace.Current) && !record.IsIdle && runner.IsPlaying;
        }

        private static bool HasEndedEarly(ActionTrace.Record record)
        {
            return record.HasEnded && !record.IsIdle && record.EndedAt < ActionLength;
        }

        private float IdleSeconds(ActionTrace.Record record)
        {
            return record.HasEnded ? record.EndedAt : runner.Trace.CurrentTime;
        }

        /// <summary>An action's presses are in normalized time; an idle stretch's are in seconds.</summary>
        private float TimeAcrossBar(ActionTrace.Record record)
        {
            return record.IsIdle ? Mathf.Max(IdleSecondsShown, IdleSeconds(record)) : ActionLength;
        }

        private static void DrawWindowBands(Rect bar, AttackDefinition attack)
        {
            DrawWindowBand(bar, 0, attack.WarpWindow, WarpWindowColour);
            DrawWindowBand(bar, 1, attack.HitWindow, HitWindowColour);
            DrawWindowBand(bar, 2, attack.ComboWindow, ComboWindowColour);
            DrawWindowBand(bar, 3, attack.EvadeWindow, EvadeWindowColour);
        }

        /// <summary>An empty window still gets a sliver, so it is visibly there.</summary>
        private static void DrawWindowBand(Rect bar, int bandIndex, Window window, Color colour)
        {
            float bandHeight = bar.height / WindowBandCount;
            float width = Mathf.Max(MinimumBandWidth, window.Length * bar.width);
            Fill(new Rect(bar.x + window.Start * bar.width, bar.y + bandIndex * bandHeight, width, bandHeight), colour);
        }

        private static void DrawCueTicks(Rect bar, ActionDefinition action)
        {
            IReadOnlyList<PresentationCue> cues = action.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null)
                {
                    DrawTickAcrossBar(bar, cues[i].FiresAt, CueTickOverhang, CueColour);
                }
            }
        }

        private void DrawPresses(Rect bar, ActionTrace.Record record)
        {
            float timeAcrossBar = TimeAcrossBar(record);
            for (int i = 0; i < record.Marks.Count; i++)
            {
                ActionTrace.Mark press = record.Marks[i];
                float shareOfBar = Mathf.Clamp01(press.PressedAt / timeAcrossBar);
                DrawPress(bar, shareOfBar, press);
            }
        }

        private static void DrawPress(Rect bar, float shareOfBar, ActionTrace.Mark press)
        {
            Color colour = PressColour(press.Status);
            float tickX = bar.x + shareOfBar * BarWidth;

            Fill(new Rect(tickX - TickWidth / 2f, bar.y - PressTickHeight, TickWidth, PressTickHeight), colour);

            GUI.color = colour;
            GUI.Label(new Rect(tickX + TickWidth, bar.y - PressLabelHeight, PressLabelWidth, PressLabelHeight), PressLabel(press));
            GUI.color = Color.white;
        }

        private static Color PressColour(ActionTrace.MarkStatus status)
        {
            switch (status)
            {
                case ActionTrace.MarkStatus.Consumed: return ConsumedPressColour;
                case ActionTrace.MarkStatus.Expired: return ExpiredPressColour;
                default: return WaitingPressColour;
            }
        }

        private static string PressLabel(ActionTrace.Mark press)
        {
            string kindName = press.Intent.Kind.name;
            string kind = kindName.Substring(0, Mathf.Min(PressLabelKindLetters, kindName.Length));
            switch (press.Status)
            {
                case ActionTrace.MarkStatus.Consumed: return kind + "+";
                case ActionTrace.MarkStatus.Expired: return kind + "x";
                default: return kind + "?";
            }
        }

        private static void DrawTickAcrossBar(Rect bar, float shareOfBar, float overhang, Color colour)
        {
            float tickX = bar.x + shareOfBar * BarWidth;
            Fill(new Rect(tickX - TickWidth / 2f, bar.y - overhang, TickWidth, bar.height + overhang * 2f), colour);
        }

        private static void Fill(Rect rect, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
