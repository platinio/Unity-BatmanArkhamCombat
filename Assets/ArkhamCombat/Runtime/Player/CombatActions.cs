using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// The runner lives in the character's own context, which the scene cannot see into, so this is
    /// also where a state or the overlay finds the runner and the frame's warp movement. Without
    /// generated Hermes events the actions still play and nothing is announced.
    /// </summary>
    public sealed class CombatActions : CharacterComponent
    {
        private ActionRunner runner;
        private MotorWarpMover warpMover;
        private IActionStartGate startGate;
        private ISceneGameEvents sceneGameEvents;

        public ActionRunner Runner => runner;

        [Inject]
        internal void Construct(
            ActionRunner runner,
            MotorWarpMover warpMover,
            IActionStartGate startGate,
            ISceneGameEvents sceneGameEvents)
        {
            this.runner = runner;
            this.warpMover = warpMover;
            this.startGate = startGate;
            this.sceneGameEvents = sceneGameEvents;
        }

        private void Awake()
        {
            if (runner == null)
            {
                Debug.LogError(
                    $"[{nameof(CombatActions)}] Nothing was injected on '{name}'. The character needs a " +
                    $"GameObjectContext with a {nameof(CombatCharacterInstaller)}, and the scene a SceneContext.", this);
            }
        }

        private void Start() => StartListeningToTheRunner();

        private void OnDestroy()
        {
            StopListeningToTheRunner();
            runner?.Trace.StopListening();
        }

        public override void Tick(float deltaTime) =>
            runner.Tick(deltaTime, transform.position, startGate.CanStartFromIdle);

        public Vector3 TakePendingWarpMovement() => warpMover.TakePendingMovement();

#if HERMES_EVENTS_GENERATED
        private GameEventDispatcher SceneEvents => sceneGameEvents.GameEventDispatcher;

        private bool IsSceneMissingHermes => sceneGameEvents == null || SceneEvents == null;

        internal void StartListeningToTheRunner()
        {
            if (IsSceneMissingHermes)
            {
                Debug.LogError(
                    $"[{nameof(CombatActions)}] '{name}' cannot announce its actions, so nothing will hear them start or end. " +
                    "The scene needs an object with SceneGameEvents and GameEventDispatcher.", this);
                return;
            }

            runner.ActionStarted += AnnounceActionStarted;
            runner.ActionEnded += AnnounceActionEnded;
        }

        internal void StopListeningToTheRunner()
        {
            if (IsSceneMissingHermes)
            {
                return;
            }

            runner.ActionStarted -= AnnounceActionStarted;
            runner.ActionEnded -= AnnounceActionEnded;
        }

        // The runner is only listened to once the scene events were found, so they are known to be there.
        private void AnnounceActionStarted(ActionDefinition action, bool isInterrupt) =>
            SceneEvents.ActionStartedGameEvent.Raise(gameObject, action, isInterrupt);

        private void AnnounceActionEnded(ActionDefinition action, bool wasInterrupted) =>
            SceneEvents.ActionEndedGameEvent.Raise(gameObject, action, wasInterrupted);
#else
        private void StartListeningToTheRunner() { }

        private void StopListeningToTheRunner() { }
#endif
    }
}
