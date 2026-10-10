using ArcaneOnyx.UnityExtensions;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Each character's own combat is bound on the character by its
    /// <see cref="CombatCharacterInstaller"/>.
    /// </summary>
    [AutoAssetGeneration("Installers/Static", "CombatStaticInstaller")]
    [StaticInstaller(StaticInstallerExecutionOrder.Normal)]
    public sealed class CombatStaticInstaller : ScriptableObjectInstaller
    {
        [Tooltip("The interrupt kinds, the combo meter settings and the stand-in tunables.")]
        [SerializeField] private CombatConfig config;

        public override void InstallBindings()
        {
            ReportAuthoringErrors();

            BindSettings();
            BindTargeting();
        }

        private void BindSettings()
        {
            Container.Bind<IComboMeterSettings>().FromInstance(config);
            Container.Bind<ITargetingSettings>().FromInstance(config);
            Container.Bind<IFacingSettings>().FromInstance(config);
            Container.Bind<ICombatFactsSettings>().FromInstance(config);
            Container.Bind<IHitRangeSettings>().FromInstance(config);
            Container.Bind<InterruptKinds>().FromMethod(_ => InterruptKindsOf(config)).AsSingle();
        }

        private void BindTargeting()
        {
            Container.Bind<ITargetRoster>().To<SceneTargetRoster>().AsSingle();
            Container.Bind<ITargetScorer>().To<StandInTargetScorer>().AsSingle();
        }

        private static InterruptKinds InterruptKindsOf(CombatConfig combatConfig) =>
            combatConfig != null
                ? new InterruptKinds(combatConfig.EvadeKind, combatConfig.CounterKind)
                : new InterruptKinds(evade: null, counter: null);

        /// <summary>
        /// Checks the config once at scene load, so a missing evade or counter kind is an error in
        /// the console rather than an interrupt that silently never happens.
        /// </summary>
        private void ReportAuthoringErrors()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] No CombatConfig assigned on '{name}'.", this);
                return;
            }

            if (config.EvadeKind == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no evade kind.", config);
            }

            if (config.CounterKind == null)
            {
                Debug.LogError($"[{nameof(CombatStaticInstaller)}] CombatConfig '{config.name}' has no counter kind.", config);
            }
        }
    }
}
