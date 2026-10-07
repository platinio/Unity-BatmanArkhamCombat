using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ActionRunnerTests
    {
        private AttackDefinition jab;
        private AttackDefinition cross;
        private Stance stance;
        private IntentBuffer intents;
        private CombatContext context;
        private NullPresentationDriver driver;
        private RecordingSink sink;
        private RecordingHits hits;
        private RecordingEvents events;

        [SetUp]
        public void SetUp()
        {
            jab = Attack("Jab", 1f, new Window(0.3f, 0.5f), new Window(0.5f, 0.9f), new Window(0f, 0.6f), new Window(0f, 0.3f), 1f, 4f);
            cross = Attack("Cross", 1f, new Window(0.3f, 0.5f), new Window(0.5f, 0.9f), new Window(0f, 0.3f), new Window(0f, 0.3f), 1f, 4f);
            stance = Stance("Neutral",
                new[]
                {
                    new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "S1")),
                    new ChainNode("S1", jab, new Edge(IntentKind.Strike, "S2")),
                    new ChainNode("S2", cross, new Edge(IntentKind.Strike, "S1")),
                    new ChainNode("Evade", cross)
                },
                new[] { new Edge(IntentKind.Evade, "Evade") },
                chainResetSeconds: 0.5f);

            intents = Buffer(lifetime: 10f);
            context = new CombatContext();
            driver = new NullPresentationDriver();
            sink = new RecordingSink();
            hits = new RecordingHits();
            events = new RecordingEvents();
        }

        private ActionRunner Runner() => new ActionRunner(stance, intents, context, new AlwaysConditionEvaluator(), driver, sink, hits, events);

        private static void Tick(ActionRunner runner, int ticks, float dt = 0.1f, bool canStart = true)
        {
            for (int i = 0; i < ticks; i++)
            {
                runner.Tick(dt, Vector3.zero, canStart);
            }
        }

        [Test]
        public void AStrikeWhileIdle_StartsTheChainFromTheRoot()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 1);

            Assert.IsTrue(runner.IsPlaying);
            Assert.AreSame(jab, runner.CurrentAction);
            Assert.AreEqual("S1", runner.CurrentNode.Id);
            Assert.AreEqual("start Jab", events.Log[0]);
        }

        [Test]
        public void NothingStarts_WhenTheCharacterMayNotStartAChain()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 3, canStart: false);

            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual(1, intents.Entries.Count, "the press is kept for when control returns");
        }

        [Test]
        public void TheActiveWindow_ArmsOnceAndDisarmsOnce()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 1);
            Tick(runner, 2);
            Assert.AreEqual(0, hits.Armed, "t 0.2: not yet");

            Tick(runner, 1);
            Assert.AreEqual(1, hits.Armed, "t 0.3: armed");
            Assert.IsTrue(runner.HitArmed);
            Assert.AreSame(jab, hits.LastAttack);

            Tick(runner, 1);
            Assert.AreEqual(0, hits.Disarmed, "t 0.4: still armed");

            Tick(runner, 1);
            Assert.AreEqual(1, hits.Disarmed, "t 0.5: disarmed");

            Tick(runner, 10);
            Assert.AreEqual(1, hits.Armed);
            Assert.AreEqual(1, hits.Disarmed);
        }

        [Test]
        public void AnInterrupt_DisarmsTheHitbox_AndStartsTheDestinationImmediately()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            Tick(runner, 3);
            Assert.IsTrue(hits.IsArmed, "t 0.3: armed before the interrupt");

            intents.Push(IntentKind.Evade, Vector2.zero);
            Tick(runner, 1);

            Assert.IsFalse(hits.IsArmed, "nothing stays armed behind the move that was cut");
            Assert.IsFalse(runner.HitArmed);
            Assert.AreSame(cross, runner.CurrentAction);
            Assert.AreEqual("Evade", runner.CurrentNode.Id);
            Assert.AreEqual(0f, driver.NormalizedTime, 1e-5f, "the destination started from zero");
            CollectionAssert.Contains(events.Log, "cut Jab");
            CollectionAssert.Contains(events.Log, "interrupt Cross");
        }

        [Test]
        public void AnEvadeOutsideItsWindow_DoesNotInterrupt()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            Tick(runner, 7);
            intents.Push(IntentKind.Evade, Vector2.zero);

            Tick(runner, 1);

            Assert.AreSame(jab, runner.CurrentAction);
        }

        [Test]
        public void AFollowUp_IsTakenOnlyWhileTheCancelWindowIsOpen()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 4);
            Assert.AreSame(jab, runner.CurrentAction, "t 0.4: cancel window closed, the press waits");

            Tick(runner, 1);
            Assert.AreSame(cross, runner.CurrentAction, "t 0.5: the cancel window opened and the press was spent");
            Assert.AreEqual("S2", runner.CurrentNode.Id);
            Assert.AreEqual(0, intents.Entries.Count);
        }

        [Test]
        public void ClipEnd_WithNoFollowUp_HandsBackToIdle_AndKeepsTheNodeForTheResetTime()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            Tick(runner, 10);

            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual("end Jab", events.Log[events.Log.Count - 1]);
            Assert.AreEqual("S1", runner.CurrentNode.Id, "within chainResetSeconds the position is kept");

            Tick(runner, 5, canStart: false);
            Assert.AreEqual("Neutral", runner.CurrentNode.Id, "after chainResetSeconds the position collapsed to the root");
        }

        [Test]
        public void APressDuringRecovery_StartsTheNextChain_FromTheKeptNode()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            Tick(runner, 9);
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 1);
            Assert.IsFalse(runner.IsPlaying, "t 1.0: the jab ended; the press arrived after the cancel window");

            Tick(runner, 1);
            Assert.AreSame(cross, runner.CurrentAction, "idle at S1: the press continues the chain");
        }

        [Test]
        public void WithAZeroResetTime_ClipEndReturnsToTheRoot()
        {
            stance.Configure("Neutral", stance.Nodes, stance.GlobalEdges, chainResetSeconds: 0f);
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 1);
            Tick(runner, 10);

            Tick(runner, 1, canStart: false);
            Assert.AreEqual("Neutral", runner.CurrentNode.Id);
        }

        [Test]
        public void TheWarp_ArrivesAtStrikeDistance_WhenTheWindowCloses()
        {
            ActionRunner runner = Runner();
            runner.Target = new PointTarget(new Vector3(0f, 0f, 3f));
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 1);
            Vector3 position = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                sink.Total = Vector3.zero;
                runner.Tick(0.1f, position, true);
                position += sink.Total;
            }

            Assert.AreEqual(2f, position.z, 1e-3f, "stopped strikeDistance short of the target");
            Assert.IsFalse(runner.WarpOpen);
            Assert.IsFalse(runner.WarpRefused);
        }

        [Test]
        public void TheWarp_RefusesBeyondMaxLunge()
        {
            ActionRunner runner = Runner();
            runner.Target = new PointTarget(new Vector3(0f, 0f, 10f));
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 4);

            Assert.IsTrue(runner.WarpRefused);
            Assert.AreEqual(0, sink.Calls, "no displacement at all");
            Assert.IsTrue(runner.Trace.Records[runner.Trace.Records.Count - 1].WarpRefused);
        }

        [Test]
        public void TheWarp_DoesNothingWithoutATarget()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);

            Tick(runner, 4);

            Assert.AreEqual(0, sink.Calls);
            Assert.IsFalse(runner.WarpRefused);
        }

        [Test]
        public void Cancel_DisarmsAndGoesIdle()
        {
            ActionRunner runner = Runner();
            intents.Push(IntentKind.Strike, Vector2.zero);
            Tick(runner, 4);
            Assert.IsTrue(hits.IsArmed);

            runner.Cancel();

            Assert.IsFalse(hits.IsArmed);
            Assert.IsFalse(runner.IsPlaying);
            Assert.AreEqual("cut Jab", events.Log[events.Log.Count - 1]);
        }

        [Test]
        public void PlayAction_RunsAPlainActionOutsideTheChain()
        {
            ActionDefinition flinch = Action("Flinch", 0.5f);
            ActionRunner runner = Runner();

            runner.PlayAction(flinch, Vector3.zero);
            Tick(runner, 3);
            Assert.AreSame(flinch, runner.CurrentAction);
            Assert.IsNull(runner.CurrentAttack);

            Tick(runner, 3);
            Assert.IsFalse(runner.IsPlaying);
        }

        [Test]
        public void TheTrace_KeepsTheLastEightRecords_WithTheirIntentMarks()
        {
            ActionRunner runner = Runner();

            for (int i = 0; i < 6; i++)
            {
                intents.Push(IntentKind.Strike, Vector2.zero);
                Tick(runner, 11);
            }

            Assert.AreEqual(8, runner.Trace.Records.Count);
            Assert.IsTrue(runner.Trace.Current.IsIdle);

            ActionTrace.Record last = runner.Trace.Records[runner.Trace.Records.Count - 2];
            Assert.IsTrue(last.Ended);
            Assert.AreEqual(1f, last.EndedAt, 1e-5f);

            ActionTrace.Record idleBefore = runner.Trace.Records[runner.Trace.Records.Count - 3];
            Assert.IsTrue(idleBefore.IsIdle);
            Assert.AreEqual(1, idleBefore.Marks.Count, "the press that started the action was marked on the idle stretch");
            Assert.AreEqual(ActionTrace.MarkStatus.Consumed, idleBefore.Marks[0].Status);
        }
    }
}
