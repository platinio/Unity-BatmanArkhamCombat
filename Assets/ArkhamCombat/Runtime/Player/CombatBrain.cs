using System.Collections.Generic;
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
    /// combat components see the previous frame's stick and a press expires one tick late; at any
    /// playable frame rate neither is visible.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatBrain : MonoBehaviour
    {
        [Tooltip("The per-frame combat jobs on this character, ticked top to bottom before the " +
                 "runner. A job sees what the jobs above it wrote this frame.")]
        [SerializeField] private List<CombatComponent> combatComponents = new List<CombatComponent>();

        private CharacterBrain characterBrain;
        private CharacterMotor motor;
        private ActionRunner runner;
        private ComboMeter meter;

        public ActionRunner Runner => runner;

        private bool IsCharacterBrainRunning => characterBrain.StateMachine != null && characterBrain.enabled;

        [Inject]
        private void Construct(
            CharacterMotor motor,
            ActionRunner runner,
            ComboMeter meter)
        {
            this.motor = motor;
            this.runner = runner;
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

            ReportCombatComponentErrors();
        }

        private void Reset() => combatComponents = new List<CombatComponent>(GetComponents<CombatComponent>());

        private void Update()
        {
            if (!IsCharacterBrainRunning)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            TickCombatComponents(deltaTime);
            meter.Tick(deltaTime);
            runner.Tick(deltaTime, transform.position, CanStartActionFromIdle());
            EnterAttackingStateWhileAnActionPlays();
        }

        internal void TickCombatComponents(float deltaTime)
        {
            for (int i = 0; i < combatComponents.Count; i++)
            {
                CombatComponent combatComponent = combatComponents[i];
                if (combatComponent != null)
                {
                    combatComponent.Tick(deltaTime);
                }
            }
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

        private void ReportCombatComponentErrors()
        {
            for (int i = 0; i < combatComponents.Count; i++)
            {
                CombatComponent combatComponent = combatComponents[i];
                if (combatComponent == null)
                {
                    Debug.LogError(
                        $"[{nameof(CombatBrain)}] Entry {i} of the combat components on '{name}' is empty and will be skipped.", this);
                }
                else if (combatComponent.gameObject != gameObject)
                {
                    Debug.LogError(
                        $"[{nameof(CombatBrain)}] The {combatComponent.GetType().Name} in the combat components on '{name}' " +
                        $"is on '{combatComponent.name}'. A combat component belongs on the character it serves.", this);
                }
            }
        }

        private bool IsInState<TState>() where TState : ICharacterState => characterBrain.StateMachine.Current is TState;
    }
}
