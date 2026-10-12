using System;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>Gravity is held while the dash travels so an evade does not dip.</summary>
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
                    PlanarVelocity = combatActions.TakeWarpVelocity(deltaTime),
                    Rotation = context.Transform.rotation,
                    SuppressGravity = Runner.IsTravelling
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
