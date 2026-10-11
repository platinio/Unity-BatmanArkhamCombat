using System.Collections.Generic;
using ArcaneOnyx.ScriptableObjectDatabase;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Characters;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Enemies
{
    /// <summary>
    /// An enemy has no stance and no presses: its tree plays its actions. An optional piece is
    /// bound when its component is on the enemy and replaced by one that does nothing when it is not.
    /// </summary>
    public sealed class EnemyInstaller : MonoInstaller
    {
        [Tooltip("Which kind of enemy this is: its health, what it shrugs off, how long it idles.")]
        [SerializeField, ScriptableItemDatabaseSelector(typeof(EnemyArchetypeDatabase))]
        private EnemyArchetype archetype;

        public override void InstallBindings()
        {
            ReportAuthoringErrors();

            BindArchetype();
            BindFacts();
            BindConditions();
            BindPresentation();
            BindWarpAndHits();
            BindTargeting();
            BindStartGate();
            BindRunner();
        }

        // Left unbound when missing: what depends on them takes them as optional and goes without.
        private void BindArchetype()
        {
            bool hasArchetype = archetype != null;
            if (hasArchetype)
            {
                Container.Bind<EnemyArchetype>().FromInstance(archetype);
            }

            bool hasProfile = hasArchetype && archetype.HitReceiverProfile != null;
            if (hasProfile)
            {
                Container.Bind<HitReceiverProfile>().FromInstance(archetype.HitReceiverProfile);
            }
        }

        private void BindFacts() => Container.Bind<CombatFacts>().FromInstance(new CombatFacts());

        private void BindConditions() => Container.Bind<IConditionEvaluator>().To<AlwaysConditionEvaluator>().AsSingle();

        private void BindPresentation()
        {
            IPresentationDriver driverOnTheBody = GetComponentInChildren<IPresentationDriver>();
            bool hasBody = driverOnTheBody != null;

            Container.Bind<IPresentationDriver>().FromInstance(hasBody ? driverOnTheBody : new NullPresentationDriver());
        }

        // CombatActions asks for the warp mover by its own type, so an enemy has one though nothing takes its movement yet.
        private void BindWarpAndHits()
        {
            Container.Bind(typeof(MotorWarpMover), typeof(IWarpMover)).To<MotorWarpMover>().AsSingle();
            Container.Bind<IHitWindowListener>().To<NullHitWindowListener>().AsSingle();
        }

        private void BindTargeting()
        {
            bool hasTargeting = TryGetComponent(out IActionTargetPicker targetPickerOnTheEnemy);

            Container.Bind<IActionTargetPicker>()
                .FromInstance(hasTargeting ? targetPickerOnTheEnemy : new NullActionTargetPicker());
        }

        // Without a gate of its own nothing holds an enemy back: its tree may start an action at any time.
        private void BindStartGate()
        {
            bool hasStartGate = TryGetComponent(out IActionStartGate startGateOnTheEnemy);

            Container.Bind<IActionStartGate>()
                .FromInstance(hasStartGate ? startGateOnTheEnemy : new AlwaysOpenActionStartGate());
        }

        private void BindRunner() =>
            Container.Bind<ActionRunner>().AsSingle().WithArguments<Stance, IntentBuffer>(null, null);

        private void ReportAuthoringErrors()
        {
            if (archetype == null)
            {
                Debug.LogError($"[{nameof(EnemyInstaller)}] No archetype assigned on '{name}', so this enemy has no health and cannot be hit.", this);
                return;
            }

            List<string> errors = new List<string>();
            archetype.Validate(errors);

            foreach (string error in errors)
            {
                Debug.LogError($"[{nameof(EnemyInstaller)}] {error}", archetype);
            }

            ReportACombatRangeInsideTheRing();
        }

        // Inside the ring's reach the two would undo each other: holding its slot walks the enemy out of combat range, and closing in walks it back.
        private void ReportACombatRangeInsideTheRing()
        {
            EncounterDirectorSettings encounter = Container.TryResolve<EncounterDirectorSettings>();
            if (encounter == null)
            {
                return;
            }

            float farthestASlotCanBe = encounter.SlotRadius + encounter.SlotSampleRadius;
            if (archetype.CombatRange <= farthestASlotCanBe)
            {
                Debug.LogError(
                    $"[{nameof(EnemyInstaller)}] Archetype '{archetype.name}': combatRange ({archetype.CombatRange:0.00}) does not reach past the ring " +
                    $"(slot radius plus sample radius, {farthestASlotCanBe:0.00}), so its enemies would walk in and out of range instead of holding a slot.",
                    archetype);
            }
        }
    }
}
