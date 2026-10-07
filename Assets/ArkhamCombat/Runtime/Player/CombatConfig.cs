using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Everything the player's combat is tuned by: the stance, the meter, and the numbers the
    /// stand-in target and hit checks use until specs 04 and 05 replace them. One asset, bound by
    /// the combat installer, so a different feel is a different asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Combat Config", fileName = "CombatConfig")]
    public sealed class CombatConfig : ScriptableObject
    {
        [Header("Chain")]
        [Tooltip("The chain the player fights with.")]
        [SerializeField] private Stance stance;

        [Header("Meter")]
        [SerializeField] private ComboMeterSettings meter = new ComboMeterSettings();

        [Header("Facing")]
        [Tooltip("SmoothDamp time for turning toward the target while an action plays. Small: the warp needs the character squared up.")]
        [SerializeField, Min(0f)] private float faceTurnSmoothTime = 0.05f;

        [Tooltip("Lunge limit the targetBeyondLunge fact is measured against while no attack plays.")]
        [SerializeField, Min(0f)] private float idleMaxLunge = 4f;

        [Header("Stand-in target selection (spec 04 replaces this)")]
        [Tooltip("Dummies farther than this are never picked.")]
        [SerializeField, Min(0f)] private float targetMaxDistance = 8f;

        [Tooltip("Dummies more than this many degrees off the stick (or the facing, with no stick) are never picked.")]
        [SerializeField, Range(0f, 180f)] private float targetMaxAngle = 110f;

        [Header("Stand-in hit check (spec 05 replaces this)")]
        [Tooltip("A strike lands when the target is within strikeDistance plus this at the active window's start.")]
        [SerializeField, Min(0f)] private float hitRangeMargin = 0.6f;

        public Stance Stance => stance;
        public ComboMeterSettings Meter => meter;
        public float FaceTurnSmoothTime => faceTurnSmoothTime;
        public float IdleMaxLunge => idleMaxLunge;
        public float TargetMaxDistance => targetMaxDistance;
        public float TargetMaxAngle => targetMaxAngle;
        public float HitRangeMargin => hitRangeMargin;
    }
}
