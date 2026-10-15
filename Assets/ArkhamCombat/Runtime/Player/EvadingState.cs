using System;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using Zenject;

namespace ArkhamCombat.Player
{
    public sealed class EvadingState : ICharacterState
    {
        private readonly CharacterContext context;
        private readonly CombatActions combatActions;

        public EvadingState(CharacterContext context, CombatActions combatActions)
        {
            this.context = context;
            this.combatActions = combatActions;
        }

        private ActionRunner Runner => combatActions.Runner;

        public void Enter() { }

        // Movement left over from this evade must not carry into the strike that follows it.
        public void Exit() => combatActions.TakePendingWarpMovement();

        // The runner has ticked already, so the frame a dash ends on still has movement to apply before leaving.
        public void Tick(float deltaTime)
        {
            MoveByWhatTheDashCoveredThisFrame(deltaTime);

            if (!Runner.IsPlaying)
            {
                context.StateMachine.Change<LocomotionState>();
            }
        }

        private void MoveByWhatTheDashCoveredThisFrame(float deltaTime)
        {
            bool shouldHoldGravitySoTheEvadeDoesNotDip = Runner.IsTravelling;

            context.Motor.Tick(
                new MotionIntent
                {
                    PlanarVelocity = combatActions.TakeWarpVelocity(deltaTime),
                    Rotation = context.Transform.rotation,
                    SuppressGravity = shouldHoldGravitySoTheEvadeDoesNotDip
                },
                deltaTime);
        }
    }

    [Serializable]
    public sealed class EvadingStateBinding : ICharacterStateBinding
    {
        public void Install(DiContainer container) =>
            container.Bind<ICharacterState>().To<EvadingState>().AsSingle();
    }
}
