using System.Collections.Generic;
using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ActionClockTests
    {
        private ActionClock clock;
        private RecordingCue cue;
        private List<string> fired;

        [SetUp]
        public void SetUp()
        {
            clock = new ActionClock();
            cue = new RecordingCue(() => clock.NormalizedTime);
            fired = new List<string>();
            clock.CueDue += c =>
            {
                fired.Add($"{c.At:0.00}");
                cue.Play(null, c);
            };
        }

        [Test]
        public void CuesFireOnceEach_InTimeOrder_OnTheTickThatCrossesThem()
        {
            ActionDefinition action = Action("A", 1f,
                new PresentationCue(0.75f, cue),
                new PresentationCue(0.25f, cue),
                new PresentationCue(0.5f, cue));

            clock.Play(action);
            for (int i = 0; i < 10; i++)
            {
                clock.Tick(0.1f);
            }

            CollectionAssert.AreEqual(new[] { "0.25", "0.50", "0.75" }, fired);
            Assert.AreEqual(3, cue.FiredAt.Count);
            Assert.AreEqual(0.3f, cue.FiredAt[0], 1e-4f, "the 0.25 cue fired on the tick that reached 0.3");
        }

        [Test]
        public void ACueAtZero_FiresOnPlay()
        {
            clock.Play(Action("A", 1f, new PresentationCue(0f, cue)));

            CollectionAssert.AreEqual(new[] { "0.00" }, fired);
        }

        [Test]
        public void ACueAtOne_FiresWhenTheClockReachesTheEnd()
        {
            clock.Play(Action("A", 0.5f, new PresentationCue(1f, cue)));
            clock.Tick(0.4f);
            Assert.IsEmpty(fired);

            clock.Tick(0.2f);
            CollectionAssert.AreEqual(new[] { "1.00" }, fired);
            Assert.IsTrue(clock.Finished);
            Assert.AreEqual(1f, clock.NormalizedTime);
        }

        [Test]
        public void NothingFires_WhileSpeedIsZero()
        {
            clock.Play(Action("A", 1f, new PresentationCue(0.1f, cue)));
            clock.Speed = 0f;

            for (int i = 0; i < 20; i++)
            {
                clock.Tick(0.1f);
            }

            Assert.IsEmpty(fired);
            Assert.AreEqual(0f, clock.NormalizedTime);

            clock.Speed = 1f;
            clock.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { "0.10" }, fired);
        }

        [Test]
        public void Speed_ScalesTheClock()
        {
            clock.Play(Action("A", 1f));
            clock.Speed = 2f;
            clock.Tick(0.25f);

            Assert.AreEqual(0.5f, clock.NormalizedTime, 1e-5f);
        }

        [Test]
        public void Stop_DropsPendingCues()
        {
            clock.Play(Action("A", 1f, new PresentationCue(0.5f, cue)));
            clock.Tick(0.2f);
            clock.Stop();
            clock.Tick(1f);

            Assert.IsEmpty(fired);
            Assert.IsFalse(clock.IsPlaying);
            Assert.AreEqual(0f, clock.NormalizedTime);
        }

        [Test]
        public void Play_ReplacesTheRunningAction_AndItsPendingCues()
        {
            clock.Play(Action("A", 1f, new PresentationCue(0.5f, cue)));
            clock.Tick(0.4f);
            clock.Play(Action("B", 1f, new PresentationCue(0.3f, cue)));
            clock.Tick(0.35f);

            CollectionAssert.AreEqual(new[] { "0.30" }, fired, "only B's cue, and only once");
        }
    }
}
