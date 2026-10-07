using ArkhamCombat.Combat;
using DG.Tweening;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>
    /// The demo's only driver: the core's duration clock with DOTween cues on a body child. Cues fire
    /// from the clock, not from DOTween's own time, so hit-stop and interrupts stop them exactly
    /// where gameplay stopped. Every tween a cue starts carries this component as its id, which is
    /// how Stop kills them as a group and Speed pauses them.
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
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Vector3 fistRestPosition;

        public Transform Body => body;
        public Transform Fist => fist;
        public Renderer BodyRenderer => bodyRenderer;
        public Vector3 RestLocalPosition => restPosition;
        public Quaternion RestLocalRotation => restRotation;
        public Vector3 RestLocalScale => restScale;
        public Vector3 FistRestLocalPosition => fistRestPosition;
        public object TweenId => this;

        public bool IsPlaying => clock.IsPlaying;
        public float NormalizedTime => clock.NormalizedTime;

        public float Speed
        {
            get => clock.Speed;
            set
            {
                clock.Speed = value;
                if (value <= 0f)
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

            restPosition = body.localPosition;
            restRotation = body.localRotation;
            restScale = body.localScale;
            if (fist != null)
            {
                fistRestPosition = fist.localPosition;
            }

            clock.CueDue += OnCueDue;
        }

        private void OnDestroy()
        {
            clock.CueDue -= OnCueDue;
            DOTween.Kill(TweenId);
        }

        public void Play(ActionDefinition action)
        {
            DOTween.Kill(TweenId);
            clock.Play(action);
        }

        public void Stop()
        {
            clock.Stop();
            DOTween.Kill(TweenId);
            BlendToRest();
        }

        public void Tick(float deltaTime) => clock.Tick(deltaTime);

        private void OnCueDue(PresentationCue cue)
        {
            if (body != null && cue.Kind != null)
            {
                cue.Kind.Play(this, cue);
            }
        }

        private void BlendToRest()
        {
            if (body == null)
            {
                return;
            }

            body.DOLocalMove(restPosition, restBlendSeconds).SetId(TweenId);
            body.DOLocalRotateQuaternion(restRotation, restBlendSeconds).SetId(TweenId);
            body.DOScale(restScale, restBlendSeconds).SetId(TweenId);

            if (fist != null)
            {
                fist.DOLocalMove(fistRestPosition, restBlendSeconds).SetId(TweenId);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.SetPropertyBlock(null);
            }
        }
    }
}
