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

            Container.Bind<StandInTargetPicker>().AsSingle();
            Container.Bind<ActionRunner>().AsSingle();
        }
    }
}
