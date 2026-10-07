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
    /// The player's combat graph, bound scene-wide beside the character installer: the config, the
    /// meter, the context and its publisher, the runner with its driver and sinks, and the stand-in
    /// target picker. The events sink is picked on this asset, so swapping the logging stub for the
    /// Hermes adapter is an inspector change.
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
            if (config == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] No CombatConfig assigned on '{name}'.", this);
            }
            else
            {
                ReportAuthoringErrors(config);
            }

            Container.Bind<CombatConfig>().FromInstance(config);
            Container.Bind<Stance>().FromResolveGetter<CombatConfig>(c => c != null ? c.Stance : null);
            Container.Bind<ICombatEvents>().FromInstance(events ?? new NullCombatEvents());

            Container.Bind<CombatContext>().AsSingle();
            Container.Bind<ComboMeter>()
                .FromMethod(c => new ComboMeter(
                    c.Container.Resolve<CombatConfig>().Meter,
                    c.Container.Resolve<ICombatEvents>()))
                .AsSingle();

            Container.Bind<IntentBuffer>().FromResolveGetter<ICharacterInput>(input => input.Intents).AsSingle();

            Container.Bind<CombatContextPublisher>().AsSingle();
            Container.Bind<IConditionEvaluator>().To<FunctionConditionEvaluator>().AsSingle();

            Container.Bind<IPresentationDriver>().To<ProceduralPresentationDriver>().FromComponentInHierarchy().AsSingle();
            Container.Bind(typeof(MotorDisplacementSink), typeof(IDisplacementSink)).To<MotorDisplacementSink>().AsSingle();
            Container.Bind<IHitWindowSink>().To<DemoHitWindowSink>().AsSingle();

            Container.Bind<ITargetPicker>().To<StandInTargetPicker>().AsSingle();
            Container.Bind<ActionRunner>().AsSingle();
        }

        /// <summary>
        /// Runs the stance and attack validation once at scene load, so a reversed window or an edge
        /// to nowhere is an error in the console rather than a hitbox that never disarms.
        /// </summary>
        private static void ReportAuthoringErrors(CombatConfig config)
        {
            if (config.Stance == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no stance.", config);
                return;
            }

            List<string> errors = new List<string>();
            config.Stance.Validate(errors);

            HashSet<AttackDefinition> attacks = new HashSet<AttackDefinition>();
            foreach (ChainNode node in config.Stance.Nodes)
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
                    foreach (AttackDefinition attack in node.Pool.Attacks)
                    {
                        if (attack != null)
                        {
                            attacks.Add(attack);
                        }
                    }
                }
            }

            foreach (AttackDefinition attack in attacks)
            {
                attack.Validate(errors);
            }

            foreach (string error in errors)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] {error}", config.Stance);
            }
        }
    }
}
