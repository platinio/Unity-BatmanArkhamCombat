using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class EvadingStateSwitch : CharacterComponent
    {
        private ActionRunner runner;
        private CharacterBrain characterBrain;

        [Inject]
        private void Construct(ActionRunner runner) => this.runner = runner;

        private void Awake() => characterBrain = GetComponent<CharacterBrain>();

        public override void Tick(float deltaTime) => EnterEvadingStateWhileADashPlays();

        internal static bool ShouldEnterEvadingWhen(DashDefinition currentDash, ICharacterState currentState)
        {
            bool isADashPlaying = currentDash != null;
            return isADashPlaying && currentState.CanBeInterrupted;
        }

        private void EnterEvadingStateWhileADashPlays()
        {
            if (ShouldEnterEvadingWhen(runner.CurrentDash, characterBrain.StateMachine.Current))
            {
                characterBrain.StateMachine.Change<EvadingState>();
            }
        }
    }
}
