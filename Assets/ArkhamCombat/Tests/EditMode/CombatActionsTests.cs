using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class CombatActionsTests
    {
        private const float TickSeconds = 0.1f;
        private const float PressLifetimeSeconds = 10f;
        private const float NegligibleDistance = 1e-4f;

        private static readonly Vector3 CharacterPosition = new Vector3(10f, 0f, 0f);
        private static readonly Vector3 TargetPosition = new Vector3(10f, 0f, 3f);

        private TestIntentKinds kinds;
        private AttackDefinition jab;
        private Stance stance;
        private IntentBuffer intents;
        private RecordingActionTargetPicker targetPicker;
        private SettableActionStartGate startGate;
        private GameObject character;
        private CombatActions combatActions;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            jab = Attack("Jab");
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
                new NullPresentationDriver(), warpMover, new NullHitWindowListener(), new NullCombatEvents(), targetPicker);

            // Hidden and never saved, so the open scene is left untouched.
            character = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            character.transform.position = CharacterPosition;
            combatActions = character.AddComponent<CombatActions>();
            combatActions.Construct(runner, warpMover, startGate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(stance);
            Object.DestroyImmediate(jab);
            kinds.Destroy();
        }

        private void PressStrike() => intents.Push(kinds.Strike, Vector2.zero);

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
    }
}
