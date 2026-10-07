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

                float maxLunge = currentAttack != null ? currentAttack.MaxLunge : config.IdleMaxLunge;
                context.TargetBeyondLunge = context.TargetDistance > maxLunge;
            }
            else
            {
                toTarget = Vector3.zero;
                context.TargetDistance = float.PositiveInfinity;
                context.TargetSide = 0;
                context.TargetState = string.Empty;
                context.TargetBeyondLunge = true;
            }

            Write(CombatContext.Keys.ComboCount, context.ComboCount);
            Write(CombatContext.Keys.ComboTier, context.ComboTier);
            Write(CombatContext.Keys.TargetDistance, context.TargetDistance);
            Write(CombatContext.Keys.TargetSide, context.TargetSide);
            Write(CombatContext.Keys.TargetState, context.TargetState);
            Write(CombatContext.Keys.TargetBeyondLunge, context.TargetBeyondLunge);
            Write(CombatContext.Keys.IncomingAttackCounterable, context.IncomingAttackCounterable);
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

        private void Write(string key, object value) => AgentVariableWriter.SetOn(agent, key, value, Source);
    }
}
