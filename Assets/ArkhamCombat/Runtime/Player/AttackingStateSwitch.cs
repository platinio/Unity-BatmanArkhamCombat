using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Entering Attacking is all it ever does to the state machine; leaving it alone otherwise is
    /// what keeps Locomotion unaware that combat exists.
    /// </summary>
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class AttackingStateSwitch : CharacterComponent, IActionStartGate
    {
        private ActionRunner runner;
        private CharacterBrain characterBrain;
        private CharacterMotor motor;

        public bool CanStartFromIdle =>
            CanStartFromIdleWhen(motor.Ground.IsGrounded, characterBrain.StateMachine.Current);

        [Inject]
        private void Construct(ActionRunner runner) => this.runner = runner;

        private void Awake()
        {
            characterBrain = GetComponent<CharacterBrain>();
            motor = GetComponent<CharacterMotor>();
        }

        public override void Tick(float deltaTime) => EnterAttackingStateWhileAnAttackPlays();

        internal static bool CanStartFromIdleWhen(bool isGrounded, ICharacterState currentState)
        {
            bool hasAState = currentState != null;
            return isGrounded && hasAState && currentState.CanBeInterrupted;
        }

        internal static bool ShouldEnterAttackingWhen(AttackDefinition currentAttack, ICharacterState currentState)
        {
            bool isAnAttackPlaying = currentAttack != null;
            return isAnAttackPlaying && currentState.CanBeInterrupted;
        }

        private void EnterAttackingStateWhileAnAttackPlays()
        {
            if (ShouldEnterAttackingWhen(runner.CurrentAttack, characterBrain.StateMachine.Current))
            {
                characterBrain.StateMachine.Change<AttackingState>();
            }
        }
    }
}
