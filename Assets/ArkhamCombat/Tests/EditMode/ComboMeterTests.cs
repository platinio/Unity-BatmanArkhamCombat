using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ComboMeterTests
    {
        private const float TimeoutSeconds = 2f;

        private RecordingEvents events;
        private ComboMeter meter;

        [SetUp]
        public void SetUp()
        {
            events = new RecordingEvents();
            meter = new ComboMeter(new ComboMeterSettings(TimeoutSeconds, tierThresholds: new[] { 3, 5, 8 }), events);
        }

        [TestCase(ComboIncrementReason.StrikeLanded)]
        [TestCase(ComboIncrementReason.CounterSucceeded)]
        [TestCase(ComboIncrementReason.EvadeSucceeded)]
        public void EveryIncrementReason_RaisesTheCount(ComboIncrementReason reason)
        {
            meter.Increment(reason);

            Assert.AreEqual(1, meter.Count);
            Assert.AreEqual("combo 1 tier 0", events.Log[0]);
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
            Assert.AreEqual("combo 0 tier 0", events.Log[1]);
        }

        [Test]
        public void AResetAtZero_RaisesNoEvent()
        {
            meter.Reset(ComboResetReason.Whiff);

            Assert.IsEmpty(events.Log);
        }

        [Test]
        public void Tier_IsTheNumberOfThresholdsReached()
        {
            int[] expectedTierAtCount = { 0, 0, 1, 1, 2, 2, 2, 3, 3 };

            for (int count = 1; count <= expectedTierAtCount.Length; count++)
            {
                meter.Increment(ComboIncrementReason.StrikeLanded);
                Assert.AreEqual(expectedTierAtCount[count - 1], meter.Tier, $"tier at count {count}");
            }
        }

        [Test]
        public void TheTimeout_ResetsTheMeter_OnlyWhenTheCountIsAboveZero()
        {
            meter.Tick(10f);
            Assert.IsEmpty(events.Log, "nothing to time out at zero");

            meter.Increment(ComboIncrementReason.StrikeLanded);
            meter.Tick(1.9f);
            Assert.AreEqual(1, meter.Count, "just before the timeout the count is kept");

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

            Assert.AreEqual(2, meter.Count, "the second increment started the timeout over");
        }
    }
}
