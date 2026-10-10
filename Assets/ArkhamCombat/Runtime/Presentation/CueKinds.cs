using System;
using ArkhamCombat.Combat;
using DG.Tweening;
using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>Tilts the body forward by the cue's strength in degrees and back. Negative leans away.</summary>
    [Serializable]
    public sealed class LeanCue : ICueKind
    {
        public void Play(ICueTarget target, PresentationCue cue)
        {
            Quaternion restRotation = target.RestLocalRotation;
            Quaternion tiltedRotation = restRotation * Quaternion.Euler(cue.Strength, 0f, 0f);
            float halfDuration = cue.Duration * 0.5f;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(target.Body.DOLocalRotateQuaternion(tiltedRotation, halfDuration).SetEase(cue.Ease))
                .Append(target.Body.DOLocalRotateQuaternion(restRotation, halfDuration).SetEase(cue.Ease));
        }
    }

    /// <summary>Pushes the fist (or the body) forward by the cue's strength in metres and pulls it back.</summary>
    [Serializable]
    public sealed class PunchCue : ICueKind
    {
        [Tooltip("Share of the cue spent extending; the rest pulls back. Quick out and slow back reads as a snap rather than a shove.")]
        [SerializeField, Range(0f, 1f)] private float shareOfDurationExtending = 0.3f;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            bool hasFist = target.Fist != null;
            Transform pushedPart = hasFist ? target.Fist : target.Body;
            Vector3 restPosition = hasFist ? target.FistRestLocalPosition : target.RestLocalPosition;
            Vector3 extendedPosition = restPosition + Vector3.forward * cue.Strength;
            float extendDuration = cue.Duration * shareOfDurationExtending;
            float returnDuration = cue.Duration - extendDuration;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(pushedPart.DOLocalMove(extendedPosition, extendDuration).SetEase(cue.Ease))
                .Append(pushedPart.DOLocalMove(restPosition, returnDuration).SetEase(cue.Ease));
        }
    }

    /// <summary>Scales Y down and XZ up by the cue's strength as a factor, then back. Negative strength stretches.</summary>
    [Serializable]
    public sealed class SquashCue : ICueKind
    {
        // At 1 the body would flatten to nothing on one axis.
        private const float MaxSquashFactor = 0.9f;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Vector3 restScale = target.RestLocalScale;
            float squashFactor = Mathf.Clamp(cue.Strength, -MaxSquashFactor, MaxSquashFactor);
            Vector3 squashedScale = new Vector3(
                restScale.x * (1f + squashFactor),
                restScale.y * (1f - squashFactor),
                restScale.z * (1f + squashFactor));
            float halfDuration = cue.Duration * 0.5f;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(target.Body.DOScale(squashedScale, halfDuration).SetEase(cue.Ease))
                .Append(target.Body.DOScale(restScale, halfDuration).SetEase(cue.Ease));
        }
    }

    /// <summary>Yaws the body a number of full turns over the cue, then snaps to rest. The strength's sign picks the direction.</summary>
    [Serializable]
    public sealed class SpinCue : ICueKind
    {
        private const float DegreesPerTurn = 360f;

        [Tooltip("Full turns over the cue's duration.")]
        [SerializeField, Min(1)] private int turns = 1;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Transform body = target.Body;
            Quaternion restRotation = target.RestLocalRotation;
            float spinDirection = Mathf.Sign(cue.Strength);
            Vector3 spunEulerAngles = restRotation.eulerAngles + new Vector3(0f, DegreesPerTurn * turns * spinDirection, 0f);

            body.DOLocalRotate(spunEulerAngles, cue.Duration, RotateMode.FastBeyond360)
                .SetEase(cue.Ease)
                .SetId(target.TweenId)
                .OnKill(() =>
                {
                    // On kill as well as on completion: a spin stopped early must not leave the body yawed.
                    if (body != null)
                    {
                        body.localRotation = restRotation;
                    }
                });
        }
    }

    /// <summary>Lerps the body's colour toward the cue colour and back. Strength scales how far.</summary>
    [Serializable]
    public sealed class FlashCue : ICueKind
    {
        private const float FullTint = 1f;
        private const int TintThenUntint = 2;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Renderer renderer = target.BodyRenderer;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null)
            {
                return;
            }

            int colourProperty = MaterialColourProperty.Of(material);
            Color restColour = material.GetColor(colourProperty);
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            float halfDuration = cue.Duration * 0.5f;
            float tint = 0f;

            void ApplyTint(float value)
            {
                tint = value;
                if (renderer == null)
                {
                    return;
                }

                propertyBlock.SetColor(colourProperty, Color.Lerp(restColour, cue.Colour, tint * cue.Strength));
                renderer.SetPropertyBlock(propertyBlock);
            }

            void ClearTint()
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            DOTween.To(() => tint, ApplyTint, FullTint, halfDuration)
                .SetEase(cue.Ease)
                .SetLoops(TintThenUntint, LoopType.Yoyo)
                .SetId(target.TweenId)
                .OnKill(ClearTint);
        }
    }
}
