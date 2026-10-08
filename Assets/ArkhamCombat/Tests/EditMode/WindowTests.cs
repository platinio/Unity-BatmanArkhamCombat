using ArkhamCombat.Combat;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    public class WindowTests
    {
        [Test]
        public void Contains_IncludesTheStart_ButNotTheEnd()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.Contains(0.19f));
            Assert.IsTrue(window.Contains(0.2f), "the start belongs to the window");
            Assert.IsTrue(window.Contains(0.49f));
            Assert.IsFalse(window.Contains(0.5f), "the end does not, so a close fires on the tick that reaches it");
        }

        [Test]
        public void IsOpen_FiresOnTheTickThatCrossesTheStart_AndOnlyThatOne()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.IsOpen(0f, 0.1f));
            Assert.IsTrue(window.IsOpen(0.1f, 0.25f));
            Assert.IsFalse(window.IsOpen(0.25f, 0.4f), "already inside: no second open");
        }

        [Test]
        public void IsOpen_DoesNotFireAgain_WhenThePreviousTickLandedExactlyOnTheStart()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsTrue(window.IsOpen(0.1f, 0.2f), "landing exactly on the start opens");
            Assert.IsFalse(window.IsOpen(0.2f, 0.3f), "the next tick must not open again");
        }

        [Test]
        public void IsClosed_FiresOnTheTickThatReachesTheEnd()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.IsClosed(0.3f, 0.45f));
            Assert.IsTrue(window.IsClosed(0.45f, 0.5f));
            Assert.IsFalse(window.IsClosed(0.5f, 0.7f));
        }

        [Test]
        public void ATickThatStepsOverTheWholeWindow_OpensAndClosesItInThatTick()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsTrue(window.IsOpen(0.1f, 0.9f));
            Assert.IsTrue(window.IsClosed(0.1f, 0.9f));
        }

        [Test]
        public void AZeroLengthWindow_ContainsNothing_ButOpensAndClosesOnce()
        {
            Window window = new Window(0.4f, 0.4f);

            Assert.IsTrue(window.IsZeroLength);
            Assert.IsFalse(window.Contains(0.4f));
            Assert.IsTrue(window.IsOpen(0.3f, 0.5f));
            Assert.IsTrue(window.IsClosed(0.3f, 0.5f));
            Assert.IsFalse(window.IsOpen(0.5f, 0.6f));
        }

        [Test]
        public void WhenALoopingClockStartsOver_EdgesBeforeAndAfterTheLoopAreBothCrossed()
        {
            Window late = new Window(0.9f, 1f);
            Window early = new Window(0f, 0.1f);
            const float beforeTheLoop = 0.85f;
            const float afterTheLoop = 0.05f;

            Assert.IsTrue(late.IsOpen(beforeTheLoop, afterTheLoop), "the start at 0.9 was passed before the loop");
            Assert.IsTrue(late.IsClosed(beforeTheLoop, afterTheLoop), "the end at 1.0 was reached as the clock looped");
            Assert.IsTrue(early.IsOpen(beforeTheLoop, afterTheLoop), "the start at 0 was passed after the loop");
            Assert.IsFalse(early.IsClosed(beforeTheLoop, afterTheLoop), "the end at 0.1 is still ahead");
        }

        [Test]
        public void WhenALoopingClockStartsOver_AnEdgeThePreviousTickLandedOnDoesNotFireAgain()
        {
            Window late = new Window(0.9f, 1f);

            Assert.IsFalse(late.IsClosed(1f, 0.05f), "the previous tick already landed on 1.0");
        }

        [Test]
        public void IsValid_RejectsAnEndBeforeTheStart()
        {
            Assert.IsTrue(new Window(0.2f, 0.2f).IsValid(out _), "zero length is allowed");
            Assert.IsFalse(new Window(0.5f, 0.2f).IsValid(out string error));
            StringAssert.Contains("before it starts", error);
        }
    }
}
