using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Presentation
{
    [RequireComponent(typeof(Animator))]
    public sealed class AnimationPresentationDriver : MonoBehaviour, IPresentationDriver, ICueTarget
    {
        [Tooltip("Which clip each action plays on this character, and the locomotion under them.")]
        [SerializeField] private CharacterClips clips;

        private readonly ActionClock clock = new ActionClock();
        private ILocomotionSignal locomotionSignal = new StillLocomotionSignal();
        private ActionClipPlayback playback;
        private Renderer[] tintedRenderers = new Renderer[0];
        private BodyPose standingPose;
        private BodyPose restPose;

        public Transform Body => transform;
        public Transform Fist => null;
        public Renderer BodyRenderer => tintedRenderers.Length > 0 ? tintedRenderers[0] : null;
        public IReadOnlyList<Renderer> TintedRenderers => tintedRenderers;
        public BodyPose RestPose => restPose;
        public BodyPose StandingPose => standingPose;
        public Vector3 FistRestLocalPosition => Vector3.zero;
        public object TweenId => this;

        public bool IsPlaying => clock.IsPlaying;
        public float NormalizedTime => clock.NormalizedTime;

        public float ClipSeconds => playback != null ? playback.ClipSeconds : 0f;
        public float ClipWeight => playback != null ? playback.Weight : 0f;
        public float MoveRight => playback != null ? playback.MoveRight : 0f;
        public float MoveForward => playback != null ? playback.MoveForward : 0f;
        public string FrameDataLine => playback != null ? playback.FrameDataLine : string.Empty;

        internal CharacterClips Clips => clips;

        public float Speed
        {
            get => clock.Speed;
            set
            {
                clock.Speed = value;
                CueTweens.FollowSpeed(this, value);
            }
        }

        [Inject]
        internal void Construct(ILocomotionSignal locomotionSignal) => this.locomotionSignal = locomotionSignal;

        internal void Awake()
        {
            tintedRenderers = GetComponentsInChildren<Renderer>();
            standingPose = BodyPose.Of(transform);
            restPose = standingPose;
            clock.CueDue += PlayCue;

            if (clips == null)
            {
                Debug.LogError($"[{nameof(AnimationPresentationDriver)}] No character clips assigned on '{name}', so every action plays as a hold with only its colour cues.", this);
                return;
            }

            ReportAuthoringErrors();
            playback = new ActionClipPlayback(GetComponent<Animator>(), clips);
        }

        private void OnDestroy()
        {
            clock.CueDue -= PlayCue;
            CueTweens.Kill(this);
            playback?.Destroy();
        }

        public void Play(ActionDefinition action)
        {
            CueTweens.Kill(this);
            playback?.Show(action);
            clock.Play(action);
        }

        public void Stop()
        {
            clock.Stop();
            CueTweens.Kill(this);
            playback?.FadeOut();
        }

        public void Tick(float deltaTime)
        {
            clock.Tick(deltaTime);
            if (playback == null)
            {
                return;
            }

            playback.FollowTheClock(clock.NormalizedTime);
            playback.StepTheBlend(deltaTime * clock.Speed);
            playback.ShowLocomotionAt(locomotionSignal.PlanarVelocity);
        }

        public void RestAt(BodyPose pose) => restPose = pose;

        private void PlayCue(PresentationCue cue)
        {
            bool canPlayHere = cue.Kind != null && cue.Kind.CanPlayOnARiggedBody;
            if (canPlayHere)
            {
                cue.Kind.Play(this, cue);
            }
        }

        private void ReportAuthoringErrors()
        {
            List<string> errors = new List<string>();
            clips.Validate(errors);
            foreach (string error in errors)
            {
                Debug.LogError($"[{nameof(AnimationPresentationDriver)}] {error}", clips);
            }
        }
    }
}
