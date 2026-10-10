using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// Knows nothing about chains, so the same asset backs any number of chain nodes, and nothing
    /// about hits; the <see cref="AttackDefinition"/> subclass adds those. Duration is the clock
    /// for the whole system, which is what lets the demo run without a single authored animation.
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

        public float Duration => duration;
        public IReadOnlyList<PresentationCue> Cues => cues;
        public IReadOnlyList<string> Tags => tags;

        public bool HasTag(string tag) => tags.Contains(tag);

        public void Configure(float duration, IEnumerable<PresentationCue> cues = null, IEnumerable<string> tags = null)
        {
            this.duration = duration;
            this.cues = cues != null ? new List<PresentationCue>(cues) : new List<PresentationCue>();
            this.tags = tags != null ? new List<string>(tags) : new List<string>();
        }

        public virtual bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;

            if (duration <= 0f)
            {
                errors.Add($"'{name}': duration must be positive.");
            }

            for (int i = 0; i < cues.Count; i++)
            {
                ValidateCue(i, cues[i], errors);
            }

            return errors.Count == errorCountBefore;
        }

        private void ValidateCue(int index, PresentationCue cue, List<string> errors)
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
