using System;
using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    [CreateAssetMenu(menuName = "ArkhamCombat/Character Clips", fileName = "CharacterClips")]
    public sealed class CharacterClips : ScriptableObject
    {
        [Tooltip("The blend tree the character stands, walks and runs with. It plays under every action and alone between them.")]
        [SerializeField] private RuntimeAnimatorController locomotion;

        [Tooltip("One per action. An action with no entry is a hold: the body stays on locomotion while it plays.")]
        [SerializeField] private List<ClipEntry> entries = new List<ClipEntry>();

        [Tooltip("Seconds a playing clip takes to fade back to locomotion when an action with no entry starts.")]
        [SerializeField, Min(0f)] private float holdBlendOutSeconds = ClipEntry.DefaultBlendOutSeconds;

        public RuntimeAnimatorController Locomotion => locomotion;
        public IReadOnlyList<ClipEntry> Entries => entries;
        public float HoldBlendOutSeconds => holdBlendOutSeconds;

        public void Configure(RuntimeAnimatorController locomotion, IEnumerable<ClipEntry> entries, float holdBlendOutSeconds = ClipEntry.DefaultBlendOutSeconds)
        {
            this.locomotion = locomotion;
            this.entries = new List<ClipEntry>(entries);
            this.holdBlendOutSeconds = holdBlendOutSeconds;
        }

        public bool TryFindEntry(ActionDefinition action, out ClipEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                bool isTheActionsClip = entries[i].Action == action && entries[i].Clip != null;
                if (isTheActionsClip)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

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
                bool isMappedAgain = entry.Action != null && !actionsSeen.Add(entry.Action);
                if (isMappedAgain)
                {
                    errors.Add($"'{name}': '{entry.Action.name}' has two entries; the first one plays.");
                }
            }
        }
    }

    [Serializable]
    public sealed class ClipEntry
    {
        public const float DefaultBlendInSeconds = 0.08f;
        public const float DefaultBlendOutSeconds = 0.12f;
        public const float DefaultCrossfadeSeconds = 0.15f;

        // A looping clip wraps at its length, so a sample there is its first frame.
        private const float JustBeforeTheEnd = 0.001f;

        [SerializeField] private ActionDefinition action;

        [SerializeField] private AnimationClip clip;

        [Tooltip("Where in the clip the action starts, as a share of the clip's length.")]
        [SerializeField, Range(0f, 1f)] private float start = 0f;

        [Tooltip("Where in the clip the action ends, as a share of the clip's length. The part between is stretched or squeezed to the action's duration.")]
        [SerializeField, Range(0f, 1f)] private float end = 1f;

        [Tooltip("Seconds from locomotion to this clip when the action starts.")]
        [SerializeField, Min(0f)] private float blendInSeconds = DefaultBlendInSeconds;

        [Tooltip("Seconds back to locomotion when the action ends or is stopped.")]
        [SerializeField, Min(0f)] private float blendOutSeconds = DefaultBlendOutSeconds;

        [Tooltip("The clip's last frame stays on the body after the action ends, until the next action: a knockdown waits lying down for the get-up, and death stays down.")]
        [SerializeField] private bool keepsItsLastPose;

        [Tooltip("Seconds the clip before fades out under this one when this action follows another. 0 is a cut.")]
        [SerializeField, Min(0f)] private float crossfadeSeconds = DefaultCrossfadeSeconds;

        public ClipEntry() { }

        public ClipEntry(ActionDefinition action, AnimationClip clip, float start = 0f, float end = 1f, float blendInSeconds = DefaultBlendInSeconds, float blendOutSeconds = DefaultBlendOutSeconds, bool keepsItsLastPose = false, float crossfadeSeconds = DefaultCrossfadeSeconds)
        {
            this.action = action;
            this.clip = clip;
            this.start = start;
            this.end = end;
            this.blendInSeconds = blendInSeconds;
            this.blendOutSeconds = blendOutSeconds;
            this.keepsItsLastPose = keepsItsLastPose;
            this.crossfadeSeconds = crossfadeSeconds;
        }

        public ActionDefinition Action => action;
        public AnimationClip Clip => clip;
        public float Start => start;
        public float End => end;
        public float BlendInSeconds => blendInSeconds;
        public float BlendOutSeconds => blendOutSeconds;
        public bool KeepsItsLastPose => keepsItsLastPose;
        public float CrossfadeSeconds => crossfadeSeconds;

        public float ClipSecondsAt(float normalizedActionTime)
        {
            float clipSeconds = Mathf.Lerp(start, end, normalizedActionTime) * clip.length;
            return clip.isLooping ? Mathf.Min(clipSeconds, clip.length - JustBeforeTheEnd) : clipSeconds;
        }
    }
}
