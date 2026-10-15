using System;
using System.Collections.Generic;
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
            Quaternion restRotation = target.RestPose.LocalRotation;
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
            Vector3 restPosition = hasFist ? target.FistRestLocalPosition : target.RestPose.LocalPosition;
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
        internal const float MaxSquashFactor = 0.9f;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            Vector3 restScale = target.RestPose.LocalScale;
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
            Quaternion restRotation = target.RestPose.LocalRotation;
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
        private const int OneFlash = 1;

        public void Play(ICueTarget target, PresentationCue cue) => BodyTint.Flash(target, cue, OneFlash, cue.Duration);

        public bool CanPlayOnARiggedBody => true;
    }

    /// <summary>Strength scales how far the colour goes, as for a flash.</summary>
    [Serializable]
    public sealed class PulseCue : ICueKind
    {
        [Tooltip("About how long one flash takes, there and back. The cue plays the nearest whole number of flashes and stretches them to fill its duration exactly, so the pulse ends when the cue does.")]
        [SerializeField, Min(0.05f)] private float secondsPerFlash = 0.2f;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            int flashes = Mathf.Max(1, Mathf.RoundToInt(cue.Duration / secondsPerFlash));
            float secondsPerFlashToFillTheCue = cue.Duration / flashes;

            BodyTint.Flash(target, cue, flashes, secondsPerFlashToFillTheCue);
        }

        public bool CanPlayOnARiggedBody => true;
    }

    internal static class BodyTint
    {
        private const float FullTint = 1f;
        private const int TintThenUntint = 2;

        public static void Flash(ICueTarget target, PresentationCue cue, int flashes, float secondsPerFlash)
        {
            List<TintedRenderer> tinted = TintedRenderer.Of(target.TintedRenderers);
            if (tinted.Count == 0)
            {
                return;
            }

            float secondsToFullTint = secondsPerFlash * 0.5f;
            float tint = 0f;

            void ApplyTint(float value)
            {
                tint = value;
                foreach (TintedRenderer renderer in tinted)
                {
                    renderer.Tint(cue.Colour, tint * cue.Strength);
                }
            }

            void ClearTint()
            {
                foreach (TintedRenderer renderer in tinted)
                {
                    renderer.Clear();
                }
            }

            DOTween.To(() => tint, ApplyTint, FullTint, secondsToFullTint)
                .SetEase(cue.Ease)
                .SetLoops(TintThenUntint * flashes, LoopType.Yoyo)
                .SetId(target.TweenId)
                .OnKill(ClearTint);
        }
    }

    /// <summary>A model of many materials flashes as one: each renderer keeps its own rest colour and its own property block.</summary>
    internal sealed class TintedRenderer
    {
        private readonly Renderer renderer;
        private readonly int colourProperty;
        private readonly Color restColour;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private TintedRenderer(Renderer renderer, Material material)
        {
            this.renderer = renderer;
            colourProperty = MaterialColourProperty.Of(material);
            restColour = material.GetColor(colourProperty);
        }

        public static List<TintedRenderer> Of(IReadOnlyList<Renderer> renderers)
        {
            List<TintedRenderer> tinted = new List<TintedRenderer>();
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null)
                {
                    tinted.Add(new TintedRenderer(renderer, material));
                }
            }

            return tinted;
        }

        public void Tint(Color colour, float share)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock.SetColor(colourProperty, Color.Lerp(restColour, colour, share));
            renderer.SetPropertyBlock(propertyBlock);
        }

        public void Clear()
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }
    }

    /// <summary>Lays the body down by the cue's strength in degrees, backward when positive, and makes lying its rest pose.</summary>
    [Serializable]
    public sealed class FallCue : ICueKind
    {
        public void Play(ICueTarget target, PresentationCue cue)
        {
            BodyPose standing = target.StandingPose;
            Quaternion lyingRotation = standing.LocalRotation * Quaternion.Euler(-cue.Strength, 0f, 0f);
            Vector3 lyingPosition = standing.LocalPosition + Vector3.down * HeightLostFalling(target, cue.Strength);

            HeldPose.Hold(target, cue, new BodyPose(lyingPosition, lyingRotation, standing.LocalScale));
        }

        // A body rotates about its centre, so a capsule lying flat would float at its standing height.
        // The centre drops by what the body is taller than deep, scaled by how far it has turned.
        internal static float HeightLostFalling(ICueTarget target, float fallDegrees)
        {
            Renderer renderer = target.BodyRenderer;
            if (renderer == null)
            {
                return 0f;
            }

            Vector3 standingScale = target.StandingPose.LocalScale;
            Bounds meshBounds = renderer.localBounds;
            float halfHeight = meshBounds.extents.y * standingScale.y;
            float halfDepth = meshBounds.extents.z * standingScale.z;
            float shareTurned = 1f - Mathf.Cos(fallDegrees * Mathf.Deg2Rad);

            return Mathf.Max(0f, (halfHeight - halfDepth) * shareTurned);
        }
    }

    /// <summary>Stands the body up and makes standing its rest pose again.</summary>
    [Serializable]
    public sealed class RiseCue : ICueKind
    {
        public void Play(ICueTarget target, PresentationCue cue) => HeldPose.Hold(target, cue, target.StandingPose);
    }

    /// <summary>Squashes the body by the cue's strength as a factor and tilts it forward. No return tween: Stop straightens it.</summary>
    [Serializable]
    public sealed class CrumpleCue : ICueKind
    {
        [Tooltip("How far forward the crumpled body tilts.")]
        [SerializeField] private float tiltDegrees = 25f;

        public void Play(ICueTarget target, PresentationCue cue)
        {
            BodyPose rest = target.RestPose;
            float squashFactor = Mathf.Clamp(cue.Strength, 0f, SquashCue.MaxSquashFactor);
            Vector3 crumpledScale = new Vector3(rest.LocalScale.x, rest.LocalScale.y * (1f - squashFactor), rest.LocalScale.z);
            Quaternion crumpledRotation = rest.LocalRotation * Quaternion.Euler(tiltDegrees, 0f, 0f);

            DOTween.Sequence().SetId(target.TweenId)
                .Join(target.Body.DOScale(crumpledScale, cue.Duration).SetEase(cue.Ease))
                .Join(target.Body.DOLocalRotateQuaternion(crumpledRotation, cue.Duration).SetEase(cue.Ease));
        }
    }

    internal static class HeldPose
    {
        // The rest pose moves before the tween starts, so an interrupted fall still ends lying down.
        public static void Hold(ICueTarget target, PresentationCue cue, BodyPose pose)
        {
            target.RestAt(pose);

            DOTween.Sequence().SetId(target.TweenId)
                .Join(target.Body.DOLocalMove(pose.LocalPosition, cue.Duration).SetEase(cue.Ease))
                .Join(target.Body.DOLocalRotateQuaternion(pose.LocalRotation, cue.Duration).SetEase(cue.Ease))
                .Join(target.Body.DOScale(pose.LocalScale, cue.Duration).SetEase(cue.Ease));
        }
    }
}
