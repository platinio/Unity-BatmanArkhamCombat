using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Knows nothing about chains, so the same asset backs any number of chain nodes. Duration is
    /// the clock for the whole system, which is what lets the demo run without a single authored
    /// animation.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Action", fileName = "Action")]
    public class ActionDefinition : ScriptableObject
    {
        [Tooltip("Seconds the action plays. Normalized time, which every window and cue is measured in, is elapsed over this.")]
        [SerializeField, Min(0.01f)] private float duration = 0.5f;

        [Tooltip("What the body does while the action plays, each at a normalized time. Pick a kind from the dropdown.")]
        [SerializeField] private List<PresentationCue> cues = new List<PresentationCue>();

        [Tooltip("Free-form labels Functions can read through the context.")]
        [SerializeField] private List<string> tags = new List<string>();

        [Tooltip("While open (normalized time), a strike that reaches the character does nothing to it. Leave start and end equal for an action that can be hit all the way through.")]
        [SerializeField] private Window invulnerableWindow = new Window(0f, 0f);

        public float Duration => duration;
        public Window InvulnerableWindow => invulnerableWindow;
        public IReadOnlyList<PresentationCue> Cues => cues;
        public IReadOnlyList<string> Tags => tags;

        public void Configure(float duration, IEnumerable<PresentationCue> cues = null, IEnumerable<string> tags = null)
        {
            this.duration = duration;
            this.cues = cues != null ? new List<PresentationCue>(cues) : new List<PresentationCue>();
            this.tags = tags != null ? new List<string>(tags) : new List<string>();
        }

        public void ConfigureInvulnerableWindow(Window invulnerableWindow) => this.invulnerableWindow = invulnerableWindow;

        public bool IsInvulnerableAt(float normalizedTime) => invulnerableWindow.Contains(normalizedTime);

        // A plain action has no windows to give. An attack gives its own, which is how the runner
        // knows an attack is playing without testing the action's type.
        internal virtual void GiveItsWindowsTo(ActionRunner runner) { }

        /// <summary>
        /// Adds one line to <paramref name="errors"/> for each authoring mistake in this asset, and
        /// answers whether it added none.
        /// </summary>
        public virtual bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;

            ReportWhenDurationIsNotPositive(errors);
            ReportEveryBrokenCue(errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(invulnerableWindow), invulnerableWindow, errors);

            bool isWellFormed = errors.Count == errorCountBefore;
            return isWellFormed;
        }

        private void ReportWhenDurationIsNotPositive(List<string> errors)
        {
            if (duration <= 0f)
            {
                errors.Add($"'{name}': duration must be positive.");
            }
        }

        protected void ReportWhenWindowEndsBeforeItStarts(string windowName, Window window, List<string> errors)
        {
            if (!window.IsValid(out string whatIsWrong))
            {
                errors.Add($"'{name}': {windowName} {whatIsWrong}.");
            }
        }

        private void ReportEveryBrokenCue(List<string> errors)
        {
            for (int i = 0; i < cues.Count; i++)
            {
                ReportWhenCueIsBroken(i, cues[i], errors);
            }
        }

        private void ReportWhenCueIsBroken(int index, PresentationCue cue, List<string> errors)
        {
            if (cue == null)
            {
                errors.Add($"'{name}': cue {index} is null.");
                return;
            }

            if (cue.Kind == null)
            {
                errors.Add($"'{name}': cue {index} has no kind.");
            }

            if (cue.FiresAt < 0f || cue.FiresAt > 1f)
            {
                errors.Add($"'{name}': cue {index} fires at {cue.FiresAt:0.00}, outside [0, 1].");
            }
        }
    }
}
