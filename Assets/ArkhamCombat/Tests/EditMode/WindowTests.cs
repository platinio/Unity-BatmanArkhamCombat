using ArkhamCombat.Combat;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    public class WindowTests
    {
        [Test]
        public void Contains_IsHalfOpen()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.Contains(0.19f));
            Assert.IsTrue(window.Contains(0.2f), "the start belongs to the window");
            Assert.IsTrue(window.Contains(0.49f));
            Assert.IsFalse(window.Contains(0.5f), "the end does not, so a close fires on the tick that reaches it");
        }

        [Test]
        public void Opened_FiresOnTheTickThatCrossesTheStart_AndOnlyThatOne()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.Opened(0f, 0.1f));
            Assert.IsTrue(window.Opened(0.1f, 0.25f));
            Assert.IsFalse(window.Opened(0.25f, 0.4f), "already inside: no second open");
        }

        [Test]
        public void Opened_DoesNotRefire_WhenPreviousTickLandedExactlyOnTheStart()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsTrue(window.Opened(0.1f, 0.2f), "landing exactly on the start opens");
            Assert.IsFalse(window.Opened(0.2f, 0.3f), "the next tick must not open again");
        }

        [Test]
        public void Closed_FiresOnTheTickThatReachesTheEnd()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsFalse(window.Closed(0.3f, 0.45f));
            Assert.IsTrue(window.Closed(0.45f, 0.5f));
            Assert.IsFalse(window.Closed(0.5f, 0.7f));
        }

        [Test]
        public void ALargeStep_OpensAndClosesInTheSameTick()
        {
            Window window = new Window(0.2f, 0.5f);

            Assert.IsTrue(window.Opened(0.1f, 0.9f));
            Assert.IsTrue(window.Closed(0.1f, 0.9f));
        }

        [Test]
        public void AZeroLengthWindow_ContainsNothing_ButOpensAndClosesOnce()
        {
            Window window = new Window(0.4f, 0.4f);

            Assert.IsTrue(window.IsZeroLength);
            Assert.IsFalse(window.Contains(0.4f));
            Assert.IsTrue(window.Opened(0.3f, 0.5f));
            Assert.IsTrue(window.Closed(0.3f, 0.5f));
            Assert.IsFalse(window.Opened(0.5f, 0.6f));
        }

        [Test]
        public void WrapAround_OnLoop_CrossesEdgesOnBothSidesOfTheSeam()
        {
            Window late = new Window(0.9f, 1f);
            Window early = new Window(0f, 0.1f);

            // previous 0.85 -> looped back to 0.05: passes through 0.9, 1.0 and 0.0.
            Assert.IsTrue(late.Opened(0.85f, 0.05f), "start at 0.9 was passed before the seam");
            Assert.IsTrue(late.Closed(0.85f, 0.05f), "end at 1.0 was passed at the seam");
            Assert.IsTrue(early.Opened(0.85f, 0.05f), "start at 0 was passed after the seam");
            Assert.IsFalse(early.Closed(0.85f, 0.05f), "end at 0.1 is still ahead");
        }

        [Test]
        public void WrapAround_DoesNotRefireAnEdgeTheLastTickLandedOn()
        {
            Window late = new Window(0.9f, 1f);

            Assert.IsFalse(late.Closed(1f, 0.05f), "the previous tick already sat on 1.0");
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
