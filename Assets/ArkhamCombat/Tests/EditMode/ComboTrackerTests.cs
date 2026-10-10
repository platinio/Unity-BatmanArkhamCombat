#if HERMES_EVENTS_GENERATED
using System.Collections.Generic;
using ArcaneOnyx.GameEventGenerator;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode runs no Awake and no Start, so SetUp builds the dispatcher's events and starts the
    // tracker by hand, in the order Unity would.
    public class ComboTrackerTests
    {
        private const float MeterTimeoutSeconds = 2.5f;
        private const float LongerThanTheMeterTimeout = 60f;

        private GameObject hermesObject;
        private GameEventDispatcher dispatcher;
        private GameObject character;
        private GameObject anotherCharacter;
        private GameObject target;
        private AttackDefinition attack;
        private ComboTracker tracker;
        private List<ComboChangedEventArgs> comboChanges;
        private List<ComboResetEventArgs> comboResets;

        [SetUp]
        public void SetUp()
        {
            hermesObject = HiddenObject("Hermes");
            dispatcher = hermesObject.AddComponent<GameEventDispatcher>();
            dispatcher.StrikeLandedGameEvent = new GameEventDispatcher.StrikeLandedEvent(dispatcher);
            dispatcher.StrikeWhiffedGameEvent = new GameEventDispatcher.StrikeWhiffedEvent(dispatcher);
            dispatcher.ComboChangedGameEvent = new GameEventDispatcher.ComboChangedEvent(dispatcher);
            dispatcher.ComboResetGameEvent = new GameEventDispatcher.ComboResetEvent(dispatcher);

            character = HiddenObject("Character");
            anotherCharacter = HiddenObject("Another character");
            target = HiddenObject("Target");
            attack = Attack("Jab");

            tracker = character.AddComponent<ComboTracker>();
            tracker.Construct(new SceneGameEventsWith(dispatcher), new FixedComboMeterSettings(MeterTimeoutSeconds, 3, 5, 8));
            tracker.CreateMeter();
            tracker.StartListeningToStrikes();

            comboChanges = new List<ComboChangedEventArgs>();
            comboResets = new List<ComboResetEventArgs>();
            dispatcher.ComboChangedGameEvent.AddListener(comboChanges.Add);
            dispatcher.ComboResetGameEvent.AddListener(comboResets.Add);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hermesObject);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(anotherCharacter);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(attack);
        }

        private void StrikeLandsFor(GameObject attacker) => dispatcher.StrikeLandedGameEvent.Raise(attacker, attack, target);

        private void StrikeWhiffsFor(GameObject attacker) => dispatcher.StrikeWhiffedGameEvent.Raise(attacker, attack);

        [Test]
        public void CountsItsOwnCharactersLandedStrikes()
        {
            StrikeLandsFor(character);
            StrikeLandsFor(character);

            Assert.AreEqual(2, tracker.Count);
        }

        [Test]
        public void LosesTheComboWhenItsOwnCharacterWhiffs()
        {
            StrikeLandsFor(character);

            StrikeWhiffsFor(character);

            Assert.AreEqual(0, tracker.Count);
        }

        [Test]
        public void IgnoresAnotherCharactersStrikes()
        {
            StrikeLandsFor(character);

            StrikeLandsFor(anotherCharacter);
            StrikeWhiffsFor(anotherCharacter);

            Assert.AreEqual(1, tracker.Count);
        }

        [Test]
        public void LosesTheComboAfterTheMeterTimeout()
        {
            StrikeLandsFor(character);

            tracker.Tick(LongerThanTheMeterTimeout);

            Assert.AreEqual(0, tracker.Count);
            Assert.AreEqual(ComboResetReason.Timeout, comboResets[0].Reason);
        }

        [Test]
        public void AnnouncesAComboChangeWithItsCharacter()
        {
            StrikeLandsFor(character);

            Assert.AreEqual(1, comboChanges.Count);
            Assert.AreSame(character, comboChanges[0].Character);
            Assert.AreEqual(1, comboChanges[0].Count);
            Assert.AreEqual(0, comboChanges[0].Tier);
        }

        [Test]
        public void AnnouncesAComboResetWithItsCharacterAndTheReason()
        {
            StrikeLandsFor(character);

            StrikeWhiffsFor(character);

            Assert.AreEqual(1, comboResets.Count);
            Assert.AreSame(character, comboResets[0].Character);
            Assert.AreEqual(ComboResetReason.Whiff, comboResets[0].Reason);
            Assert.AreEqual(0, comboChanges[comboChanges.Count - 1].Count, "the change to zero follows the reset");
        }

        [Test]
        public void AnnouncesNothingForAnotherCharactersStrikes()
        {
            StrikeLandsFor(anotherCharacter);
            StrikeWhiffsFor(anotherCharacter);

            Assert.IsEmpty(comboChanges);
            Assert.IsEmpty(comboResets);
        }

        [Test]
        public void StopsCountingOnceItStopsListening()
        {
            StrikeLandsFor(character);

            tracker.StopListeningToStrikes();
            StrikeLandsFor(character);

            Assert.AreEqual(1, tracker.Count);
        }
    }
}
#endif
