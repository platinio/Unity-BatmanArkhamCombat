#if HERMES_EVENTS_GENERATED
using System.Collections.Generic;
using ArcaneOnyx.GameEventGenerator;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // Edit mode runs no Awake and no Start, so SetUp builds the dispatcher's events and starts the
    // combat actions listening to their runner by hand, in the order Unity would.
    public class CombatActionsTests
    {
        private const float JabSeconds = 1f;
        private const float TickSeconds = 0.1f;
        private const float PressLifetimeSeconds = 10f;
        private const float NegligibleDistance = 1e-4f;

        private static readonly Vector3 CharacterPosition = new Vector3(10f, 0f, 0f);
        private static readonly Vector3 TargetPosition = new Vector3(10f, 0f, 3f);

        private TestIntentKinds kinds;
        private AttackDefinition jab;
        private ActionDefinition flinch;
        private Stance stance;
        private IntentBuffer intents;
        private RecordingActionTargetPicker targetPicker;
        private SettableActionStartGate startGate;
        private GameObject hermesObject;
        private GameEventDispatcher dispatcher;
        private GameObject character;
        private CombatActions combatActions;
        private List<ActionStartedEventArgs> startedActions;
        private List<ActionEndedEventArgs> endedActions;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            jab = Attack("Jab", JabSeconds);
            flinch = Action("Flinch");
            stance = Stance("Neutral",
                new[]
                {
                    NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                    new ChainNode("S1", jab, new Edge(kinds.Strike, "S1"))
                });

            intents = new IntentBuffer();
            targetPicker = new RecordingActionTargetPicker();
            startGate = new SettableActionStartGate();
            MotorWarpMover warpMover = new MotorWarpMover();

            ActionRunner runner = new ActionRunner(
                stance, intents, new CombatFacts(), new AlwaysConditionEvaluator(), kinds.InterruptKinds,
                new NullPresentationDriver(), warpMover, new NullHitWindowListener(), targetPicker);

            hermesObject = HiddenObject("Hermes");
            dispatcher = hermesObject.AddComponent<GameEventDispatcher>();
            dispatcher.ActionStartedGameEvent = new GameEventDispatcher.ActionStartedEvent(dispatcher);
            dispatcher.ActionEndedGameEvent = new GameEventDispatcher.ActionEndedEvent(dispatcher);

            character = HiddenObject("Character");
            character.transform.position = CharacterPosition;
            combatActions = character.AddComponent<CombatActions>();
            combatActions.Construct(runner, warpMover, startGate, new SceneGameEventsWith(dispatcher));
            combatActions.StartListeningToTheRunner();

            startedActions = new List<ActionStartedEventArgs>();
            endedActions = new List<ActionEndedEventArgs>();
            dispatcher.ActionStartedGameEvent.AddListener(startedActions.Add);
            dispatcher.ActionEndedGameEvent.AddListener(endedActions.Add);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hermesObject);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(stance);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(flinch);
            kinds.Destroy();
        }

        private void PressStrike() => intents.Push(kinds.Strike, Vector2.zero);

        private void StartTheJab()
        {
            startGate.CanStartFromIdle = true;
            PressStrike();
            combatActions.Tick(TickSeconds);
        }

        private void TickFor(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / TickSeconds);
            for (int i = 0; i < ticks; i++)
            {
                combatActions.Tick(TickSeconds);
            }
        }

        [Test]
        public void APressStartsAnAction_WhileTheStartGateAllowsIt()
        {
            startGate.CanStartFromIdle = true;
            PressStrike();

            combatActions.Tick(TickSeconds);

            Assert.IsTrue(combatActions.Runner.IsPlaying);
            Assert.AreSame(jab, combatActions.Runner.CurrentAction);
        }

        [Test]
        public void APressWaits_WhileTheStartGateRefuses()
        {
            startGate.CanStartFromIdle = false;
            PressStrike();

            combatActions.Tick(TickSeconds);

            Assert.IsFalse(combatActions.Runner.IsPlaying);
            Assert.AreEqual(1, intents.Queued.Count, "the press stays queued until the gate opens");
        }

        [Test]
        public void TheWarpStartsFromWhereTheCharacterStands()
        {
            targetPicker.TargetToGive = new PointTarget(TargetPosition);
            startGate.CanStartFromIdle = true;
            PressStrike();

            combatActions.Tick(TickSeconds);
            combatActions.Tick(TickSeconds);
            Vector3 warpMovement = combatActions.TakePendingWarpMovement();

            Assert.Greater(warpMovement.z, 0f, "the target is straight ahead of the character");
            Assert.AreEqual(0f, warpMovement.x, NegligibleDistance, "a warp measured from anywhere else would move sideways");
        }

        [Test]
        public void TheWarpMovementIsHandedOverOnce()
        {
            targetPicker.TargetToGive = new PointTarget(TargetPosition);
            startGate.CanStartFromIdle = true;
            PressStrike();
            combatActions.Tick(TickSeconds);
            combatActions.Tick(TickSeconds);

            combatActions.TakePendingWarpMovement();

            Assert.AreEqual(Vector3.zero, combatActions.TakePendingWarpMovement());
        }

        [Test]
        public void AnnouncesAnActionStartingWithItsCharacter()
        {
            StartTheJab();

            Assert.AreEqual(1, startedActions.Count);
            Assert.AreSame(character, startedActions[0].Character);
            Assert.AreSame(jab, startedActions[0].Action);
            Assert.IsFalse(startedActions[0].IsInterrupt, "a press from idle starts the combo and interrupts nothing");
            Assert.IsEmpty(endedActions, "the jab is still playing");
        }

        [Test]
        public void AnnouncesAnActionEndingWithItsCharacter()
        {
            StartTheJab();

            TickFor(JabSeconds);

            Assert.AreEqual(1, endedActions.Count);
            Assert.AreSame(character, endedActions[0].Character);
            Assert.AreSame(jab, endedActions[0].Action);
            Assert.IsFalse(endedActions[0].WasInterrupted, "the jab played to its end");
        }

        [Test]
        public void AnnouncesAnInterruptAndTheActionItCutShort()
        {
            StartTheJab();

            combatActions.Runner.PlayAction(flinch, CharacterPosition);

            Assert.AreEqual(1, endedActions.Count);
            Assert.AreSame(jab, endedActions[0].Action);
            Assert.IsTrue(endedActions[0].WasInterrupted, "the flinch cut the jab short");
            Assert.AreEqual(2, startedActions.Count);
            Assert.AreSame(flinch, startedActions[1].Action);
            Assert.IsTrue(startedActions[1].IsInterrupt);
        }

        [Test]
        public void AnnouncesNothingOnceItStopsListeningToTheRunner()
        {
            StartTheJab();

            combatActions.StopListeningToTheRunner();
            TickFor(JabSeconds);
            StartTheJab();

            Assert.IsTrue(combatActions.Runner.IsPlaying, "the runner plays on, whoever listens");
            Assert.AreEqual(1, startedActions.Count, "only the jab from before it stopped listening");
            Assert.IsEmpty(endedActions);
        }
    }
}
#endif
