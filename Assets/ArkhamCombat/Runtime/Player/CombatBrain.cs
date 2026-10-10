using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Runs the combat side once per frame, before the character brain ticks the state machine: it
    /// ticks the character's combat components in the order of its list, and that is all it does.
    /// Because this runs first, the combat components see the previous frame's stick and a press
    /// expires one tick late; at any playable frame rate neither is visible.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatBrain : MonoBehaviour
    {
        [Tooltip("The per-frame combat jobs on this character, ticked top to bottom. A job sees " +
                 "what the jobs above it wrote this frame.")]
        [SerializeField] private List<CombatComponent> combatComponents = new List<CombatComponent>();

        private CharacterBrain characterBrain;

        private bool IsCharacterBrainRunning => characterBrain.StateMachine != null && characterBrain.enabled;

        private void Awake()
        {
            // The brain on this object, not whichever one the container found in the scene.
            characterBrain = GetComponent<CharacterBrain>();

            ReportCombatComponentErrors();
        }

        private void Reset() => combatComponents = new List<CombatComponent>(GetComponents<CombatComponent>());

        private void Update()
        {
            if (!IsCharacterBrainRunning)
            {
                return;
            }

            TickCombatComponents(Time.deltaTime);
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
    }
}
