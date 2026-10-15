using ArkhamCombat.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ArkhamCombat.Presentation
{
    /// <summary>The clip never advances on its own: its time is set from the gameplay clock, so a stopped clock is a held pose.</summary>
    public sealed class ActionClipPlayback
    {
        private enum ShownPose
        {
            Locomotion,
            TheActionsClip,
            LocomotionUnderAHold,
            TheKeptLastFrameOfAClip,
            AClipFadingOut
        }

        private const int LocomotionInput = 0;
        private const int ActionInput = 1;
        private const int ClipBeforeInput = 2;
        private const int InputCount = 3;
        private const float ClipNeverAdvancesOnItsOwn = 0f;

        private readonly CharacterClips clips;
        private readonly ClipBlend blend = new ClipBlend();
        private readonly ClipBlend crossfade = new ClipBlend();
        private readonly Transform model;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable locomotionPlayable;
        private AnimationClipPlayable clipPlayable;
        private AnimationClipPlayable clipBeforePlayable;
        private ClipEntry entry;
        private ShownPose shownPose = ShownPose.Locomotion;

        public ActionClipPlayback(Animator animator, CharacterClips clips)
        {
            this.clips = clips;
            model = animator.transform;
            LeaveMovingTheCharacterToGameplay(animator);
            BuildGraph(animator);
        }

        public ClipEntry Entry => entry;

        public bool IsShowingAClip => shownPose == ShownPose.TheActionsClip;

        public float ClipSeconds => clipPlayable.IsValid() ? (float)clipPlayable.GetTime() : 0f;

        public float Weight => blend.Weight;

        public bool IsCrossfading => !crossfade.HasReachedItsTarget;

        public float MoveRight => LocomotionParameter(LocomotionParameterNames.MoveRight);

        public float MoveForward => LocomotionParameter(LocomotionParameterNames.MoveForward);

        public string FrameDataLine
        {
            get
            {
                switch (shownPose)
                {
                    case ShownPose.TheActionsClip: return $"clip {entry.Action.name}  {ClipSeconds:0.00}s  weight {blend.Weight:0.00}{CrossfadePartOfTheLine}";
                    case ShownPose.LocomotionUnderAHold: return $"hold  weight {blend.Weight:0.00}{CrossfadePartOfTheLine}";
                    case ShownPose.TheKeptLastFrameOfAClip: return $"clip kept  weight {blend.Weight:0.00}{CrossfadePartOfTheLine}";
                    case ShownPose.AClipFadingOut: return $"clip fading out  weight {blend.Weight:0.00}{CrossfadePartOfTheLine}";
                    default: return string.Empty;
                }
            }
        }

        internal PlayableGraph Graph => graph;

        private string CrossfadePartOfTheLine => IsCrossfading ? $"  crossfade {crossfade.Weight:0.00}" : string.Empty;

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
            crossfade.Step(deltaTime);
            ApplyWeights();
            DropTheClipOnceFadedOut();
            DropTheClipBeforeOnceCrossfaded();
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
            bool keepsItsLastPose = IsShowingAClip && entry.KeepsItsLastPose;
            if (keepsItsLastPose)
            {
                KeepTheLastPose();
                return;
            }

            float seconds = IsShowingAClip ? entry.BlendOutSeconds : clips.HoldBlendOutSeconds;
            entry = null;
            shownPose = ShownPose.AClipFadingOut;
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

        private void KeepTheLastPose()
        {
            clipPlayable.SetTime(entry.ClipSecondsAt(1f));
            entry = null;
            shownPose = ShownPose.TheKeptLastFrameOfAClip;
        }

        private void ShowClip(ClipEntry entryToShow)
        {
            CrossfadeFromTheClipBefore(entryToShow.CrossfadeSeconds);
            entry = entryToShow;
            shownPose = ShownPose.TheActionsClip;
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
            shownPose = ShownPose.LocomotionUnderAHold;
            blend.BlendTo(0f, clips.HoldBlendOutSeconds);
            ApplyWeights();
        }

        private void CrossfadeFromTheClipBefore(float crossfadeSeconds)
        {
            DropTheClipBefore();
            bool hasAClipToCrossfadeFrom = clipPlayable.IsValid() && blend.Weight > 0f;
            if (!hasAClipToCrossfadeFrom)
            {
                DropTheClip();
                crossfade.CutTo(1f);
                return;
            }

            graph.Disconnect(mixer, ActionInput);
            graph.Connect(clipPlayable, 0, mixer, ClipBeforeInput);
            clipBeforePlayable = clipPlayable;
            crossfade.CutTo(0f);
            crossfade.BlendTo(1f, crossfadeSeconds);
            DropTheClipBeforeOnceCrossfaded();
        }

        private void ApplyWeights()
        {
            float shareOfTheClips = blend.Weight;
            mixer.SetInputWeight(ActionInput, shareOfTheClips * crossfade.Weight);
            mixer.SetInputWeight(ClipBeforeInput, shareOfTheClips * (1f - crossfade.Weight));
            mixer.SetInputWeight(LocomotionInput, 1f - shareOfTheClips);
        }

        private void DropTheClipOnceFadedOut()
        {
            bool hasFadedOut = shownPose == ShownPose.AClipFadingOut && blend.Weight <= 0f;
            if (hasFadedOut)
            {
                DropTheClip();
                shownPose = ShownPose.Locomotion;
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

        private void DropTheClipBeforeOnceCrossfaded()
        {
            if (!IsCrossfading)
            {
                DropTheClipBefore();
            }
        }

        private void DropTheClipBefore()
        {
            if (clipBeforePlayable.IsValid())
            {
                graph.Disconnect(mixer, ClipBeforeInput);
                clipBeforePlayable.Destroy();
            }
        }
    }
}
