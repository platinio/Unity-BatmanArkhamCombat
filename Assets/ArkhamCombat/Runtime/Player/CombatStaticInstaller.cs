using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArcaneOnyx.UnityExtensions;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The player's combat, bound scene-wide beside the character installer. Where combat events go
    /// is picked on this asset, so swapping the logging stub for the Hermes adapter is an inspector
    /// change.
    /// </summary>
    [AutoAssetGeneration("Installers/Static", "CombatStaticInstaller")]
    [StaticInstaller(StaticInstallerExecutionOrder.Normal)]
    public sealed class CombatStaticInstaller : ScriptableObjectInstaller
    {
        [Tooltip("The stance, the meter and the stand-in tunables.")]
        [SerializeField] private CombatConfig config;

        [Tooltip("Where presentation events go. The logging stub until the Hermes adapter exists.")]
        [SerializeReference, SubclassSelector] private ICombatEvents events = new LoggingCombatEvents();

        public override void InstallBindings()
        {
            ReportAuthoringErrors();

            BindSettings();
            BindComboState();
            BindConditions();
            BindRunner();
            BindTargeting();
        }

        private void BindSettings()
        {
            Container.Bind<CombatConfig>().FromInstance(config);
            Container.Bind<Stance>().FromResolveGetter<CombatConfig>(combatConfig => combatConfig != null ? combatConfig.Stance : null);
            Container.Bind<ICombatEvents>().FromInstance(events ?? new NullCombatEvents());
        }

        private void BindComboState()
        {
            Container.Bind<CombatContext>().AsSingle();
            Container.Bind<ComboMeter>()
                .FromMethod(injectContext => new ComboMeter(
                    injectContext.Container.Resolve<CombatConfig>().Meter,
                    injectContext.Container.Resolve<ICombatEvents>()))
                .AsSingle();
            Container.Bind<IntentBuffer>().FromResolveGetter<ICharacterInput>(input => input.Intents).AsSingle();
        }

        private void BindConditions()
        {
            Container.Bind<CombatContextPublisher>().AsSingle();
            Container.Bind<IConditionEvaluator>().To<FunctionConditionEvaluator>().AsSingle();
        }

        private void BindRunner()
        {
            Container.Bind<IPresentationDriver>().To<ProceduralPresentationDriver>().FromComponentInHierarchy().AsSingle();
            Container.Bind(typeof(MotorWarpMover), typeof(IWarpMover)).To<MotorWarpMover>().AsSingle();
            Container.Bind<IHitWindowListener>().To<DemoHitWindowListener>().AsSingle();
            Container.Bind<ActionRunner>().AsSingle();
        }

        private void BindTargeting()
        {
            Container.Bind<ITargetRoster>().To<SceneTargetRoster>().AsSingle();
            Container.Bind<ITargetPicker>().To<StandInTargetPicker>().AsSingle();
        }

        /// <summary>
        /// Validates the stance and its attacks once at scene load, so a reversed window or an edge
        /// to nowhere is an error in the console rather than a hit window that never closes.
        /// </summary>
        private void ReportAuthoringErrors()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] No CombatConfig assigned on '{name}'.", this);
                return;
            }

            Stance stance = config.Stance;
            if (stance == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no stance.", config);
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
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] {error}", stance);
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
