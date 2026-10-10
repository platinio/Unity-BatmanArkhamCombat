using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// It hears every strike in the scene and counts only its own character's. Without generated
    /// Hermes events the combo stays at zero.
    /// </summary>
    public sealed class ComboTracker : CharacterComponent
    {
        private ISceneGameEvents sceneGameEvents;
        private IComboMeterSettings meterSettings;
        private ComboMeter meter;

        public int Count => meter.Count;

        public int Tier => meter.Tier;

        [Inject]
        internal void Construct(ISceneGameEvents sceneGameEvents, IComboMeterSettings meterSettings)
        {
            this.sceneGameEvents = sceneGameEvents;
            this.meterSettings = meterSettings;
        }

        private void Awake() => CreateMeter();

        // Not OnEnable: the dispatcher builds its events in its own Awake, which may run after this one.
        private void Start() => StartListeningToStrikes();

        private void OnDestroy() => StopListeningToStrikes();

        public override void Tick(float deltaTime) => meter.Tick(deltaTime);

        internal void CreateMeter()
        {
            meter = new ComboMeter(meterSettings);
            meter.ComboChanged += AnnounceComboChanged;
            meter.ComboReset += AnnounceComboReset;
        }

#if HERMES_EVENTS_GENERATED
        private GameEventDispatcher SceneEvents => sceneGameEvents.GameEventDispatcher;

        private bool IsSceneMissingHermes => sceneGameEvents == null || SceneEvents == null;

        internal void StartListeningToStrikes()
        {
            if (IsSceneMissingHermes)
            {
                Debug.LogError(
                    $"[{nameof(ComboTracker)}] '{name}' cannot hear strikes, so its combo will not count. " +
                    "The scene needs an object with SceneGameEvents and GameEventDispatcher.", this);
                return;
            }

            SceneEvents.StrikeLandedGameEvent.AddListener(OnStrikeLanded);
            SceneEvents.StrikeWhiffedGameEvent.AddListener(OnStrikeWhiffed);
        }

        internal void StopListeningToStrikes()
        {
            if (IsSceneMissingHermes)
            {
                return;
            }

            SceneEvents.StrikeLandedGameEvent.RemoveListener(OnStrikeLanded);
            SceneEvents.StrikeWhiffedGameEvent.RemoveListener(OnStrikeWhiffed);
        }

        private void OnStrikeLanded(StrikeLandedEventArgs strike)
        {
            if (IsThisCharacter(strike.Attacker))
            {
                meter.Increment();
            }
        }

        private void OnStrikeWhiffed(StrikeWhiffedEventArgs strike)
        {
            if (IsThisCharacter(strike.Attacker))
            {
                meter.Reset(ComboResetReason.Whiff);
            }
        }

        private bool IsThisCharacter(GameObject attacker) => attacker == gameObject;

        // The meter only changes after a strike was heard, so the scene events are known to be there.
        private void AnnounceComboChanged(int count, int tier) => SceneEvents.ComboChangedGameEvent.Raise(gameObject, count, tier);

        private void AnnounceComboReset(ComboResetReason reason) => SceneEvents.ComboResetGameEvent.Raise(gameObject, reason);
#else
        private void StartListeningToStrikes()
        {
            Debug.LogWarning(
                $"[{nameof(ComboTracker)}] The Hermes events are not generated, so the combo on '{name}' will not count. " +
                "Run Regenerate Events in Window > Arcane Onyx > Hermes.", this);
        }

        private void StopListeningToStrikes() { }

        private void AnnounceComboChanged(int count, int tier) { }

        private void AnnounceComboReset(ComboResetReason reason) { }
#endif
    }
}
