using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ActionRunnerTests
    {
        // Both attacks last one second, so seconds played and normalized time are the same number.
        private const float AttackSeconds = 1f;
        private const float TickSeconds = 0.1f;
        private const float ChainResetSeconds = 0.5f;
        private const float PressLifetimeSeconds = 10f;
        private const float StrikeDistance = 1f;
        private const float MaxLunge = 4f;

        private static readonly Window HitWindow = new Window(0.3f, 0.5f);
        private static readonly Window ComboWindow = new Window(0.5f, 0.9f);
        private static readonly Window WarpWindow = new Window(0f, 0.3f);
        private static readonly Window JabEvadeWindow = new Window(0f, 0.6f);
        private static readonly Window CrossEvadeWindow = new Window(0f, 0.3f);

        private TestIntentKinds kinds;
        private AttackDefinition jab;
        private AttackDefinition cross;
        private Stance stance;
        private IntentBuffer intents;
        private CombatFacts facts;
        private NullPresentationDriver driver;
        private RecordingWarpMover warpMover;
        private RecordingHitWindowListener hitWindowListener;
        private RecordingEvents events;
        private RecordingActionTargetPicker targetPicker;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            jab = Attack("Jab", AttackSeconds, HitWindow, ComboWindow, JabEvadeWindow, WarpWindow, StrikeDistance, MaxLunge);
            cross = Attack("Cross", AttackSeconds, HitWindow, ComboWindow, CrossEvadeWindow, WarpWindow, StrikeDistance, MaxLunge);

            // The evade node reuses the cross: any action shows that the evade started.
            stance = Stance("Neutral",
                new[]
                {
                    NodeWithNothingToPlay("Neutral", new Edge(kinds.Strike, "S1")),
                    new ChainNode("S1", jab, new Edge(kinds.Strike, "S2")),
                    new ChainNode("S2", cross, new Edge(kinds.Strike, "S1")),
                    new ChainNode("Evade", cross)
                },
                new[] { new Edge(kinds.Evade, "Evade") },
                ChainResetSeconds);

            intents = new IntentBuffer();
            facts = new CombatFacts();
            driver = new NullPresentationDriver();
            warpMover = new RecordingWarpMover();
            hitWindowListener = new RecordingHitWindowListener();
            events = new RecordingEvents();
            targetPicker = new RecordingActionTargetPicker();
        }

        [TearDown]
        public void TearDown() => kinds.Destroy();

        [Test]
        public void AStrikeWhileIdle_StartsTheComboFromTheRoot()
        {
            ActionRunner runner = CreateRunner();
            Press(kinds.Strike);

            Tick(runner);

            Assert.IsTrue(runner.IsPlaying);
            Assert.AreSame(jab, runner.CurrentAction);
            Assert.AreEqual("S1", runner.CurrentNode.Id);
            Assert.AreEqual("start Jab", events.Log[0]);
        }

        [Test]
        public void NoActionStarts_WhileTheCharacterCannotStartFromIdle()
        {
            ActionRunner runner = CreateRunner();
            Press(kinds.Strike);

            Tick(runner, 3, canStartFromIdle: false);

            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual(1, intents.Queued.Count, "the press waits until the character can act again");
        }

        [Test]
        public void TheHitWindow_OpensOnceAndClosesOnce()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);

            TickFor(runner, 0.2f);
            Assert.AreEqual(0, hitWindowListener.OpenedCount, "at 0.2 the hit window has not opened yet");

            TickFor(runner, 0.1f);
            Assert.AreEqual(1, hitWindowListener.OpenedCount, "at 0.3 the hit window opens");
            Assert.IsTrue(runner.IsHitWindowOpen);
            Assert.AreSame(jab, hitWindowListener.LastOpenedFor);

            TickFor(runner, 0.1f);
            Assert.AreEqual(0, hitWindowListener.ClosedCount, "at 0.4 the hit window is still open");

            TickFor(runner, 0.1f);
            Assert.AreEqual(1, hitWindowListener.ClosedCount, "at 0.5 the hit window closes");

            TickFor(runner, AttackSeconds);
            Assert.AreEqual(1, hitWindowListener.OpenedCount);
            Assert.AreEqual(1, hitWindowListener.ClosedCount);
        }

        [Test]
        public void AnEvade_InterruptsTheAttack_ClosesItsHitWindow_AndStartsAtOnce()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, HitWindow.Start);
            Assert.IsTrue(hitWindowListener.IsOpen, "at 0.3 the hit window is open before the evade");

            Press(kinds.Evade);
            Tick(runner);

            Assert.IsFalse(hitWindowListener.IsOpen, "the hit window never stays open behind the interrupted attack");
            Assert.IsFalse(runner.IsHitWindowOpen);
            Assert.AreSame(cross, runner.CurrentAction);
            Assert.AreEqual("Evade", runner.CurrentNode.Id);
            Assert.AreEqual(0f, driver.NormalizedTime, 1e-5f, "the evade starts from its beginning");
            CollectionAssert.Contains(events.Log, "interrupted Jab");
            CollectionAssert.Contains(events.Log, "interrupt with Cross");
        }

        [Test]
        public void AnEvadeAfterTheEvadeWindow_DoesNotInterrupt()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            const float afterTheJabsEvadeWindow = 0.7f;
            TickFor(runner, afterTheJabsEvadeWindow);
            Press(kinds.Evade);

            Tick(runner);

            Assert.AreSame(jab, runner.CurrentAction);
        }

        [Test]
        public void AFollowUp_IsTakenOnlyWhileTheComboWindowIsOpen()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            Press(kinds.Strike);

            TickFor(runner, 0.4f);
            Assert.AreSame(jab, runner.CurrentAction, "at 0.4 the combo window is not open yet, so the press waits");

            TickFor(runner, 0.1f);
            Assert.AreSame(cross, runner.CurrentAction, "at 0.5 the combo window opens and the press is spent");
            Assert.AreEqual("S2", runner.CurrentNode.Id);
            Assert.AreEqual(0, intents.Queued.Count);

            CollectionAssert.Contains(events.Log, "end Jab");
            CollectionAssert.DoesNotContain(events.Log, "interrupted Jab");
            ActionTrace.Record jabRecord = RecordBeforeTheLatest(runner);
            Assert.IsFalse(jabRecord.WasInterrupted, "the jab allowed the follow-up, so it was not interrupted");
            Assert.AreEqual(0.5f, jabRecord.EndedAt, 1e-5f);
        }

        [Test]
        public void APressWhileTheComboWindowIsAlreadyOpen_IsTakenOnTheNextTick()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            const float insideTheComboWindow = 0.7f;
            TickFor(runner, insideTheComboWindow);
            Assert.IsTrue(runner.IsComboWindowOpen, "at 0.7 the combo window is open");

            Press(kinds.Strike);
            Tick(runner);

            Assert.AreSame(cross, runner.CurrentAction, "the resolver is asked on every tick while the window is open");
        }

        [Test]
        public void Interrupt_FromCode_ReplacesTheRunningAction()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, HitWindow.Start);
            Assert.IsTrue(hitWindowListener.IsOpen);
            stance.TryGetNode("Evade", out ChainNode evade);

            runner.Interrupt(evade, Vector3.zero);

            Assert.IsFalse(hitWindowListener.IsOpen);
            Assert.AreSame(cross, runner.CurrentAction);
            Assert.AreEqual("Evade", runner.CurrentNode.Id);
            CollectionAssert.Contains(events.Log, "interrupted Jab");
            CollectionAssert.Contains(events.Log, "interrupt with Cross");
        }

        [Test]
        public void AnAttackEndingWithoutAFollowUp_GoesIdle_AndKeepsItsPlaceInTheComboUntilTheResetTime()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, AttackSeconds);

            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual("end Jab", events.Log[events.Log.Count - 1]);
            Assert.AreEqual("S1", runner.CurrentNode.Id, "until the reset time passes, the combo keeps its place");

            TickFor(runner, ChainResetSeconds, canStartFromIdle: false);
            Assert.AreEqual("Neutral", runner.CurrentNode.Id, "after the reset time the combo starts over at the root");
        }

        [Test]
        public void AStrikeAfterTheComboWindow_ContinuesTheComboOnceTheAttackEnds()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, ComboWindow.End);
            Press(kinds.Strike);

            Tick(runner);
            Assert.IsFalse(runner.IsPlaying, "at 1.0 the jab has ended; the press came after the combo window closed");

            Tick(runner);
            Assert.AreSame(cross, runner.CurrentAction, "idle at S1, the press continues the combo");
        }

        [Test]
        public void WithAZeroResetTime_TheComboStartsOverAsSoonAsTheAttackEnds()
        {
            stance.Configure("Neutral", stance.Nodes, stance.GlobalEdges, chainResetSeconds: 0f);
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, AttackSeconds);

            Tick(runner, canStartFromIdle: false);

            Assert.AreEqual("Neutral", runner.CurrentNode.Id);
        }

        [Test]
        public void TheWarp_ArrivesAtStrikeDistance_WhenTheWarpWindowCloses()
        {
            ActionRunner runner = CreateRunner();
            targetPicker.TargetToGive = new PointTarget(new Vector3(0f, 0f, 3f));
            StartJab(runner);

            Vector3 position = Vector3.zero;
            for (int i = 0; i < TicksIn(WarpWindow.End); i++)
            {
                position = TickAndFollowTheWarp(runner, position);
            }

            Assert.AreEqual(2f, position.z, 1e-3f, "stopped one strike distance short of the target");
            Assert.IsFalse(runner.IsWarpWindowOpen);
            Assert.IsFalse(runner.WasWarpRefused);
        }

        [Test]
        public void TheWarp_ReAimsEveryTick_WhenTheTargetMoves()
        {
            ActionRunner runner = CreateRunner();
            PointTarget target = new PointTarget(new Vector3(0f, 0f, 3f));
            targetPicker.TargetToGive = target;
            StartJab(runner);

            Vector3 position = Vector3.zero;
            for (int i = 0; i < TicksIn(WarpWindow.End); i++)
            {
                target.Position = new Vector3(i * 0.5f, 0f, 3f);
                position = TickAndFollowTheWarp(runner, position);
            }

            Vector3 expected = jab.WarpDestination(position, target.Position);
            Assert.AreEqual(expected.x, position.x, 1e-3f, "landed relative to where the target ended up");
            Assert.AreEqual(expected.z, position.z, 1e-3f);
        }

        [Test]
        public void TheWarpIsRefused_ExactlyWhenTheTargetIsBeyondTheLungeLimit()
        {
            float lungeLimit = jab.MaxLunge + jab.StrikeDistance;
            Vector3 justInside = new Vector3(0f, 0f, lungeLimit - 0.01f);
            Vector3 justBeyond = new Vector3(0f, 0f, lungeLimit + 0.01f);
            Assert.IsFalse(jab.IsBeyondLunge(Vector3.zero, justInside));
            Assert.IsTrue(jab.IsBeyondLunge(Vector3.zero, justBeyond));

            ActionRunner runnerWithTargetInside = CreateRunner();
            targetPicker.TargetToGive = new PointTarget(justInside);
            StartJab(runnerWithTargetInside);
            Tick(runnerWithTargetInside);
            Assert.IsFalse(runnerWithTargetInside.WasWarpRefused);

            ActionRunner runnerWithTargetBeyond = CreateRunner();
            targetPicker.TargetToGive = new PointTarget(justBeyond);
            StartJab(runnerWithTargetBeyond);
            Tick(runnerWithTargetBeyond);
            Assert.IsTrue(runnerWithTargetBeyond.WasWarpRefused);
        }

        [Test]
        public void TheWarp_IsRefused_WhenTheTargetIsFarBeyondTheLungeLimit()
        {
            ActionRunner runner = CreateRunner();
            targetPicker.TargetToGive = new PointTarget(new Vector3(0f, 0f, 10f));
            StartJab(runner);

            TickFor(runner, WarpWindow.End);

            Assert.IsTrue(runner.WasWarpRefused);
            Assert.AreEqual(0, warpMover.MoveCount, "the character does not move at all");
            Assert.IsTrue(LatestRecord(runner).WasWarpRefused);
        }

        [Test]
        public void TheWarp_DoesNothingWithoutATarget()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);

            TickFor(runner, WarpWindow.End);

            Assert.AreEqual(0, warpMover.MoveCount);
            Assert.IsFalse(runner.WasWarpRefused);
        }

        [Test]
        public void AnActionStartedByAPress_PicksItsTargetOnce_WithTheStickAtThePress()
        {
            ActionRunner runner = CreateRunner();
            Vector2 stickRight = new Vector2(1f, 0f);
            PointTarget pickedAtThePress = new PointTarget(new Vector3(0f, 0f, 2f));
            targetPicker.TargetToGive = pickedAtThePress;
            Press(kinds.Strike, stickRight);

            Tick(runner);
            targetPicker.TargetToGive = new PointTarget(new Vector3(2f, 0f, 0f));
            TickFor(runner, HitWindow.Start);

            CollectionAssert.AreEqual(new[] { stickRight }, targetPicker.AskedDirections, "asked once, with the stick at the press");
            Assert.AreSame(pickedAtThePress, runner.CurrentActionTarget, "the target stays for the whole action");
            Assert.AreSame(pickedAtThePress, hitWindowListener.LastOpenedAgainst, "the hit goes to the target the action started with");
        }

        [Test]
        public void TheActionTarget_IsClearedWhenTheActionEnds()
        {
            ActionRunner runner = CreateRunner();
            targetPicker.TargetToGive = new PointTarget(new Vector3(0f, 0f, 2f));
            StartJab(runner);

            TickFor(runner, AttackSeconds);

            Assert.IsFalse(runner.IsPlaying);
            Assert.IsNull(runner.CurrentActionTarget);
        }

        [Test]
        public void AFollowUpInTheCombo_PicksItsTargetAgain()
        {
            ActionRunner runner = CreateRunner();
            PointTarget jabTarget = new PointTarget(new Vector3(0f, 0f, 2f));
            PointTarget crossTarget = new PointTarget(new Vector3(2f, 0f, 0f));
            Vector2 stickRight = new Vector2(1f, 0f);
            targetPicker.TargetToGive = jabTarget;
            StartJab(runner);

            targetPicker.TargetToGive = crossTarget;
            Press(kinds.Strike, stickRight);
            TickFor(runner, ComboWindow.Start);

            Assert.AreSame(cross, runner.CurrentAction);
            Assert.AreSame(crossTarget, runner.CurrentActionTarget, "consecutive attacks may go to different targets");
            CollectionAssert.AreEqual(new[] { Vector2.zero, stickRight }, targetPicker.AskedDirections);
        }

        [Test]
        public void AnActionStartedFromCode_PicksItsTargetStraightAhead()
        {
            ActionRunner runner = CreateRunner();
            stance.TryGetNode("Evade", out ChainNode evade);
            ActionDefinition flinch = Action("Flinch", 0.5f);

            runner.Interrupt(evade, Vector3.zero);
            runner.PlayAction(flinch, Vector3.zero);

            CollectionAssert.AreEqual(new[] { Vector2.zero, Vector2.zero }, targetPicker.AskedDirections);
        }

        [Test]
        public void WithoutATargetPicker_AnActionHasNoTarget_AndPlaysInPlace()
        {
            ActionRunner runner = CreateRunnerWithoutTargetPicker();
            StartJab(runner);
            Assert.IsTrue(runner.IsPlaying);
            Assert.IsNull(runner.CurrentActionTarget);

            Assert.DoesNotThrow(() => TickFor(runner, AttackSeconds));
            Assert.AreEqual(0, warpMover.MoveCount);
            Assert.AreEqual(1, hitWindowListener.OpenedCount);
            Assert.IsNull(hitWindowListener.LastOpenedAgainst);
        }

        [Test]
        public void Cancel_ClosesTheHitWindow_AndGoesIdle()
        {
            ActionRunner runner = CreateRunner();
            StartJab(runner);
            TickFor(runner, HitWindow.Start);
            Assert.IsTrue(hitWindowListener.IsOpen);

            runner.Cancel();

            Assert.IsFalse(hitWindowListener.IsOpen);
            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual("interrupted Jab", events.Log[events.Log.Count - 1]);
        }

        [Test]
        public void PlayAction_RunsAPlainActionOutsideTheCombo()
        {
            ActionDefinition flinch = Action("Flinch", 0.5f);
            ActionRunner runner = CreateRunner();

            runner.PlayAction(flinch, Vector3.zero);
            TickFor(runner, 0.3f);
            Assert.AreSame(flinch, runner.CurrentAction);
            Assert.IsNull(runner.CurrentAttack);

            TickFor(runner, 0.3f);
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void TheTrace_KeepsTheLastEightRecords_WithThePressesMarkedOnThem()
        {
            ActionRunner runner = CreateRunner();

            const int moreAttacksThanTheTraceHolds = 6;
            for (int i = 0; i < moreAttacksThanTheTraceHolds; i++)
            {
                StrikeAndPlayTheAttackToItsEnd(runner);
            }

            Assert.AreEqual(8, runner.Trace.Records.Count);
            Assert.IsTrue(runner.Trace.Current.IsIdle);

            ActionTrace.Record lastAttack = RecordBeforeTheLatest(runner);
            Assert.IsTrue(lastAttack.HasEnded);
            Assert.AreEqual(1f, lastAttack.EndedAt, 1e-5f);

            ActionTrace.Record idleBeforeTheLastAttack = runner.Trace.Records[runner.Trace.Records.Count - 3];
            Assert.IsTrue(idleBeforeTheLastAttack.IsIdle);
            Assert.AreEqual(1, idleBeforeTheLastAttack.Marks.Count, "the press that started the attack is marked on the idle stretch before it");
            Assert.AreEqual(ActionTrace.MarkStatus.Consumed, idleBeforeTheLastAttack.Marks[0].Status);
        }

        private ActionRunner CreateRunner() =>
            new ActionRunner(stance, intents, facts, new AlwaysConditionEvaluator(), kinds.InterruptKinds, driver, warpMover, hitWindowListener, events, targetPicker);

        private ActionRunner CreateRunnerWithoutTargetPicker() =>
            new ActionRunner(stance, intents, facts, new AlwaysConditionEvaluator(), kinds.InterruptKinds, driver, warpMover, hitWindowListener, events);

        private void Press(IntentKind kind) => intents.Push(kind, Vector2.zero);

        private void Press(IntentKind kind, Vector2 stickAtPress) => intents.Push(kind, stickAtPress);

        /// <summary>For a runner at the root: the press is taken on the first tick, so the jab starts at time zero.</summary>
        private void StartJab(ActionRunner runner)
        {
            Press(kinds.Strike);
            Tick(runner);
        }

        private void StrikeAndPlayTheAttackToItsEnd(ActionRunner runner)
        {
            Press(kinds.Strike);
            Tick(runner);
            TickFor(runner, AttackSeconds);
        }

        private static void Tick(ActionRunner runner, int count = 1, bool canStartFromIdle = true)
        {
            for (int i = 0; i < count; i++)
            {
                runner.Tick(TickSeconds, Vector3.zero, canStartFromIdle);
            }
        }

        private static void TickFor(ActionRunner runner, float seconds, bool canStartFromIdle = true)
        {
            Tick(runner, TicksIn(seconds), canStartFromIdle);
        }

        private static int TicksIn(float seconds) => Mathf.RoundToInt(seconds / TickSeconds);

        /// <summary>The runner only asks for movement; the test applies it, as the motor would.</summary>
        private Vector3 TickAndFollowTheWarp(ActionRunner runner, Vector3 position)
        {
            warpMover.TotalMovement = Vector3.zero;
            runner.Tick(TickSeconds, position, true);
            return position + warpMover.TotalMovement;
        }

        private static ActionTrace.Record LatestRecord(ActionRunner runner) => runner.Trace.Records[runner.Trace.Records.Count - 1];

        private static ActionTrace.Record RecordBeforeTheLatest(ActionRunner runner) => runner.Trace.Records[runner.Trace.Records.Count - 2];
    }
}
