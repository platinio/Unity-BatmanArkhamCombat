using System.Collections.Generic;
using ArkhamCombat.Combat;
using DG.Tweening;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// Cues fire from the clock, not from DOTween's own time, so hit-stop and interrupts stop them
    /// exactly where gameplay stopped. Every tween a cue starts carries this component as its id,
    /// so the cue tweens can be killed or paused as a group.
    /// </summary>
    public sealed class ProceduralPresentationDriver : MonoBehaviour, IPresentationDriver, ICueTarget
    {
        [Tooltip("What the cues move: this object, a child of the character. Never the character's root: the motor owns that.")]
        [SerializeField] private Transform body;

        [Tooltip("Optional. Punch cues push this; the body when empty.")]
        [SerializeField] private Transform fist;

        [Tooltip("Optional. Colour cues tint this renderer, and a fall measures the body on it.")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Seconds the body takes to return to its rest pose after an action ends or is stopped.")]
        [SerializeField, Min(0f)] private float restBlendSeconds = 0.12f;

        private readonly ActionClock clock = new ActionClock();
        private BodyPose standingPose;
        private BodyPose restPose;
        private Vector3 fistRestLocalPosition;

        public Transform Body => body;
        public Transform Fist => fist;
        public Renderer BodyRenderer => bodyRenderer;
        public IReadOnlyList<Renderer> TintedRenderers => bodyRenderer != null ? new[] { bodyRenderer } : new Renderer[0];
        public BodyPose RestPose => restPose;
        public BodyPose StandingPose => standingPose;
        public Vector3 FistRestLocalPosition => fistRestLocalPosition;
        public object TweenId => this;

        public bool IsPlaying => clock.IsPlaying;
        public float NormalizedTime => clock.NormalizedTime;

        public float Speed
        {
            get => clock.Speed;
            set
            {
                clock.Speed = value;
                CueTweens.FollowSpeed(this, value);
            }
        }

        private void Awake()
        {
            if (body == null)
            {
                Debug.LogError($"[{nameof(ProceduralPresentationDriver)}] No body assigned on '{name}'.", this);
                enabled = false;
                return;
            }

            RememberStandingPose();
            clock.CueDue += PlayCue;
        }

        private void OnDestroy()
        {
            clock.CueDue -= PlayCue;
            CueTweens.Kill(this);
        }

        public void Play(ActionDefinition action)
        {
            CueTweens.Kill(this);
            clock.Play(action);
        }

        public void Stop()
        {
            clock.Stop();
            CueTweens.Kill(this);
            BlendToRestPose();
        }

        public void Tick(float deltaTime) => clock.Tick(deltaTime);

        public void RestAt(BodyPose pose) => restPose = pose;

        private void RememberStandingPose()
        {
            standingPose = BodyPose.Of(body);
            restPose = standingPose;

            if (fist != null)
            {
                fistRestLocalPosition = fist.localPosition;
            }
        }

        private void PlayCue(PresentationCue cue)
        {
            if (body != null && cue.Kind != null)
            {
                cue.Kind.Play(this, cue);
            }
        }

        private void BlendToRestPose()
        {
            if (body == null)
            {
                return;
            }

            body.DOLocalMove(restPose.LocalPosition, restBlendSeconds).SetId(TweenId);
            body.DOLocalRotateQuaternion(restPose.LocalRotation, restBlendSeconds).SetId(TweenId);
            body.DOScale(restPose.LocalScale, restBlendSeconds).SetId(TweenId);

            if (fist != null)
            {
                fist.DOLocalMove(fistRestLocalPosition, restBlendSeconds).SetId(TweenId);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.SetPropertyBlock(null);
            }
        }
    }
}
