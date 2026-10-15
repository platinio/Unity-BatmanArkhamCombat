using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// The runner's clock stays the one truth, as on the capsules: hit windows, warp and hit-stop mean
    /// the same on a character, and the clip only follows it. Cues that bend a capsule stay silent
    /// here; colour cues tint every renderer of the model.
    /// </summary>
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

        public string FrameDataLine
        {
            get
            {
                if (playback == null)
                {
                    return string.Empty;
                }

                bool isAHold = clock.IsPlaying && !playback.IsShowingAClip;
                return isAHold ? $"hold  weight {playback.Weight:0.00}" : playback.FrameDataLine;
            }
        }

        [Inject]
        internal void Construct(ILocomotionSignal locomotionSignal) => this.locomotionSignal = locomotionSignal;

        public float Speed
        {
            get => clock.Speed;
            set
            {
                clock.Speed = value;

                bool isFrozen = value <= 0f;
                if (isFrozen)
                {
                    CueTweens.Freeze(this);
                }
                else
                {
                    CueTweens.Resume(this);
                }
            }
        }

        private void Awake()
        {
            if (clips == null)
            {
                Debug.LogError($"[{nameof(AnimationPresentationDriver)}] No character clips assigned on '{name}', so every action plays as a hold.", this);
                return;
            }

            ReportAuthoringErrors();
            playback = new ActionClipPlayback(GetComponent<Animator>(), clips);
            tintedRenderers = GetComponentsInChildren<Renderer>();
            standingPose = BodyPose.Of(transform);
            restPose = standingPose;
            clock.CueDue += PlayCue;
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
            playback.Blend(deltaTime * clock.Speed);
            playback.MoveAt(locomotionSignal.PlanarVelocity);
        }

        public void RestAt(BodyPose pose) => restPose = pose;

        private void PlayCue(PresentationCue cue)
        {
            bool playsHere = cue.Kind != null && cue.Kind.CanPlayOnARiggedBody;
            if (playsHere)
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
