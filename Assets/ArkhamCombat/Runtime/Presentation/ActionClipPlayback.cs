using ArkhamCombat.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// The clip never advances on its own: its time is set from the gameplay clock, stretched so the
    /// part of the clip used spans the action, and a stopped clock is a held pose. Locomotion plays
    /// underneath the whole time and shows through as the clip blends out.
    /// </summary>
    public sealed class ActionClipPlayback
    {
        private const int LocomotionInput = 0;
        private const int ActionInput = 1;
        private const int InputCount = 2;
        private const float ClipNeverAdvancesOnItsOwn = 0f;

        private readonly CharacterClips clips;
        private readonly ClipBlend blend = new ClipBlend();
        private readonly Transform model;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable locomotion;
        private AnimationClipPlayable clipPlayable;
        private ClipEntry entry;

        // Gameplay moves the character: the motor, the warp, the dash, the agent. A clip only moves the body around its root.
        public ActionClipPlayback(Animator animator, CharacterClips clips)
        {
            this.clips = clips;
            model = animator.transform;
            animator.applyRootMotion = false;
            BuildGraph(animator);
        }

        public ClipEntry Entry => entry;

        public bool IsShowingAClip => entry != null;

        public float ClipSeconds => clipPlayable.IsValid() ? (float)clipPlayable.GetTime() : 0f;

        public float Weight => blend.Weight;

        public float MoveRight => LocomotionParameter(LocomotionParameterNames.MoveRight);

        public float MoveForward => LocomotionParameter(LocomotionParameterNames.MoveForward);

        public string FrameDataLine
        {
            get
            {
                if (IsShowingAClip)
                {
                    return $"clip {entry.Action.name}  {ClipSeconds:0.00}s  weight {blend.Weight:0.00}";
                }

                bool isFadingOut = blend.Weight > 0f;
                return isFadingOut ? $"clip fading out  weight {blend.Weight:0.00}" : string.Empty;
            }
        }

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

        public void Blend(float deltaTime)
        {
            blend.Step(deltaTime);
            ApplyWeights();
            DropTheClipOnceFadedOut();
        }

        public void MoveAt(Vector3 worldPlanarVelocity)
        {
            if (!locomotion.IsValid())
            {
                return;
            }

            Vector3 velocityInTheModelsFrame = model.InverseTransformDirection(worldPlanarVelocity);
            locomotion.SetFloat(LocomotionParameterNames.MoveRight, velocityInTheModelsFrame.x);
            locomotion.SetFloat(LocomotionParameterNames.MoveForward, velocityInTheModelsFrame.z);
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

        private void BuildGraph(Animator animator)
        {
            graph = PlayableGraph.Create($"{animator.name} actions");
            mixer = AnimationMixerPlayable.Create(graph, InputCount);
            if (clips.Locomotion != null)
            {
                locomotion = AnimatorControllerPlayable.Create(graph, clips.Locomotion);
                graph.Connect(locomotion, 0, mixer, LocomotionInput);
            }

            ApplyWeights();

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        // The new clip takes the weight the old one had, so a follow-up at full weight is a cut and never dips through locomotion.
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

        private float LocomotionParameter(string name) => locomotion.IsValid() ? locomotion.GetFloat(name) : 0f;

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
