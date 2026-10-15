using ArkhamCombat.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ArkhamCombat.Presentation
{
    /// <summary>The clip never advances on its own: its time is set from the gameplay clock, so a stopped clock is a held pose.</summary>
    public sealed class ActionClipPlayback
    {
        private const int LocomotionInput = 0;
        private const int ActionInput = 1;
        private const int InputCount = 2;
        private const float ClipNeverAdvancesOnItsOwn = 0f;

        private readonly CharacterClips clips;
        private readonly ClipBlend blend = new ClipBlend();
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable clipPlayable;
        private ClipEntry entry;

        public ActionClipPlayback(Animator animator, CharacterClips clips)
        {
            this.clips = clips;
            LeaveMovingTheCharacterToGameplay(animator);
            BuildGraph(animator);
        }

        public ClipEntry Entry => entry;

        public bool IsShowingAClip => entry != null;

        public float ClipSeconds => clipPlayable.IsValid() ? (float)clipPlayable.GetTime() : 0f;

        public float Weight => blend.Weight;

        internal PlayableGraph Graph => graph;

        public void Show(ActionDefinition action)
        {
            bool hasAClip = clips.TryFindEntry(action, out ClipEntry entryForTheAction);
            if (hasAClip)
            {
                ShowClip(entryForTheAction);
            }
            else
            {
                Hold();
            }
        }

        public void FollowTheClock(float normalizedTime)
        {
            if (IsShowingAClip)
            {
                clipPlayable.SetTime(entry.ClipSecondsAt(normalizedTime));
            }
        }

        public void StepTheBlend(float deltaTime)
        {
            blend.Step(deltaTime);
            ApplyWeights();
            DropTheClipOnceFadedOut();
        }

        public void FadeOut()
        {
            float seconds = IsShowingAClip ? entry.BlendOutSeconds : clips.HoldBlendOutSeconds;
            entry = null;
            blend.BlendTo(0f, seconds);
            ApplyWeights();
        }

        public void Destroy()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }

        private static void LeaveMovingTheCharacterToGameplay(Animator animator) => animator.applyRootMotion = false;

        private void BuildGraph(Animator animator)
        {
            graph = PlayableGraph.Create($"{animator.name} actions");
            mixer = AnimationMixerPlayable.Create(graph, InputCount);
            if (clips.Locomotion != null)
            {
                AnimatorControllerPlayable locomotion = AnimatorControllerPlayable.Create(graph, clips.Locomotion);
                graph.Connect(locomotion, 0, mixer, LocomotionInput);
            }

            ApplyWeights();

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        private void ShowClip(ClipEntry entryToShow)
        {
            DropTheClip();
            entry = entryToShow;
            clipPlayable = AnimationClipPlayable.Create(graph, entry.Clip);
            clipPlayable.SetSpeed(ClipNeverAdvancesOnItsOwn);
            clipPlayable.SetTime(entry.ClipSecondsAt(0f));
            graph.Connect(clipPlayable, 0, mixer, ActionInput);
            blend.BlendTo(1f, entry.BlendInSeconds);
            ApplyWeights();
        }

        private void Hold()
        {
            entry = null;
            blend.BlendTo(0f, clips.HoldBlendOutSeconds);
            ApplyWeights();
        }

        private void ApplyWeights()
        {
            mixer.SetInputWeight(ActionInput, blend.Weight);
            mixer.SetInputWeight(LocomotionInput, 1f - blend.Weight);
        }

        private void DropTheClipOnceFadedOut()
        {
            bool hasFadedOut = !IsShowingAClip && blend.Weight <= 0f;
            if (hasFadedOut)
            {
                DropTheClip();
            }
        }

        private void DropTheClip()
        {
            if (clipPlayable.IsValid())
            {
                graph.Disconnect(mixer, ActionInput);
                clipPlayable.Destroy();
            }
        }
    }
}
