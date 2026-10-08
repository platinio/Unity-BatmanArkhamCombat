using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Runs the combat side once per frame, before the character brain ticks the state machine.
    /// It only pushes the state machine into Attacking when an action starts; leaving it alone
    /// otherwise is what keeps Locomotion unaware that combat exists. Because this runs first, the
    /// target picker sees the previous frame's stick and a press expires one tick late; at any
    /// playable frame rate neither is visible.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatBrain : MonoBehaviour
    {
        private const float StickPushedSqrMagnitude = 0.01f;

        private CharacterBrain characterBrain;
        private CharacterMotor motor;
        private ActionRunner runner;
        private CombatContextPublisher publisher;
        private ITargetPicker targetPicker;
        private ComboMeter meter;

        public ActionRunner Runner => runner;

        private bool IsCharacterBrainRunning => characterBrain.StateMachine != null && characterBrain.enabled;

        [Inject]
        private void Construct(
            CharacterMotor motor,
            ActionRunner runner,
            CombatContextPublisher publisher,
            ITargetPicker targetPicker,
            ComboMeter meter)
        {
            this.motor = motor;
            this.runner = runner;
            this.publisher = publisher;
            this.targetPicker = targetPicker;
            this.meter = meter;
        }

        private void Awake()
        {
            // The brain on this object, not whichever one the container found in the scene.
            characterBrain = GetComponent<CharacterBrain>();

            if (runner == null)
            {
                Debug.LogError(
                    $"[{nameof(CombatBrain)}] Nothing was injected. The scene needs a SceneContext and the combat installer.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!IsCharacterBrainRunning)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            PickTarget();
            publisher.Publish(runner.Target, runner.CurrentAttack);
            meter.Tick(deltaTime);
            runner.Tick(deltaTime, transform.position, CanStartActionFromIdle());
            EnterAttackingStateWhileAnActionPlays();
        }

        private void PickTarget() => runner.Target = targetPicker.Pick(transform.position, PreferredDirection());

        /// <summary>The stick, in the movement frame, while it is pushed; the facing otherwise.</summary>
        private Vector3 PreferredDirection()
        {
            Vector2 move = characterBrain.Context.Input.Move;
            bool isStickPushed = move.sqrMagnitude > StickPushedSqrMagnitude;
            if (isStickPushed)
            {
                return characterBrain.Context.MovementFrame.Frame * new Vector3(move.x, 0f, move.y);
            }

            return transform.forward;
        }

        // Attacking counts too: an idle runner while still in Attacking is recovery, so a queued press
        // continues the combo without a one-frame trip through Locomotion.
        private bool CanStartActionFromIdle() =>
            motor.Ground.IsGrounded && (IsInState<LocomotionState>() || IsInState<AttackingState>());

        private void EnterAttackingStateWhileAnActionPlays()
        {
            if (runner.IsPlaying && !IsInState<AttackingState>())
            {
                characterBrain.StateMachine.Change<AttackingState>();
            }
        }

        private bool IsInState<TState>() where TState : ICharacterState => characterBrain.StateMachine.Current is TState;
    }
}
