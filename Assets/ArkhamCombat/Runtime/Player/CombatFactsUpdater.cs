using System;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.Movement;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Fills the <see cref="CombatFacts"/> each frame and mirrors it onto the player's agent
    /// variables through the BH3 writer, so Functions read the same facts the resolver does and the
    /// keys show in Variable Watch. Target side and stick angle are character-relative.
    /// </summary>
    public sealed class CombatFactsUpdater
    {
        private const string Source = "CombatFacts";
        private const float NegligibleSqrMagnitude = 1e-4f;

        private const int TargetOnTheLeft = -1;
        private const int TargetDeadAhead = 0;
        private const int TargetOnTheRight = 1;

        // The sine of the angle either side of the facing that still counts as dead ahead (about six degrees).
        private const float DeadAheadSine = 0.1f;

        private readonly GameObject agent;
        private readonly Transform character;
        private readonly CombatFacts facts;
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

        public CombatFactsUpdater(
            CharacterMotor motor,
            CombatFacts facts,
            ComboMeter meter,
            CombatConfig config,
            IMovementFrame frame)
        {
            agent = motor.gameObject;
            character = motor.transform;
            this.facts = facts;
            this.meter = meter;
            this.config = config;
            this.frame = frame;
        }

        public void UpdateFacts(IActionTarget target, AttackDefinition currentAttack)
        {
            facts.ComboCount = meter.Count;
            facts.ComboTier = meter.Tier;
            facts.HasTarget = target != null && target.IsValid;
            facts.IsIncomingAttackCounterable = false;

            if (facts.HasTarget)
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
        public void UpdateStickAngle(Vector2 moveAtPress)
        {
            float angle = 0f;
            bool canMeasureAngle = facts.HasTarget
                                   && moveAtPress.sqrMagnitude > NegligibleSqrMagnitude
                                   && toTarget.sqrMagnitude > NegligibleSqrMagnitude;
            if (canMeasureAngle)
            {
                Vector3 stick = frame.Frame * new Vector3(moveAtPress.x, 0f, moveAtPress.y);
                stick.y = 0f;
                angle = Vector3.SignedAngle(stick, toTarget, Vector3.up);
            }

            facts.StickAngleToTarget = angle;
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

        private void DescribeTarget(IActionTarget target, AttackDefinition currentAttack)
        {
            toTarget = target.Position - character.position;
            toTarget.y = 0f;

            Vector3 forward = character.forward;
            forward.y = 0f;

            facts.TargetDistance = toTarget.magnitude;
            facts.TargetSide = SideOf(forward, toTarget);
            facts.TargetState = target is ICombatTarget combatant ? combatant.State : string.Empty;

            // The same rule the warp refuses by, so the fact and the overlay's warp-refused flag agree.
            facts.IsTargetBeyondLunge = currentAttack != null
                ? currentAttack.IsBeyondLunge(character.position, target.Position)
                : facts.TargetDistance > config.MaxLungeWhileIdle;
        }

        private void DescribeNoTarget()
        {
            toTarget = Vector3.zero;
            facts.TargetDistance = float.PositiveInfinity;
            facts.TargetSide = TargetDeadAhead;
            facts.TargetState = string.Empty;
            facts.IsTargetBeyondLunge = true;
        }

        // Only changed values are written: each write boxes and goes through the agent's variables.
        private void WriteChangedFactsToAgent()
        {
            WriteIfChanged(CombatFacts.Keys.ComboCount, facts.ComboCount, ref lastWrittenComboCount);
            WriteIfChanged(CombatFacts.Keys.ComboTier, facts.ComboTier, ref lastWrittenComboTier);
            WriteIfChanged(CombatFacts.Keys.TargetDistance, facts.TargetDistance, ref lastWrittenTargetDistance);
            WriteIfChanged(CombatFacts.Keys.TargetSide, facts.TargetSide, ref lastWrittenTargetSide);
            WriteIfChanged(CombatFacts.Keys.TargetState, facts.TargetState, ref lastWrittenTargetState);
            WriteIfChanged(CombatFacts.Keys.TargetBeyondLunge, facts.IsTargetBeyondLunge, ref lastWrittenIsTargetBeyondLunge);
            WriteIfChanged(CombatFacts.Keys.IncomingAttackCounterable, facts.IsIncomingAttackCounterable, ref lastWrittenIsIncomingAttackCounterable);
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
