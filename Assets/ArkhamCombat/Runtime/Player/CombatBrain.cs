using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Motor;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Ticks the combat side once per frame, before the character brain ticks the state machine:
    /// pick a target, publish the facts, run the meter and the runner, and push the state machine
    /// into Attacking when the runner starts a chain from idle. Leaving the state machine alone
    /// otherwise is what keeps Locomotion unaware that combat exists.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatBrain : MonoBehaviour
    {
        private CharacterBrain brain;
        private CharacterMotor motor;
        private ActionRunner runner;
        private CombatContextPublisher publisher;
        private StandInTargetPicker picker;
        private ComboMeter meter;

        public ActionRunner Runner => runner;

        [Inject]
        private void Construct(
            CharacterBrain brain,
            CharacterMotor motor,
            ActionRunner runner,
            CombatContextPublisher publisher,
            StandInTargetPicker picker,
            ComboMeter meter)
        {
            this.brain = brain;
            this.motor = motor;
            this.runner = runner;
            this.publisher = publisher;
            this.picker = picker;
            this.meter = meter;
        }

        private void Awake()
        {
            if (runner == null)
            {
                Debug.LogError(
                    $"[{nameof(CombatBrain)}] Nothing was injected. The scene needs a SceneContext and the combat installer.", this);
                enabled = false;
            }
        }

        private void Start() => picker.Refresh();

        private void Update()
        {
            CharacterStateMachine machine = brain.StateMachine;
            if (machine == null || !brain.enabled)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            TransformTarget target = picker.Pick(transform, brain.Context.Input.Move);
            runner.Target = target;
            publisher.Publish(target, runner.CurrentAttack);
            meter.Tick(deltaTime);

            bool canStartChain = machine.Current is LocomotionState && motor.Ground.IsGrounded;
            runner.Tick(deltaTime, transform.position, canStartChain);

            if (runner.IsPlaying && !(machine.Current is AttackingState))
            {
                machine.Change<AttackingState>();
            }
        }
    }
}
