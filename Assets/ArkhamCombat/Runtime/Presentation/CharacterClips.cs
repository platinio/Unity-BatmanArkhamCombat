using System;
using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// Clips live here and not on the action: the capsule arena and the character arena share the
    /// action assets, and two characters play different clips for the same action.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Character Clips", fileName = "CharacterClips")]
    public sealed class CharacterClips : ScriptableObject
    {
        [Tooltip("The blend tree the character stands, walks and runs with. It plays under every action and alone between them.")]
        [SerializeField] private RuntimeAnimatorController locomotion;

        [Tooltip("One per action. An action with no entry is a hold: the body stays on locomotion while it plays.")]
        [SerializeField] private List<ClipEntry> entries = new List<ClipEntry>();

        [Tooltip("Seconds a playing clip takes to fade back to locomotion when an action with no entry starts.")]
        [SerializeField, Min(0f)] private float holdBlendOutSeconds = 0.12f;

        public RuntimeAnimatorController Locomotion => locomotion;
        public IReadOnlyList<ClipEntry> Entries => entries;
        public float HoldBlendOutSeconds => holdBlendOutSeconds;

        public void Configure(RuntimeAnimatorController locomotion, IEnumerable<ClipEntry> entries, float holdBlendOutSeconds = 0.12f)
        {
            this.locomotion = locomotion;
            this.entries = new List<ClipEntry>(entries);
            this.holdBlendOutSeconds = holdBlendOutSeconds;
        }

        /// <summary>An entry with no clip is a hold until it is fixed; <see cref="Validate"/> reports it.</summary>
        public bool TryFindEntry(ActionDefinition action, out ClipEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                bool isTheActionsClip = entries[i] != null && entries[i].Action == action && entries[i].Clip != null;
                if (isTheActionsClip)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Adds one line to <paramref name="errors"/> for each authoring mistake in this asset, and
        /// answers whether it added none.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;

            ReportWhenThereIsNoLocomotion(errors);
            ReportEveryBrokenEntry(errors);
            ReportEveryActionMappedTwice(errors);

            bool isWellFormed = errors.Count == errorCountBefore;
            return isWellFormed;
        }

        private void ReportWhenThereIsNoLocomotion(List<string> errors)
        {
            if (locomotion == null)
            {
                errors.Add($"'{name}': no locomotion controller, so the character has no pose between actions.");
            }
        }

        private void ReportEveryBrokenEntry(List<string> errors)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ReportWhenEntryIsBroken(i, entries[i], errors);
            }
        }

        private void ReportWhenEntryIsBroken(int index, ClipEntry entry, List<string> errors)
        {
            if (entry == null)
            {
                errors.Add($"'{name}': entry {index} is null.");
                return;
            }

            if (entry.Action == null)
            {
                errors.Add($"'{name}': entry {index} names no action.");
            }

            if (entry.Clip == null)
            {
                errors.Add($"'{name}': entry {index} has no clip, so its action plays as a hold.");
            }

            if (entry.End <= entry.Start)
            {
                errors.Add($"'{name}': entry {index} ends at {entry.End:0.00}, not after its start at {entry.Start:0.00}.");
            }
        }

        private void ReportEveryActionMappedTwice(List<string> errors)
        {
            HashSet<ActionDefinition> actionsSeen = new HashSet<ActionDefinition>();
            foreach (ClipEntry entry in entries)
            {
                bool isMappedAgain = entry != null && entry.Action != null && !actionsSeen.Add(entry.Action);
                if (isMappedAgain)
                {
                    errors.Add($"'{name}': '{entry.Action.name}' has two entries; the first one plays.");
                }
            }
        }
    }

    /// <summary>
    /// The part of the clip used is how a designer lines a clip up with gameplay: a punch whose contact
    /// sits at 40% of the clip, for a jab whose hit window opens at 30%, is trimmed until they meet.
    /// </summary>
    [Serializable]
    public sealed class ClipEntry
    {
        [SerializeField] private ActionDefinition action;

        [SerializeField] private AnimationClip clip;

        [Tooltip("Where in the clip the action starts, as a share of the clip's length.")]
        [SerializeField, Range(0f, 1f)] private float start = 0f;

        [Tooltip("Where in the clip the action ends, as a share of the clip's length. The part between is stretched or squeezed to the action's duration.")]
        [SerializeField, Range(0f, 1f)] private float end = 1f;

        [Tooltip("Seconds from locomotion to this clip when the action starts.")]
        [SerializeField, Min(0f)] private float blendInSeconds = 0.08f;

        [Tooltip("Seconds back to locomotion when the action ends or is stopped.")]
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.12f;

        public ClipEntry() { }

        public ClipEntry(ActionDefinition action, AnimationClip clip, float start = 0f, float end = 1f, float blendInSeconds = 0.08f, float blendOutSeconds = 0.12f)
        {
            this.action = action;
            this.clip = clip;
            this.start = start;
            this.end = end;
            this.blendInSeconds = blendInSeconds;
            this.blendOutSeconds = blendOutSeconds;
        }

        public ActionDefinition Action => action;
        public AnimationClip Clip => clip;
        public float Start => start;
        public float End => end;
        public float BlendInSeconds => blendInSeconds;
        public float BlendOutSeconds => blendOutSeconds;

        public float ClipSecondsAt(float normalizedActionTime) => Mathf.Lerp(start, end, normalizedActionTime) * clip.length;
    }
}
