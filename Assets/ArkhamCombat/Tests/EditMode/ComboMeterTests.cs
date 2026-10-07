using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ComboMeterTests
    {
        private RecordingEvents events;
        private ComboMeter meter;

        [SetUp]
        public void SetUp()
        {
            events = new RecordingEvents();
            meter = new ComboMeter(new ComboMeterSettings(2f, 3, 5, 8), events);
        }

        [TestCase(ComboIncrementReason.StrikeLanded)]
        [TestCase(ComboIncrementReason.CounterSucceeded)]
        [TestCase(ComboIncrementReason.EvadeSucceeded)]
        public void EveryIncrementReason_RaisesTheCount(ComboIncrementReason reason)
        {
            meter.Increment(reason);

            Assert.AreEqual(1, meter.Count);
            Assert.AreEqual("changed 1 0", events.Log[0]);
        }

        [TestCase(ComboResetReason.PlayerHit)]
        [TestCase(ComboResetReason.Whiff)]
        [TestCase(ComboResetReason.Timeout)]
        public void EveryResetReason_ZeroesTheCount_AndSaysWhy(ComboResetReason reason)
        {
            meter.Increment(ComboIncrementReason.StrikeLanded);
            events.Log.Clear();

            meter.Reset(reason);

            Assert.AreEqual(0, meter.Count);
            Assert.AreEqual($"reset {reason}", events.Log[0]);
            Assert.AreEqual("changed 0 0", events.Log[1]);
        }

        [Test]
        public void ResetAtZero_IsSilent()
        {
            meter.Reset(ComboResetReason.Whiff);

            Assert.IsEmpty(events.Log);
        }

        [Test]
        public void Tier_IsTheNumberOfThresholdsReached()
        {
            int[] expected = { 0, 0, 1, 1, 2, 2, 2, 3, 3 };

            for (int i = 1; i <= expected.Length; i++)
            {
                meter.Increment(ComboIncrementReason.StrikeLanded);
                Assert.AreEqual(expected[i - 1], meter.Tier, $"tier at count {i}");
            }
        }

        [Test]
        public void TheTimeout_ResetsOnlyOnceTheMeterHasSomethingToLose()
        {
            meter.Tick(10f);
            Assert.IsEmpty(events.Log, "nothing to time out at zero");

            meter.Increment(ComboIncrementReason.StrikeLanded);
            meter.Tick(1.9f);
            Assert.AreEqual(1, meter.Count, "not yet");

            meter.Tick(0.1f);
            Assert.AreEqual(0, meter.Count);
            Assert.AreEqual("reset Timeout", events.Log[1]);
        }

        [Test]
        public void AnIncrement_RestartsTheTimeout()
        {
            meter.Increment(ComboIncrementReason.StrikeLanded);
            meter.Tick(1.5f);
            meter.Increment(ComboIncrementReason.EvadeSucceeded);
            meter.Tick(1.5f);

            Assert.AreEqual(2, meter.Count, "the second increment bought another full timeout");
        }
    }
}
