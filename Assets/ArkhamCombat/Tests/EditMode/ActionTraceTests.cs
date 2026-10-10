using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using NUnit.Framework;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class ActionTraceTests
    {
        private const float PressLifetimeSeconds = 10f;

        private TestIntentKinds kinds;
        private IntentBuffer intents;
        private ActionTrace trace;

        [SetUp]
        public void SetUp()
        {
            kinds = new TestIntentKinds(PressLifetimeSeconds);
            intents = new IntentBuffer();
            trace = new ActionTrace(intents);
        }

        [TearDown]
        public void TearDown() => kinds.Destroy();

        [Test]
        public void APress_IsMarkedOnTheCurrentRecord()
        {
            intents.Push(kinds.Strike, Vector2.zero);

            Assert.AreEqual(1, trace.Current.Marks.Count);
        }

        [Test]
        public void AfterItStopsListening_APressLeavesNoMark()
        {
            trace.StopListening();

            intents.Push(kinds.Strike, Vector2.zero);

            Assert.AreEqual(0, trace.Current.Marks.Count);
        }
    }
}
