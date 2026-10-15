using ArkhamCombat.Presentation;
using NUnit.Framework;

namespace ArkhamCombat.Tests
{
    public class ClipBlendTests
    {
        private const float FullBlendSeconds = 0.2f;

        private ClipBlend blend;

        [SetUp]
        public void SetUp() => blend = new ClipBlend();

        [Test]
        public void AFullBlend_TakesTheSecondsItWasGiven_WhateverTheTicks()
        {
            blend.BlendTo(1f, FullBlendSeconds);

            blend.Step(0.05f);
            Assert.AreEqual(0.25f, blend.Weight, 1e-4f);
            blend.Step(0.15f);
            Assert.AreEqual(1f, blend.Weight, 1e-4f);
            Assert.IsTrue(blend.HasReachedItsTarget);
        }

        [Test]
        public void ABlendThatStartsPartWay_TakesProportionallyLess()
        {
            blend.BlendTo(0.5f, secondsForAFullBlend: 0f);
            blend.BlendTo(1f, FullBlendSeconds);

            blend.Step(FullBlendSeconds * 0.5f);

            Assert.AreEqual(1f, blend.Weight, 1e-4f);
        }

        [Test]
        public void CutTo_LandsAtOnce_AndStaysThere()
        {
            blend.BlendTo(1f, FullBlendSeconds);

            blend.CutTo(0.25f);

            Assert.AreEqual(0.25f, blend.Weight);
            Assert.AreEqual(0.25f, blend.Target);
            Assert.IsTrue(blend.HasReachedItsTarget);
            blend.Step(FullBlendSeconds);
            Assert.AreEqual(0.25f, blend.Weight, "a cut has nowhere left to go");
        }

        [Test]
        public void ZeroSeconds_IsACut()
        {
            blend.BlendTo(1f, secondsForAFullBlend: 0f);

            Assert.AreEqual(1f, blend.Weight);
        }

        [Test]
        public void TheWeightNeverOvershootsItsTarget()
        {
            blend.BlendTo(1f, FullBlendSeconds);

            blend.Step(FullBlendSeconds * 3f);

            Assert.AreEqual(1f, blend.Weight);
        }

        [Test]
        public void BlendingBackDown_StepsTheSameWay()
        {
            blend.BlendTo(1f, secondsForAFullBlend: 0f);
            blend.BlendTo(0f, FullBlendSeconds);

            blend.Step(0.1f);

            Assert.AreEqual(0.5f, blend.Weight, 1e-4f);
        }
    }
}
