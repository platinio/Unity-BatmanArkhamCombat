using System;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Measures this character's <see cref="CombatFacts"/> each tick and mirrors them onto its agent
    /// variables through the BH3 writer, so Functions read the same facts the resolver does and the
    /// keys show in Variable Watch. The target facts describe the target the next press would get,
    /// picked with the stick as it is now. Target side and stick angle are character-relative. A
    /// character without a <see cref="ComboTracker"/> reports a combo of zero.
    /// </summary>
    public sealed class CombatFactsUpdater : CombatComponent
    {
        private const string Source = "CombatFacts";
        private const float NegligibleSqrMagnitude = 1e-4f;

        private const int TargetOnTheLeft = -1;
        private const int TargetDeadAhead = 0;
        private const int TargetOnTheRight = 1;

        // The sine of the angle either side of the facing that still counts as dead ahead (about six degrees).
        private const float DeadAheadSine = 0.1f;

        private ActionRunner runner;
        private IActionTargetPicker targetPicker;
        private ComboTracker comboTracker;
        private CombatConfig config;
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
            CombatConfig config,
            IMovementFrame frame,
            [InjectOptional] IActionTargetPicker targetPicker)
        {
            this.runner = runner;
            this.config = config;
            this.frame = frame;
            this.targetPicker = targetPicker ?? new NullActionTargetPicker();
        }

        private void Awake()
        {
            // The brain on this object, not whichever one the container found in the scene.
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
            bool canMeasureAngle = Facts.HasTarget
                                   && moveAtPress.sqrMagnitude > NegligibleSqrMagnitude
                                   && toTarget.sqrMagnitude > NegligibleSqrMagnitude;
            if (canMeasureAngle)
            {
                Vector3 stick = frame.Frame * new Vector3(moveAtPress.x, 0f, moveAtPress.y);
                stick.y = 0f;
                angle = Vector3.SignedAngle(stick, toTarget, Vector3.up);
            }

            Facts.StickAngleToTarget = angle;
            Write(CombatFacts.Keys.StickAngleToTarget, angle);
        }

        /// <summary>-1 when the target is left of the facing, 1 when right, 0 inside a narrow dead-ahead band.</summary>
        public static int SideOf(Vector3 forward, Vector3 toTarget)
        {
            float rightwardAmount = forward.z * toTarget.x - forward.x * toTarget.z;
            float deadAheadLimit = DeadAheadSine * forward.magnitude * toTarget.magnitude;
            if (rightwardAmount > deadAheadLimit)
            {
                return TargetOnTheRight;
            }

            return rightwardAmount < -deadAheadLimit ? TargetOnTheLeft : TargetDeadAhead;
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
            Facts.TargetSide = SideOf(forward, toTarget);
            Facts.TargetState = target is ICombatTarget combatant ? combatant.State : string.Empty;

            // The same lunge rule the warp refuses by; the fact describes the next press's target, the warp the current action's.
            Facts.IsTargetBeyondLunge = currentAttack != null
                ? currentAttack.IsBeyondLunge(transform.position, target.Position)
                : Facts.TargetDistance > config.MaxLungeWhileIdle;
        }

        private void DescribeNoTarget()
        {
            toTarget = Vector3.zero;
            Facts.TargetDistance = float.PositiveInfinity;
            Facts.TargetSide = TargetDeadAhead;
            Facts.TargetState = string.Empty;
            Facts.IsTargetBeyondLunge = true;
        }

        // Only changed values are written: each write boxes and goes through the agent's variables.
        private void WriteChangedFactsToAgent()
        {
            WriteIfChanged(CombatFacts.Keys.ComboCount, Facts.ComboCount, ref lastWrittenComboCount);
            WriteIfChanged(CombatFacts.Keys.ComboTier, Facts.ComboTier, ref lastWrittenComboTier);
            WriteIfChanged(CombatFacts.Keys.TargetDistance, Facts.TargetDistance, ref lastWrittenTargetDistance);
            WriteIfChanged(CombatFacts.Keys.TargetSide, Facts.TargetSide, ref lastWrittenTargetSide);
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
