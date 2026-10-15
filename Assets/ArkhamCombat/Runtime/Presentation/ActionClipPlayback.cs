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
        private readonly Transform model;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable locomotionPlayable;
        private AnimationClipPlayable clipPlayable;
        private ClipEntry entry;
        private bool isHoldingAnAction;

        public ActionClipPlayback(Animator animator, CharacterClips clips)
        {
            this.clips = clips;
            model = animator.transform;
            LeaveMovingTheCharacterToGameplay(animator);
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

                if (isHoldingAnAction)
                {
                    return $"hold  weight {blend.Weight:0.00}";
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

        public void StepTheBlend(float deltaTime)
        {
            blend.Step(deltaTime);
            ApplyWeights();
            DropTheClipOnceFadedOut();
        }

        public void ShowLocomotionAt(Vector3 worldPlanarVelocity)
        {
            if (!locomotionPlayable.IsValid())
            {
                return;
            }

            Vector3 velocityInTheModelsFrame = model.InverseTransformDirection(worldPlanarVelocity);
            locomotionPlayable.SetFloat(LocomotionParameterNames.MoveRight, velocityInTheModelsFrame.x);
            locomotionPlayable.SetFloat(LocomotionParameterNames.MoveForward, velocityInTheModelsFrame.z);
        }

        public void FadeOut()
        {
            float seconds = IsShowingAClip ? entry.BlendOutSeconds : clips.HoldBlendOutSeconds;
            entry = null;
            isHoldingAnAction = false;
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
                locomotionPlayable = AnimatorControllerPlayable.Create(graph, clips.Locomotion);
                graph.Connect(locomotionPlayable, 0, mixer, LocomotionInput);
                ReportMissingLocomotionParameters();
            }

            ApplyWeights();

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        private void ReportMissingLocomotionParameters()
        {
            foreach (string parameterName in new[] { LocomotionParameterNames.MoveRight, LocomotionParameterNames.MoveForward })
            {
                if (!HasLocomotionParameter(parameterName))
                {
                    Debug.LogError($"[{nameof(ActionClipPlayback)}] '{clips.Locomotion.name}' has no float parameter '{parameterName}', so the character cannot walk.", clips);
                }
            }
        }

        private bool HasLocomotionParameter(string parameterName)
        {
            for (int i = 0; i < locomotionPlayable.GetParameterCount(); i++)
            {
                if (locomotionPlayable.GetParameter(i).name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private float LocomotionParameter(string parameterName) => locomotionPlayable.IsValid() ? locomotionPlayable.GetFloat(parameterName) : 0f;

        private void ShowClip(ClipEntry entryToShow)
        {
            DropTheClip();
            entry = entryToShow;
            isHoldingAnAction = false;
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
            isHoldingAnAction = true;
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
