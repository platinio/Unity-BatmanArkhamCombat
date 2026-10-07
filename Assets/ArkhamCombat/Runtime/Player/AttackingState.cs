using System;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The character while an action plays. Movement input never displaces it: the frame's planar
    /// velocity is whatever the runner's warp produced, and the character turns toward the target.
    /// Gravity is held during the warp so a lunge does not dip. A follow-up is not a state change;
    /// the runner swaps the action and this state keeps ticking, so StateChanged still means
    /// "started fighting" rather than "third punch".
    /// </summary>
    public sealed class AttackingState : ICharacterState
    {
        private readonly CharacterContext context;
        private readonly ActionRunner runner;
        private readonly MotorDisplacementSink displacement;
        private readonly CombatConfig config;

        public AttackingState(
            CharacterContext context,
            ActionRunner runner,
            MotorDisplacementSink displacement,
            CombatConfig config)
        {
            this.context = context;
            this.runner = runner;
            this.displacement = displacement;
            this.config = config;
        }

        public void Enter() { }

        public void Exit() => displacement.Take();

        public void Tick(float deltaTime)
        {
            if (!runner.IsPlaying)
            {
                context.StateMachine.Change<LocomotionState>();
                return;
            }

            CharacterMotor motor = context.Motor;

            Vector3 delta = displacement.Take();
            Vector3 velocity = deltaTime > 1e-6f ? delta / deltaTime : Vector3.zero;

            motor.Tick(
                new MotionIntent
                {
                    PlanarVelocity = velocity,
                    Rotation = ResolveRotation(deltaTime),
                    SuppressGravity = runner.WarpOpen
                },
                deltaTime);
        }

        /// <summary>Faces the target while one is valid, otherwise holds the current heading.</summary>
        private Quaternion ResolveRotation(float deltaTime)
        {
            IActionTarget target = runner.Target;
            Quaternion current = context.Transform.rotation;

            if (target == null || !target.IsValid)
            {
                return current;
            }

            Vector3 toTarget = target.Position - context.Transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-4f)
            {
                return current;
            }

            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(
                context.Transform.eulerAngles.y,
                targetYaw,
                ref context.TurnVelocity,
                config.FaceTurnSmoothTime,
                float.MaxValue,
                deltaTime);

            return Quaternion.Euler(0f, yaw, 0f);
        }
    }

    /// <summary>Picked in the player installer's state list, next to Locomotion and Airborne.</summary>
    [Serializable]
    public sealed class AttackingStateBinding : ICharacterStateBinding
    {
        public void Install(DiContainer container) =>
            container.Bind<ICharacterState>().To<AttackingState>().AsSingle();
    }
}
