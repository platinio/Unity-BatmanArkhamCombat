using ArkhamCombat.Combat;
using DG.Tweening;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// The demo's only driver: the core's action clock with DOTween cues on a body child. Cues fire
    /// from the clock, not from DOTween's own time, so hit-stop and interrupts stop them exactly
    /// where gameplay stopped. Every tween a cue starts carries this component as its id, so the
    /// cue tweens can be killed or paused as a group.
    /// </summary>
    public sealed class ProceduralPresentationDriver : MonoBehaviour, IPresentationDriver, ICueTarget
    {
        [Tooltip("The child the cues move. Never the root: the motor owns that.")]
        [SerializeField] private Transform body;

        [Tooltip("Optional. Punch cues push this; the body when empty.")]
        [SerializeField] private Transform fist;

        [Tooltip("Optional. Colour cues tint this renderer.")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Seconds the body takes to return to its rest pose after an action ends or is stopped.")]
        [SerializeField, Min(0f)] private float restBlendSeconds = 0.12f;

        private readonly ActionClock clock = new ActionClock();
        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private Vector3 restLocalScale;
        private Vector3 fistRestLocalPosition;

        public Transform Body => body;
        public Transform Fist => fist;
        public Renderer BodyRenderer => bodyRenderer;
        public Vector3 RestLocalPosition => restLocalPosition;
        public Quaternion RestLocalRotation => restLocalRotation;
        public Vector3 RestLocalScale => restLocalScale;
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

                bool isFrozen = value <= 0f;
                if (isFrozen)
                {
                    DOTween.Pause(TweenId);
                }
                else
                {
                    DOTween.Play(TweenId);
                }
            }
        }

        private void Awake()
        {
            if (body == null)
            {
                Debug.LogError($"[{nameof(ProceduralPresentationDriver)}] No body child assigned on '{name}'.", this);
                enabled = false;
                return;
            }

            RememberRestPose();
            clock.CueDue += PlayCue;
        }

        private void OnDestroy()
        {
            clock.CueDue -= PlayCue;
            KillCueTweens();
        }

        public void Play(ActionDefinition action)
        {
            KillCueTweens();
            clock.Play(action);
        }

        public void Stop()
        {
            clock.Stop();
            KillCueTweens();
            BlendToRestPose();
        }

        public void Tick(float deltaTime) => clock.Tick(deltaTime);

        private void RememberRestPose()
        {
            restLocalPosition = body.localPosition;
            restLocalRotation = body.localRotation;
            restLocalScale = body.localScale;

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

        private void KillCueTweens() => DOTween.Kill(TweenId);

        private void BlendToRestPose()
        {
            if (body == null)
            {
                return;
            }

            body.DOLocalMove(restLocalPosition, restBlendSeconds).SetId(TweenId);
            body.DOLocalRotateQuaternion(restLocalRotation, restBlendSeconds).SetId(TweenId);
            body.DOScale(restLocalScale, restBlendSeconds).SetId(TweenId);

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
