using System.Collections.Generic;
using ArkhamCombat.Combat;
using NUnit.Framework;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ComboMeterTests
    {
        private const float TimeoutSeconds = 2f;

        private List<string> raisedEvents;
        private ComboMeter meter;

        [SetUp]
        public void SetUp()
        {
            raisedEvents = new List<string>();
            meter = new ComboMeter(new FixedComboMeterSettings(TimeoutSeconds, 3, 5, 8));
            meter.ComboChanged += (count, tier) => raisedEvents.Add($"combo {count} tier {tier}");
            meter.ComboReset += reason => raisedEvents.Add($"reset {reason}");
        }

        [TestCase(ComboIncrementReason.StrikeLanded)]
        [TestCase(ComboIncrementReason.CounterSucceeded)]
        [TestCase(ComboIncrementReason.EvadeSucceeded)]
        public void EveryIncrementReason_RaisesTheCount(ComboIncrementReason reason)
        {
            meter.Increment(reason);

            Assert.AreEqual(1, meter.Count);
            Assert.AreEqual("combo 1 tier 0", raisedEvents[0]);
        }

        [TestCase(ComboResetReason.PlayerHit)]
        [TestCase(ComboResetReason.Whiff)]
        [TestCase(ComboResetReason.Timeout)]
        public void EveryResetReason_ZeroesTheCount_AndSaysWhy(ComboResetReason reason)
        {
            meter.Increment(ComboIncrementReason.StrikeLanded);
            raisedEvents.Clear();

            meter.Reset(reason);

            Assert.AreEqual(0, meter.Count);
            Assert.AreEqual($"reset {reason}", raisedEvents[0]);
            Assert.AreEqual("combo 0 tier 0", raisedEvents[1]);
        }

        [Test]
        public void AResetAtZero_RaisesNoEvent()
        {
            meter.Reset(ComboResetReason.Whiff);

            Assert.IsEmpty(raisedEvents);
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
            Assert.IsEmpty(raisedEvents, "nothing to time out at zero");

            meter.Increment(ComboIncrementReason.StrikeLanded);
            meter.Tick(1.9f);
            Assert.AreEqual(1, meter.Count, "just before the timeout the count is kept");

            meter.Tick(0.1f);
            Assert.AreEqual(0, meter.Count);
            Assert.AreEqual("reset Timeout", raisedEvents[1]);
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
