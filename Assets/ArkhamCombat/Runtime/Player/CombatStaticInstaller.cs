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
            Container.Bind<InterruptKinds>().FromResolveGetter<CombatConfig>(InterruptKindsOf).AsSingle();
            Container.Bind<ICombatEvents>().FromInstance(events ?? new NullCombatEvents());
        }

        private void BindComboState()
        {
            Container.Bind<CombatFactsUpdater>().FromComponentInHierarchy().AsSingle();
            Container.Bind<CombatFacts>().FromMethod(FactsFromTheSceneUpdater).AsSingle();
            Container.Bind<ComboMeter>()
                .FromMethod(injectContext => new ComboMeter(
                    injectContext.Container.Resolve<CombatConfig>().Meter,
                    injectContext.Container.Resolve<ICombatEvents>()))
                .AsSingle();
            Container.Bind<IntentBuffer>().FromResolveGetter<ICharacterInput>(input => input.Intents).AsSingle();
        }

        private void BindConditions()
        {
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
            Container.Bind<ITargetScorer>().To<StandInTargetScorer>().AsSingle();

            // Asked for optionally: a scene without CombatTargeting gives the runner and the facts no target.
            Container.Bind<IActionTargetPicker>().To<CombatTargeting>().FromComponentInHierarchy().AsSingle();
        }

        // A scene without a facts updater still gets facts, left empty: the counter rule then never
        // allows a counter and the target-side pool falls back to its no-side pick.
        private static CombatFacts FactsFromTheSceneUpdater(InjectContext injectContext)
        {
            CombatFactsUpdater factsUpdater = injectContext.Container.TryResolve<CombatFactsUpdater>();
            return factsUpdater != null ? factsUpdater.Facts : new CombatFacts();
        }

        private static InterruptKinds InterruptKindsOf(CombatConfig combatConfig) =>
            combatConfig != null
                ? new InterruptKinds(combatConfig.EvadeKind, combatConfig.CounterKind)
                : new InterruptKinds(evade: null, counter: null);

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

            ReportMissingInterruptKinds();

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

        private void ReportMissingInterruptKinds()
        {
            if (config.EvadeKind == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no evade kind.", config);
            }

            if (config.CounterKind == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no counter kind.", config);
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
