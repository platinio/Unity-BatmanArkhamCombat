using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class DashTests
    {
        // The dash lasts one second, so seconds played and normalized time are the same number.
        private const float DashSeconds = 1f;
        private const float TravelDistance = 3f;
        private const float Tolerance = 1e-4f;
        private const float PressLifetimeSeconds = 10f;
        private const float ShortTick = 0.1f;
        private const int ManySteps = 10;

        private static readonly Window TravelsInTheMiddle = new Window(0.2f, 0.7f);
        private static readonly Vector3 East = Vector3.right;
        private static readonly Vector2 StickPushedRight = Vector2.right;

        private sealed class RecordingTravelDirectionPicker : ITravelDirectionPicker
        {
            public readonly List<Vector2> AskedDirections = new List<Vector2>();
            public Vector3 DirectionToGive;

            public Vector3 PickTravelDirection(Vector2 direction)
            {
                AskedDirections.Add(direction);
                return DirectionToGive;
            }
        }

        private TestIntentKinds kinds;
        private DashDefinition evade;
        private AttackDefinition jab;
        private ActionDefinition taunt;
        private Stance stance;
        private IntentBuffer intents;
        private RecordingWarpMover warpMover;
        private RecordingTravelDirectionPicker travelDirectionPicker;
        private ActionRunner runner;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            evade = ScriptableObject.CreateInstance<DashDefinition>();
            evade.name = "Evade";
            evade.Configure(DashSeconds);
            evade.ConfigureDash(TravelsInTheMiddle, TravelDistance);
            jab = Attack("Jab");
            taunt = Action("Taunt", DashSeconds);

            stance = Stance("Neutral",
                new[]
                {
                    NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                    new ChainNode("S1", jab),
                    new ChainNode("Evade", evade)
                },
                new[] { new Edge(kinds.Evade, "Evade") });

            intents = new IntentBuffer();
            warpMover = new RecordingWarpMover();
            travelDirectionPicker = new RecordingTravelDirectionPicker { DirectionToGive = East };
            runner = new ActionRunner(
                stance, intents, new CombatFacts(), new AlwaysConditionEvaluator(), kinds.InterruptKinds, new NullPresentationDriver(),
                warpMover, new NullHitWindowListener(), travelDirectionPicker: travelDirectionPicker);
        }

        [TearDown]
        public void TearDown()
        {
            kinds.Destroy();
            Object.DestroyImmediate(evade);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(taunt);
            Object.DestroyImmediate(stance);
        }

        private void Tick(float seconds) => runner.Tick(seconds, Vector3.zero, canStartFromIdle: true);

        private void TickInSteps(float seconds, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Tick(seconds / steps);
            }
        }

        private void AssertHasMovedBy(Vector3 expected, string message = null) =>
            Assert.Less(Vector3.Distance(expected, warpMover.TotalMovement), Tolerance, $"{message} expected {expected}, moved {warpMover.TotalMovement}");

        [Test]
        public void ADashStartedByAPress_AsksTheCharactersPicker_WithTheStickAtThePress()
        {
            intents.Push(kinds.Evade, StickPushedRight);

            Tick(0f);

            Assert.AreSame(evade, runner.CurrentDash);
            CollectionAssert.AreEqual(new[] { StickPushedRight }, travelDirectionPicker.AskedDirections);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(60)]
        public void OverItsTravelWindow_ADashCoversItsWholeDistance_WhateverTheFrameRate(int framesInTheDash)
        {
            runner.PlayAction(evade, Vector3.zero);

            TickInSteps(DashSeconds, framesInTheDash);

            AssertHasMovedBy(East * TravelDistance);
        }

        [Test]
        public void BeforeItsTravelWindowOpens_ADashHasNotMoved()
        {
            float beforeTheWindow = TravelsInTheMiddle.Start / 2f;
            runner.PlayAction(evade, Vector3.zero);

            Tick(beforeTheWindow);

            AssertHasMovedBy(Vector3.zero);
            Assert.IsFalse(runner.IsTravelling);
        }

        [Test]
        public void HalfwayThroughItsTravelWindow_ADashHasCoveredHalfItsDistance()
        {
            float middleOfTheWindow = (TravelsInTheMiddle.Start + TravelsInTheMiddle.End) / 2f;
            runner.PlayAction(evade, Vector3.zero);

            Tick(middleOfTheWindow);

            AssertHasMovedBy(East * (TravelDistance / 2f));
            Assert.IsTrue(runner.IsTravelling);
        }

        [Test]
        public void ADirectionGivenWithTheAction_WinsOverThePickers()
        {
            runner.PlayAction(evade, Vector3.zero, Vector3.back);

            TickInSteps(DashSeconds, ManySteps);

            AssertHasMovedBy(Vector3.back * TravelDistance);
        }

        [Test]
        public void WithADirectionGiven_ThePickerIsNotAsked()
        {
            runner.PlayAction(evade, Vector3.zero, Vector3.back);

            Assert.IsEmpty(travelDirectionPicker.AskedDirections);
        }

        [Test]
        public void PlayingNothing_LeavesADashThatTravels_OnItsWay()
        {
            runner.PlayAction(evade, Vector3.zero);

            runner.PlayAction(null, Vector3.zero, Vector3.back);
            TickInSteps(DashSeconds, ManySteps);

            AssertHasMovedBy(East * TravelDistance);
        }

        [Test]
        public void ADashWithDistanceToCoverAndNoTimeToCoverItIn_IsReported()
        {
            evade.ConfigureDash(new Window(0.3f, 0.3f), TravelDistance);
            List<string> errors = new List<string>();

            Assert.IsFalse(evade.Validate(errors));
            StringAssert.Contains("travelWindow has no length", errors.Single());
        }

        [Test]
        public void TheDirection_IsFlattenedAndBroughtToLengthOne_SoTheDistanceIsTheDashes()
        {
            Vector3 upAndFarToTheEast = new Vector3(40f, 9f, 0f);
            runner.PlayAction(evade, Vector3.zero, upAndFarToTheEast);

            TickInSteps(DashSeconds, ManySteps);

            AssertHasMovedBy(East * TravelDistance);
        }

        [Test]
        public void WithNoDirection_ADashPlaysInPlace()
        {
            travelDirectionPicker.DirectionToGive = Vector3.zero;
            runner.PlayAction(evade, Vector3.zero);

            TickInSteps(DashSeconds, ManySteps);

            AssertHasMovedBy(Vector3.zero);
        }

        [Test]
        public void APlainAction_DoesNotTravel_AndItsDirectionIsNeverAskedFor()
        {
            runner.PlayAction(taunt, Vector3.zero);

            TickInSteps(DashSeconds, ManySteps);

            Assert.IsNull(runner.CurrentDash);
            Assert.AreEqual(0, warpMover.MoveCount);
            Assert.IsEmpty(travelDirectionPicker.AskedDirections);
        }

        [Test]
        public void AnAttack_IsNotADash()
        {
            runner.PlayAction(jab, Vector3.zero);

            Assert.IsNull(runner.CurrentDash);
            Assert.AreSame(jab, runner.CurrentAttack);
        }

        [Test]
        public void APressDuringADash_Waits_EvenForAnInterruptThatIsAllowed()
        {
            Stance stanceWithACounter = Stance("Neutral",
                new[]
                {
                    NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                    new ChainNode("S1", jab),
                    new ChainNode("Evade", evade),
                    new ChainNode("Counter", jab)
                },
                new[] { new Edge(kinds.Evade, "Evade"), new Edge(kinds.Counter, "Counter") });
            CombatFacts factsThatAllowACounter = new CombatFacts { IsIncomingAttackCounterable = true };
            ActionRunner runnerThatCouldCounter = new ActionRunner(
                stanceWithACounter, intents, factsThatAllowACounter, new AlwaysConditionEvaluator(), kinds.InterruptKinds,
                new NullPresentationDriver(), warpMover, new NullHitWindowListener(), travelDirectionPicker: travelDirectionPicker);

            try
            {
                runnerThatCouldCounter.PlayAction(evade, Vector3.zero);
                intents.Push(kinds.Counter, Vector2.zero);

                runnerThatCouldCounter.Tick(ShortTick, Vector3.zero, canStartFromIdle: true);

                Assert.AreSame(evade, runnerThatCouldCounter.CurrentAction, "a dash is not cut short by a press");
                Assert.AreEqual(1, intents.Queued.Count, "the press waits");
            }
            finally
            {
                Object.DestroyImmediate(stanceWithACounter);
            }
        }

        [Test]
        public void OnceADashEnds_TheRunnerPlaysNoDash()
        {
            runner.PlayAction(evade, Vector3.zero);

            Tick(DashSeconds);

            Assert.IsFalse(runner.IsPlaying);
            Assert.IsNull(runner.CurrentDash);
            Assert.IsFalse(runner.IsTravelling);
        }

        [Test]
        public void ANodeThatPlaysAnActionThatIsNotAnAttack_HasSomethingToPlay()
        {
            List<string> errors = new List<string>();

            Assert.IsTrue(stance.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void ATravelWindowThatEndsBeforeItStarts_IsReported()
        {
            evade.ConfigureDash(new Window(0.7f, 0.2f), TravelDistance);
            List<string> errors = new List<string>();

            Assert.IsFalse(evade.Validate(errors));
            StringAssert.Contains("travelWindow", errors[0]);
        }
    }
}
