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
            Quaternion rest = target.RestLocalRotation;
            Quaternion tilted = rest * Quaternion.Euler(cue.Strength, 0f, 0f);
            float half = cue.Duration * 0.5f;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(target.Body.DOLocalRotateQuaternion(tilted, half).SetEase(cue.Ease))
                .Append(target.Body.DOLocalRotateQuaternion(rest, half).SetEase(cue.Ease));
        }
    }

    /// <summary>Pushes the fist (or the body) forward by the cue's strength in metres and pulls it back.</summary>
    [Serializable]
    public sealed class PunchCue : ICueKind
    {
        public void Play(ICueTarget target, PresentationCue cue)
        {
            bool hasFist = target.Fist != null;
            Transform part = hasFist ? target.Fist : target.Body;
            Vector3 rest = hasFist ? target.FistRestLocalPosition : target.RestLocalPosition;
            Vector3 extended = rest + Vector3.forward * cue.Strength;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(part.DOLocalMove(extended, cue.Duration * 0.3f).SetEase(cue.Ease))
                .Append(part.DOLocalMove(rest, cue.Duration * 0.7f).SetEase(cue.Ease));
        }
    }

    /// <summary>Scales Y down and XZ up by the cue's strength as a factor, then back. Negative strength stretches.</summary>
    [Serializable]
    public sealed class SquashCue : ICueKind
    {
        public void Play(ICueTarget target, PresentationCue cue)
        {
            Vector3 rest = target.RestLocalScale;
            float s = Mathf.Clamp(cue.Strength, -0.9f, 0.9f);
            Vector3 squashed = new Vector3(rest.x * (1f + s), rest.y * (1f - s), rest.z * (1f + s));
            float half = cue.Duration * 0.5f;

            DOTween.Sequence().SetId(target.TweenId)
                .Append(target.Body.DOScale(squashed, half).SetEase(cue.Ease))
                .Append(target.Body.DOScale(rest, half).SetEase(cue.Ease));
        }
    }

    /// <summary>Yaws the body a number of full turns over the cue, then snaps to rest. For the roundhouse.</summary>
    [Serializable]
    public sealed class SpinCue : ICueKind
    {
        [Tooltip("Full turns over the cue's duration.")]
        [SerializeField, Min(1)] private int turns = 1;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Transform body = target.Body;
            Quaternion rest = target.RestLocalRotation;
            Vector3 spun = rest.eulerAngles + new Vector3(0f, 360f * turns * Mathf.Sign(cue.Strength), 0f);

            body.DOLocalRotate(spun, cue.Duration, RotateMode.FastBeyond360)
                .SetEase(cue.Ease)
                .SetId(target.TweenId)
                .OnComplete(() => body.localRotation = rest);
        }
    }

    /// <summary>Lerps the body's base colour toward the cue colour and back. Strength scales how far.</summary>
    [Serializable]
    public sealed class FlashCue : ICueKind
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Renderer renderer = target.BodyRenderer;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null)
            {
                return;
            }

            int property = material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
            Color baseColour = material.GetColor(property);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            float amount = 0f;

            DOTween.To(
                    () => amount,
                    value =>
                    {
                        amount = value;
                        block.SetColor(property, Color.Lerp(baseColour, cue.Colour, value * cue.Strength));
                        renderer.SetPropertyBlock(block);
                    },
                    1f,
                    cue.Duration * 0.5f)
                .SetEase(cue.Ease)
                .SetLoops(2, LoopType.Yoyo)
                .SetId(target.TweenId)
                .OnKill(() => renderer.SetPropertyBlock(null));
        }
    }
}
