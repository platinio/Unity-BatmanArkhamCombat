using System;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Movement input never moves it: the frame's planar velocity is whatever the runner's warp
    /// produced, and the character turns toward the target. Gravity is held during the warp so a
    /// lunge does not dip. A follow-up is not a state change; the runner swaps the action and this
    /// state keeps ticking, so StateChanged still means "started fighting" rather than "third
    /// punch".
    /// </summary>
    public sealed class AttackingState : ICharacterState
    {
        private const float NegligibleDeltaTime = 1e-6f;
        private const float NegligibleSqrDistance = 1e-4f;

        private readonly CharacterContext context;
        private readonly IFacingSettings settings;
        private CombatActions combatActions;

        public AttackingState(CharacterContext context, IFacingSettings settings)
        {
            this.context = context;
            this.settings = settings;
        }

        // Found on the character at first use, not handed to the constructor: the scene builds this
        // state and cannot see into the character's own context, where the runner lives.
        private CombatActions CombatActions
        {
            get
            {
                if (combatActions == null)
                {
                    combatActions = context.Transform.GetComponent<CombatActions>();
                }

                return combatActions;
            }
        }

        private ActionRunner Runner => CombatActions.Runner;

        public void Enter() { }

        // Warp movement left over from this attack must not carry into the next one.
        public void Exit() => CombatActions.TakePendingWarpMovement();

        public void Tick(float deltaTime)
        {
            if (!Runner.IsPlaying)
            {
                context.StateMachine.Change<LocomotionState>();
                return;
            }

            context.Motor.Tick(
                new MotionIntent
                {
                    PlanarVelocity = TakeWarpVelocity(deltaTime),
                    Rotation = TurnTowardTarget(deltaTime),
                    SuppressGravity = Runner.IsWarpWindowOpen
                },
                deltaTime);
        }

        private Vector3 TakeWarpVelocity(float deltaTime)
        {
            Vector3 warpMovement = CombatActions.TakePendingWarpMovement();
            return deltaTime > NegligibleDeltaTime ? warpMovement / deltaTime : Vector3.zero;
        }

        private Quaternion TurnTowardTarget(float deltaTime)
        {
            IActionTarget target = Runner.CurrentActionTarget;
            Quaternion currentRotation = context.Transform.rotation;

            if (target == null || !target.IsValid)
            {
                return currentRotation;
            }

            Vector3 toTarget = target.Position - context.Transform.position;
            toTarget.y = 0f;
            bool isStandingOnTarget = toTarget.sqrMagnitude < NegligibleSqrDistance;
            if (isStandingOnTarget)
            {
                return currentRotation;
            }

            float yawToTarget = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float smoothedYaw = Mathf.SmoothDampAngle(
                context.Transform.eulerAngles.y,
                yawToTarget,
                ref context.TurnVelocity,
                settings.FaceTargetSmoothTime,
                float.MaxValue,
                deltaTime);

            return Quaternion.Euler(0f, smoothedYaw, 0f);
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
