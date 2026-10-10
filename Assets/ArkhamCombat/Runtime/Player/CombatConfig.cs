using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// One asset, bound by the combat installer, so a different feel is a different asset. The
    /// stance is set on each character's CombatCharacterInstaller.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Combat Config", fileName = "CombatConfig")]
    public sealed class CombatConfig : ScriptableObject, IComboMeterSettings, ITargetingSettings, IFacingSettings, ICombatFactsSettings
    {
        [Header("Interrupts")]
        [Tooltip("The kind of press that evades. Its global edges are taken only inside the current attack's evade window, or while idle.")]
        [SerializeField] private IntentKind evadeKind;

        [Tooltip("The kind of press that counters. Its global edges are taken only while a counterable attack is incoming.")]
        [SerializeField] private IntentKind counterKind;

        [Header("Combo meter")]
        [Tooltip("Combo counts at which the tier rises. The tier is the number of thresholds reached.")]
        [SerializeField] private List<int> comboTierThresholds = new List<int> { 3, 5, 8 };

        [Tooltip("Seconds without a new hit before the combo is lost. Tuned separately from the chain reset.")]
        [SerializeField, Min(0f)] private float comboTimeoutSeconds = 2.5f;

        [Header("Facing")]
        [Tooltip("SmoothDamp time for turning toward the target while an action plays. Small: the warp needs the character squared up.")]
        [SerializeField, Min(0f)] private float faceTargetSmoothTime = 0.05f;

        [Tooltip("Lunge limit the targetBeyondLunge fact is measured against while no attack plays.")]
        [SerializeField, Min(0f)] private float maxLungeWhileIdle = 4f;

        [Tooltip("A target within this many degrees either side of the facing counts as dead ahead for the targetSide fact, rather than left or right.")]
        [SerializeField, Range(0f, 90f)] private float deadAheadAngle = 6f;

        [Header("Stand-in target selection (spec 04 replaces this)")]
        [Tooltip("Dummies farther than this are never picked.")]
        [SerializeField, Min(0f)] private float maxTargetDistance = 8f;

        [Tooltip("Dummies more than this many degrees off the stick (or the facing, with no stick) are never picked.")]
        [SerializeField, Range(0f, 180f)] private float maxTargetAngle = 110f;

        [Tooltip("A dummy this many degrees off the stick scores as if it were twice as far. Lower favours aim over distance.")]
        [SerializeField, Min(1f)] private float angleCountingAsDoubleDistance = 90f;

        [Tooltip("A stick pushed less than this (0 to 1) is at rest; the target is then picked along the facing instead of the stick.")]
        [SerializeField, Range(0f, 1f)] private float stickPushedMagnitude = 0.1f;

        [Header("Stand-in hit check (spec 05 replaces this)")]
        [Tooltip("A strike lands when the target is within strikeDistance plus this at the hit window's start.")]
        [SerializeField, Min(0f)] private float hitRangeMargin = 0.6f;

        public IntentKind EvadeKind => evadeKind;
        public IntentKind CounterKind => counterKind;
        public float HitRangeMargin => hitRangeMargin;

        IReadOnlyList<int> IComboMeterSettings.TierThresholds => comboTierThresholds;
        float IComboMeterSettings.MeterTimeoutSeconds => comboTimeoutSeconds;

        float ITargetingSettings.MaxTargetDistance => maxTargetDistance;
        float ITargetingSettings.MaxTargetAngle => maxTargetAngle;
        float ITargetingSettings.AngleCountingAsDoubleDistance => angleCountingAsDoubleDistance;
        float ITargetingSettings.StickPushedMagnitude => stickPushedMagnitude;

        float IFacingSettings.FaceTargetSmoothTime => faceTargetSmoothTime;

        float ICombatFactsSettings.DeadAheadAngle => deadAheadAngle;
        float ICombatFactsSettings.MaxLungeWhileIdle => maxLungeWhileIdle;
    }
}
