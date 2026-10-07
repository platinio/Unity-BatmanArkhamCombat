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

        private readonly GameObject agent;
        private readonly Transform character;
        private readonly CombatContext context;
        private readonly ComboMeter meter;
        private readonly CombatConfig config;
        private readonly IMovementFrame frame;
        private Vector3 toTarget;
        private int lastComboCount = -1;
        private int lastComboTier = -1;
        private float lastTargetDistance = float.NaN;
        private int lastTargetSide = int.MinValue;
        private string lastTargetState;
        private bool? lastTargetBeyondLunge;
        private bool? lastIncomingCounterable;

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
            context.IncomingAttackCounterable = false;

            if (context.HasTarget)
            {
                toTarget = target.Position - character.position;
                toTarget.y = 0f;

                Vector3 forward = character.forward;
                forward.y = 0f;

                context.TargetDistance = toTarget.magnitude;
                context.TargetSide = SideOf(forward, toTarget);
                TransformTarget scene = target as TransformTarget;
                context.TargetState = scene?.Dummy != null ? scene.Dummy.State : string.Empty;

                // The same rule the warp refuses by, so the fact and the overlay's warp-refused flag agree.
                context.TargetBeyondLunge = currentAttack != null
                    ? currentAttack.IsBeyondLunge(character.position, target.Position)
                    : context.TargetDistance > config.IdleMaxLunge;
            }
            else
            {
                toTarget = Vector3.zero;
                context.TargetDistance = float.PositiveInfinity;
                context.TargetSide = 0;
                context.TargetState = string.Empty;
                context.TargetBeyondLunge = true;
            }

            // Only changed values are written: each write boxes and goes through the agent's variables.
            WriteIfChanged(CombatContext.Keys.ComboCount, context.ComboCount, ref lastComboCount);
            WriteIfChanged(CombatContext.Keys.ComboTier, context.ComboTier, ref lastComboTier);
            WriteIfChanged(CombatContext.Keys.TargetDistance, context.TargetDistance, ref lastTargetDistance);
            WriteIfChanged(CombatContext.Keys.TargetSide, context.TargetSide, ref lastTargetSide);
            WriteIfChanged(CombatContext.Keys.TargetState, context.TargetState, ref lastTargetState);
            WriteIfChanged(CombatContext.Keys.TargetBeyondLunge, context.TargetBeyondLunge, ref lastTargetBeyondLunge);
            WriteIfChanged(CombatContext.Keys.IncomingAttackCounterable, context.IncomingAttackCounterable, ref lastIncomingCounterable);
        }

        /// <summary>Degrees between where the stick pointed at the press and the target. Zero with no stick or no target.</summary>
        public void PublishStickAngle(Vector2 moveAtPress)
        {
            float angle = 0f;
            if (context.HasTarget && moveAtPress.sqrMagnitude > 1e-4f && toTarget.sqrMagnitude > 1e-4f)
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
            float cross = forward.z * toTarget.x - forward.x * toTarget.z;
            float deadBand = 0.1f * forward.magnitude * toTarget.magnitude;
            if (cross > deadBand)
            {
                return 1;
            }

            return cross < -deadBand ? -1 : 0;
        }

        private void WriteIfChanged<T>(string key, T value, ref T last) where T : IEquatable<T>
        {
            if (last != null && last.Equals(value))
            {
                return;
            }

            last = value;
            Write(key, value);
        }

        private void WriteIfChanged(string key, bool value, ref bool? last)
        {
            if (last == value)
            {
                return;
            }

            last = value;
            Write(key, value);
        }

        private void Write(string key, object value) => AgentVariableWriter.SetOn(agent, key, value, Source);
    }
}
