using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Keeps the character's state machine and its actions in step. It puts the state machine into
    /// Attacking while an action plays, and it is the start gate that holds a press back while the
    /// character is in the air or in a state that cannot attack. Entering Attacking is all it ever
    /// does to the state machine; leaving it alone otherwise is what keeps Locomotion unaware that
    /// combat exists.
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
            // The brain and the motor on this object, not whichever ones the container found in the scene.
            characterBrain = GetComponent<CharacterBrain>();
            motor = GetComponent<CharacterMotor>();
        }

        public override void Tick(float deltaTime) => EnterAttackingStateWhileAnActionPlays();

        // Attacking counts too: an idle runner while still in Attacking is recovery, so a queued press
        // continues the combo without a one-frame trip through Locomotion.
        internal static bool CanStartFromIdleWhen(bool isGrounded, ICharacterState currentState) =>
            isGrounded && (currentState is LocomotionState || currentState is AttackingState);

        private void EnterAttackingStateWhileAnActionPlays()
        {
            if (runner.IsPlaying && !IsInAttackingState())
            {
                characterBrain.StateMachine.Change<AttackingState>();
            }
        }

        private bool IsInAttackingState() => characterBrain.StateMachine.Current is AttackingState;
    }
}
