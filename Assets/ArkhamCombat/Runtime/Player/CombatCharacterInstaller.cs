using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.TPCharacterController.Motor;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// One character's combat, bound in the GameObjectContext on that character: its stance, its
    /// runner and everything the runner talks to. What every character shares (the config, the
    /// interrupt kinds, the combat events, the roster, the target scorer) comes from the scene's
    /// <see cref="CombatStaticInstaller"/>. An optional piece is bound when its component is on the
    /// character and replaced by one that does nothing when it is not, so a character has exactly
    /// the combat it was given.
    /// </summary>
    [RequireComponent(typeof(CharacterBrain))]
    public sealed class CombatCharacterInstaller : MonoInstaller
    {
        [Tooltip("The chain this character fights with.")]
        [SerializeField] private Stance stance;

        public override void InstallBindings()
        {
            ReportAuthoringErrors();

            BindMotor();
            BindStance();
            BindPresses();
            BindFacts();
            BindConditions();
            BindPresentation();
            BindWarpAndHits();
            BindTargeting();
            BindStartGate();
            BindRunner();
        }

        // This character's motor, so nothing bound here reaches for whichever one the scene found.
        private void BindMotor() => Container.Bind<CharacterMotor>().FromInstance(GetComponent<CharacterMotor>());

        private void BindStance() => Container.Bind<Stance>().FromInstance(stance);

        private void BindPresses()
        {
            Container.Bind<IntentBuffer>().FromResolveGetter<ICharacterInput>(input => input.Intents).AsSingle();
        }

        private void BindFacts()
        {
            if (TryGetComponent(out CombatFactsUpdater factsUpdater))
            {
                // Not FromInstance, which injects the updater the moment something asks for it. The
                // updater needs the runner, and the one asking may be the runner being built (through
                // its condition evaluator). The character's context injects the updater on its own.
                Container.Bind<CombatFactsUpdater>().FromMethod(injectContext => factsUpdater);
                Container.Bind<CombatFacts>().FromInstance(factsUpdater.Facts);
            }
            else
            {
                // Left empty: the counter rule then never allows a counter and the target-side pool
                // falls back to its no-side pick.
                Container.Bind<CombatFacts>().FromInstance(new CombatFacts());
            }
        }

        /// <summary>A character whose stance has no condition never touches Visual Scripting.</summary>
        private void BindConditions()
        {
            if (HasAnyCondition(stance))
            {
                Container.Bind<IConditionEvaluator>().To<FunctionConditionEvaluator>().AsSingle();
            }
            else
            {
                Container.Bind<IConditionEvaluator>().To<AlwaysConditionEvaluator>().AsSingle();
            }
        }

        private void BindPresentation()
        {
            IPresentationDriver driverOnTheBody = GetComponentInChildren<IPresentationDriver>();
            bool hasBody = driverOnTheBody != null;

            Container.Bind<IPresentationDriver>().FromInstance(hasBody ? driverOnTheBody : new NullPresentationDriver());
        }

        private void BindWarpAndHits()
        {
            Container.Bind(typeof(MotorWarpMover), typeof(IWarpMover)).To<MotorWarpMover>().AsSingle();
            Container.Bind<IHitWindowListener>().To<DemoHitWindowListener>().AsSingle();
        }

        private void BindTargeting()
        {
            bool hasTargeting = TryGetComponent(out IActionTargetPicker targetPickerOnTheCharacter);

            Container.Bind<IActionTargetPicker>()
                .FromInstance(hasTargeting ? targetPickerOnTheCharacter : new NullActionTargetPicker());
        }

        private void BindStartGate()
        {
            bool hasStartGate = TryGetComponent(out IActionStartGate startGateOnTheCharacter);

            Container.Bind<IActionStartGate>()
                .FromInstance(hasStartGate ? startGateOnTheCharacter : new AlwaysOpenActionStartGate());
        }

        private void BindRunner() => Container.Bind<ActionRunner>().AsSingle();

        internal static bool HasAnyCondition(Stance stance)
        {
            if (stance == null)
            {
                return false;
            }

            foreach (ChainNode node in stance.Nodes)
            {
                if (node != null && HasAnyCondition(node.Edges))
                {
                    return true;
                }
            }

            return HasAnyCondition(stance.GlobalEdges);
        }

        private static bool HasAnyCondition(IReadOnlyList<Edge> edges)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i] != null && edges[i].HasCondition)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validates the stance and its attacks once as the character's context is built, so a
        /// reversed window or an edge to nowhere is an error in the console rather than a hit window
        /// that never closes.
        /// </summary>
        private void ReportAuthoringErrors()
        {
            if (stance == null)
            {
                Debug.LogError($"[{nameof(CombatCharacterInstaller)}] No stance assigned on '{name}'.", this);
                return;
            }

            List<string> errors = new List<string>();
            stance.Validate(errors);
            foreach (AttackDefinition attack in AttacksIn(stance))
            {
                attack.Validate(errors);
            }

            foreach (string error in errors)
            {
                Debug.LogError($"[{nameof(CombatCharacterInstaller)}] {error}", stance);
            }
        }

        /// <summary>Each attack once, whether a node plays it directly or picks it from a pool.</summary>
        private static HashSet<AttackDefinition> AttacksIn(Stance stance)
        {
            HashSet<AttackDefinition> attacks = new HashSet<AttackDefinition>();
            foreach (ChainNode node in stance.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (node.Attack != null)
                {
                    attacks.Add(node.Attack);
                }

                if (node.Pool != null)
                {
                    foreach (AttackDefinition pooledAttack in node.Pool.Attacks)
                    {
                        if (pooledAttack != null)
                        {
                            attacks.Add(pooledAttack);
                        }
                    }
                }
            }

            return attacks;
        }
    }
}
