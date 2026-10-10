using System;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class CombatFactsUpdater : CharacterComponent
    {
        private const string Source = "CombatFacts";
        private const float NegligibleSqrMagnitude = 1e-4f;

        private ActionRunner runner;
        private IActionTargetPicker targetPicker;
        private ComboTracker comboTracker;
        private ICombatFactsSettings settings;
        private IMovementFrame frame;
        private CharacterBrain characterBrain;
        private Vector3 toTarget;
        private int lastWrittenComboCount = -1;
        private int lastWrittenComboTier = -1;
        private float lastWrittenTargetDistance = float.NaN;
        private int lastWrittenTargetSide = int.MinValue;
        private string lastWrittenTargetState;
        private bool? lastWrittenIsTargetBeyondLunge;
        private bool? lastWrittenIsIncomingAttackCounterable;

        public CombatFacts Facts { get; } = new CombatFacts();

        [Inject]
        private void Construct(
            ActionRunner runner,
            ICombatFactsSettings settings,
            IMovementFrame frame,
            IActionTargetPicker targetPicker)
        {
            this.runner = runner;
            this.settings = settings;
            this.frame = frame;
            this.targetPicker = targetPicker;
        }

        private void Awake()
        {
            characterBrain = GetComponent<CharacterBrain>();
            comboTracker = GetComponent<ComboTracker>();
        }

        public override void Tick(float deltaTime)
        {
            MeasureCombo();
            MeasureTarget(TargetOfTheNextPress(), runner.CurrentAttack);
            MeasureIncomingAttack();
            WriteChangedFactsToAgent();
        }

        /// <summary>Degrees between where the stick pointed at the press and the target. Zero with no stick or no target.</summary>
        public void UpdateStickAngle(Vector2 moveAtPress)
        {
            float angle = 0f;
            bool isStickPushed = moveAtPress.magnitude > settings.StickPushedMagnitude;
            bool canMeasureAngle = Facts.HasTarget && isStickPushed && toTarget.sqrMagnitude > NegligibleSqrMagnitude;
            if (canMeasureAngle)
            {
                Vector3 stick = frame.Frame * new Vector3(moveAtPress.x, 0f, moveAtPress.y);
                stick.y = 0f;
                angle = Vector3.SignedAngle(stick, toTarget, Vector3.up);
            }

            Facts.StickAngleToTarget = angle;
            Write(CombatFacts.Keys.StickAngleToTarget, angle);
        }

        public static TargetSide SideOf(Vector3 forward, Vector3 toTarget, float deadAheadAngle)
        {
            float rightwardAmount = forward.z * toTarget.x - forward.x * toTarget.z;
            float deadAheadSine = Mathf.Sin(deadAheadAngle * Mathf.Deg2Rad);
            float deadAheadLimit = deadAheadSine * forward.magnitude * toTarget.magnitude;
            if (rightwardAmount > deadAheadLimit)
            {
                return TargetSide.Right;
            }

            return rightwardAmount < -deadAheadLimit ? TargetSide.Left : TargetSide.DeadAhead;
        }

        private IActionTarget TargetOfTheNextPress() => targetPicker.PickTarget(characterBrain.Context.Input.Move);

        private void MeasureCombo()
        {
            bool hasComboTracker = comboTracker != null;
            Facts.ComboCount = hasComboTracker ? comboTracker.Count : 0;
            Facts.ComboTier = hasComboTracker ? comboTracker.Tier : 0;
        }

        private void MeasureTarget(IActionTarget target, AttackDefinition currentAttack)
        {
            Facts.HasTarget = target != null && target.IsValid;
            if (Facts.HasTarget)
            {
                DescribeTarget(target, currentAttack);
            }
            else
            {
                DescribeNoTarget();
            }
        }

        // Nothing attacks the player yet, so there is never an attack to counter.
        private void MeasureIncomingAttack() => Facts.IsIncomingAttackCounterable = false;

        private void DescribeTarget(IActionTarget target, AttackDefinition currentAttack)
        {
            toTarget = target.Position - transform.position;
            toTarget.y = 0f;

            Vector3 forward = transform.forward;
            forward.y = 0f;

            Facts.TargetDistance = toTarget.magnitude;
            Facts.TargetSide = SideOf(forward, toTarget, settings.DeadAheadAngle);
            Facts.TargetState = target is ICombatTarget combatant ? combatant.State : string.Empty;

            // The same lunge rule the warp refuses by; the fact describes the next press's target, the warp the current action's.
            Facts.IsTargetBeyondLunge = currentAttack != null
                ? currentAttack.IsBeyondLunge(transform.position, target.Position)
                : Facts.TargetDistance > settings.MaxLungeWhileIdle;
        }

        private void DescribeNoTarget()
        {
            toTarget = Vector3.zero;
            Facts.TargetDistance = float.PositiveInfinity;
            Facts.TargetSide = TargetSide.DeadAhead;
            Facts.TargetState = string.Empty;
            Facts.IsTargetBeyondLunge = true;
        }

        // Only changed values are written: each write boxes and goes through the agent's variables.
        private void WriteChangedFactsToAgent()
        {
            WriteIfChanged(CombatFacts.Keys.ComboCount, Facts.ComboCount, ref lastWrittenComboCount);
            WriteIfChanged(CombatFacts.Keys.ComboTier, Facts.ComboTier, ref lastWrittenComboTier);
            WriteIfChanged(CombatFacts.Keys.TargetDistance, Facts.TargetDistance, ref lastWrittenTargetDistance);
            WriteIfChanged(CombatFacts.Keys.TargetSide, (int)Facts.TargetSide, ref lastWrittenTargetSide);
            WriteIfChanged(CombatFacts.Keys.TargetState, Facts.TargetState, ref lastWrittenTargetState);
            WriteIfChanged(CombatFacts.Keys.TargetBeyondLunge, Facts.IsTargetBeyondLunge, ref lastWrittenIsTargetBeyondLunge);
            WriteIfChanged(CombatFacts.Keys.IncomingAttackCounterable, Facts.IsIncomingAttackCounterable, ref lastWrittenIsIncomingAttackCounterable);
        }

        private void WriteIfChanged<T>(string key, T value, ref T lastWritten) where T : IEquatable<T>
        {
            if (lastWritten != null && lastWritten.Equals(value))
            {
                return;
            }

            lastWritten = value;
            Write(key, value);
        }

        private void WriteIfChanged(string key, bool value, ref bool? lastWritten)
        {
            if (lastWritten == value)
            {
                return;
            }

            lastWritten = value;
            Write(key, value);
        }

        private void Write(string key, object value) => AgentVariableWriter.SetOn(gameObject, key, value, Source);
    }
}
