using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// An enemy has no stance and no presses: its tree plays its actions.
    /// </summary>
    public sealed class EnemyInstaller : MonoInstaller
    {
        [Tooltip("Which kind of enemy this is: its health, what it shrugs off, how long it idles.")]
        [SerializeField, ScriptableItemDatabaseSelector(typeof(EnemyArchetypeDatabase))]
        private EnemyArchetype archetype;

        public override void InstallBindings()
        {
            if (!ReportAuthoringErrors())
            {
                return;
            }

            BindArchetype();
            BindPresentation();
            BindWarpMover();
            BindStartGate();
            BindRunner();
        }

        private void BindArchetype()
        {
            Container.Bind<EnemyArchetype>().FromInstance(archetype);
            Container.Bind<HitReceiverProfile>().FromInstance(archetype.HitReceiverProfile);
        }

        private void BindPresentation()
        {
            IPresentationDriver driverOnTheBody = GetComponentInChildren<IPresentationDriver>();
            bool hasBody = driverOnTheBody != null;

            Container.Bind<IPresentationDriver>().FromInstance(hasBody ? driverOnTheBody : new NullPresentationDriver());
        }

        // Reactions do not warp, so the pending movement is never taken.
        private void BindWarpMover() =>
            Container.Bind(typeof(MotorWarpMover), typeof(IWarpMover)).To<MotorWarpMover>().AsSingle();

        // No state machine to wait on: an enemy's tree may start an action at any time.
        private void BindStartGate() => Container.Bind<IActionStartGate>().FromInstance(new AlwaysOpenActionStartGate());

        private void BindRunner() => Container.Bind<ActionRunner>().FromMethod(RunnerWithoutAStance).AsSingle();

        private static ActionRunner RunnerWithoutAStance(InjectContext context)
        {
            DiContainer container = context.Container;
            return new ActionRunner(
                stance: null,
                intents: null,
                facts: new CombatFacts(),
                conditions: new AlwaysConditionEvaluator(),
                container.Resolve<InterruptKinds>(),
                container.Resolve<IPresentationDriver>(),
                container.Resolve<IWarpMover>(),
                new NullHitWindowListener());
        }

        private bool ReportAuthoringErrors()
        {
            if (archetype == null)
            {
                Debug.LogError($"[{nameof(EnemyInstaller)}] No archetype assigned on '{name}', so this enemy cannot be built.", this);
                return false;
            }

            if (archetype.HitReceiverProfile == null)
            {
                Debug.LogError($"[{nameof(EnemyInstaller)}] Archetype '{archetype.name}' names no hit receiver profile, so '{name}' cannot be hit.", archetype);
                return false;
            }

            return true;
        }
    }
}
