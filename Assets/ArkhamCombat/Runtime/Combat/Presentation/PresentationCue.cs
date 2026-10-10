using System;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A kind is a small class so a new kind is one file and appears on every asset without
    /// registration. The signature returns nothing on purpose: the driver kills tweens by the
    /// target's id, so the core never learns what a tween is.
    /// </summary>
    public interface ICueKind
    {
        void Play(ICueTarget target, PresentationCue cue);
    }

    /// <summary>
    /// Only the body child and its props, never the root: the root is the motor's, and a cue that
    /// moved it would fight the warp.
    /// </summary>
    public interface ICueTarget
    {
        Transform Body { get; }

        /// <summary>Optional. A punch cue falls back to the body when there is none.</summary>
        Transform Fist { get; }

        /// <summary>Optional. Colour cues do nothing without one.</summary>
        Renderer BodyRenderer { get; }

        Vector3 RestLocalPosition { get; }
        Quaternion RestLocalRotation { get; }
        Vector3 RestLocalScale { get; }

        /// <summary>Rest of the fist, when there is one, so a punch returns to where it started however it was interrupted.</summary>
        Vector3 FistRestLocalPosition { get; }

        /// <summary>Every tween a cue starts carries this id, so the driver can kill or pause them as a group.</summary>
        object TweenId { get; }
    }

    /// <summary>
    /// Lives on the action next to the windows so the look is tuned where the timing is.
    /// </summary>
    [Serializable]
    public sealed class PresentationCue
    {
        [Tooltip("Normalized action time the cue fires.")]
        [SerializeField, Range(0f, 1f)] private float firesAt;

        [SerializeReference, SubclassSelector] private ICueKind kind;

        [Tooltip("Seconds the tween runs.")]
        [SerializeField, Min(0f)] private float duration = 0.15f;

        [Tooltip("Scalar: degrees for leans and spins, metres for a punch, a factor for scales, 0 to 1 for colour.")]
        [SerializeField] private float strength = 1f;

        [Tooltip("Easing over the tween's own time.")]
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("For colour cues.")]
        [SerializeField] private Color colour = Color.white;

        public PresentationCue() { }

        public PresentationCue(float firesAt, ICueKind kind, float duration = 0.15f, float strength = 1f)
        {
            this.firesAt = firesAt;
            this.kind = kind;
            this.duration = duration;
            this.strength = strength;
        }

        public float FiresAt => firesAt;
        public ICueKind Kind => kind;
        public float Duration => duration;
        public float Strength => strength;
        public AnimationCurve Ease => ease;
        public Color Colour => colour;
    }
}
