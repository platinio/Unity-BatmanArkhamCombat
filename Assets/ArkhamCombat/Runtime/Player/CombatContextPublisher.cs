using System;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Fills the <see cref="CombatContext"/> each frame and mirrors it onto the player's agent
    /// variables through the BH3 writer, so Functions read the same facts the resolver does and the
    /// keys show in Variable Watch. Target side and stick angle are character-relative.
    /// </summary>
    public sealed class CombatContextPublisher
    {
        private const string Source = "CombatContext";
        private const float NegligibleSqrMagnitude = 1e-4f;

        private const int TargetOnTheLeft = -1;
        private const int TargetDeadAhead = 0;
        private const int TargetOnTheRight = 1;

        // The sine of the angle either side of the facing that still counts as dead ahead (about six degrees).
        private const float DeadAheadSine = 0.1f;

        private readonly GameObject agent;
        private readonly Transform character;
        private readonly CombatContext context;
        private readonly ComboMeter meter;
        private readonly CombatConfig config;
        private readonly IMovementFrame frame;
        private Vector3 toTarget;
        private int lastWrittenComboCount = -1;
        private int lastWrittenComboTier = -1;
        private float lastWrittenTargetDistance = float.NaN;
        private int lastWrittenTargetSide = int.MinValue;
        private string lastWrittenTargetState;
        private bool? lastWrittenIsTargetBeyondLunge;
        private bool? lastWrittenIsIncomingAttackCounterable;

        public CombatContextPublisher(
            CharacterMotor motor,
            CombatContext context,
            ComboMeter meter,
            CombatConfig config,
            IMovementFrame frame)
        {
            agent = motor.gameObject;
            character = motor.transform;
            this.context = context;
            this.meter = meter;
            this.config = config;
            this.frame = frame;
        }

        public void Publish(IActionTarget target, AttackDefinition currentAttack)
        {
            context.ComboCount = meter.Count;
            context.ComboTier = meter.Tier;
            context.HasTarget = target != null && target.IsValid;
            context.IsIncomingAttackCounterable = false;

            if (context.HasTarget)
            {
                DescribeTarget(target, currentAttack);
            }
            else
            {
                DescribeNoTarget();
            }

            WriteChangedFactsToAgent();
        }

        /// <summary>Degrees between where the stick pointed at the press and the target. Zero with no stick or no target.</summary>
        public void PublishStickAngle(Vector2 moveAtPress)
        {
            float angle = 0f;
            bool canMeasureAngle = context.HasTarget
                                   && moveAtPress.sqrMagnitude > NegligibleSqrMagnitude
                                   && toTarget.sqrMagnitude > NegligibleSqrMagnitude;
            if (canMeasureAngle)
            {
                Vector3 stick = frame.Frame * new Vector3(moveAtPress.x, 0f, moveAtPress.y);
                stick.y = 0f;
                angle = Vector3.SignedAngle(stick, toTarget, Vector3.up);
            }

            context.StickAngleToTarget = angle;
            Write(CombatContext.Keys.StickAngleToTarget, angle);
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

        private void DescribeTarget(IActionTarget target, AttackDefinition currentAttack)
        {
            toTarget = target.Position - character.position;
            toTarget.y = 0f;

            Vector3 forward = character.forward;
            forward.y = 0f;

            context.TargetDistance = toTarget.magnitude;
            context.TargetSide = SideOf(forward, toTarget);
            context.TargetState = target is ICombatTarget combatant ? combatant.State : string.Empty;

            // The same rule the warp refuses by, so the fact and the overlay's warp-refused flag agree.
            context.IsTargetBeyondLunge = currentAttack != null
                ? currentAttack.IsBeyondLunge(character.position, target.Position)
                : context.TargetDistance > config.MaxLungeWhileIdle;
        }

        private void DescribeNoTarget()
        {
            toTarget = Vector3.zero;
            context.TargetDistance = float.PositiveInfinity;
            context.TargetSide = TargetDeadAhead;
            context.TargetState = string.Empty;
            context.IsTargetBeyondLunge = true;
        }

        // Only changed values are written: each write boxes and goes through the agent's variables.
        private void WriteChangedFactsToAgent()
        {
            WriteIfChanged(CombatContext.Keys.ComboCount, context.ComboCount, ref lastWrittenComboCount);
            WriteIfChanged(CombatContext.Keys.ComboTier, context.ComboTier, ref lastWrittenComboTier);
            WriteIfChanged(CombatContext.Keys.TargetDistance, context.TargetDistance, ref lastWrittenTargetDistance);
            WriteIfChanged(CombatContext.Keys.TargetSide, context.TargetSide, ref lastWrittenTargetSide);
            WriteIfChanged(CombatContext.Keys.TargetState, context.TargetState, ref lastWrittenTargetState);
            WriteIfChanged(CombatContext.Keys.TargetBeyondLunge, context.IsTargetBeyondLunge, ref lastWrittenIsTargetBeyondLunge);
            WriteIfChanged(CombatContext.Keys.IncomingAttackCounterable, context.IsIncomingAttackCounterable, ref lastWrittenIsIncomingAttackCounterable);
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

        private void Write(string key, object value) => AgentVariableWriter.SetOn(agent, key, value, Source);
    }
}
