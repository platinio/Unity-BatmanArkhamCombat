using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // One flash is a tint and an untint, so a tween that loops twice flashes once.
    public class ColourCueTests
    {
        private const int LoopsPerFlash = 2;
        private const float DefaultSecondsPerPulseFlash = 0.2f;

        private GameObject body;
        private TintedCueTarget target;

        [SetUp]
        public void SetUp()
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.hideFlags = HideFlags.HideAndDontSave;
            target = new TintedCueTarget(body.transform, body.GetComponent<Renderer>());
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.Kill(target.TweenId);
            Object.DestroyImmediate(body);
        }

        private Tween TheTweenStartedBy(ICueKind kind, float cueSeconds)
        {
            kind.Play(target, new PresentationCue(firesAt: 0f, kind, cueSeconds));

            List<Tween> tweens = DOTween.TweensById(target.TweenId);
            Assert.AreEqual(1, tweens.Count, "a colour cue is one tween");
            return tweens[0];
        }

        [Test]
        public void AFlash_TintsTheBodyOnceAndBack_OverTheCuesDuration()
        {
            const float cueSeconds = 0.3f;

            Tween flash = TheTweenStartedBy(new FlashCue(), cueSeconds);

            Assert.AreEqual(LoopsPerFlash, flash.Loops());
            Assert.AreEqual(cueSeconds, flash.Duration(includeLoops: true), 1e-4f);
        }

        [Test]
        public void APulse_FlashesOncePerFlashLength_WhenTheCueIsAWholeNumberOfThem()
        {
            const int flashes = 3;

            Tween pulse = TheTweenStartedBy(new PulseCue(), flashes * DefaultSecondsPerPulseFlash);

            Assert.AreEqual(LoopsPerFlash * flashes, pulse.Loops());
        }

        [TestCase(0.45f, 2, TestName = "APulseALittleOverTwoFlashLengths_FlashesTwice")]
        [TestCase(0.75f, 4, TestName = "APulseOfNearlyFourFlashLengths_FlashesFourTimes")]
        public void APulseThatIsNotAWholeNumberOfFlashes_PlaysTheNearestWholeNumber(float cueSeconds, int expectedFlashes)
        {
            Tween pulse = TheTweenStartedBy(new PulseCue(), cueSeconds);

            Assert.AreEqual(LoopsPerFlash * expectedFlashes, pulse.Loops());
        }

        [TestCase(0.45f)]
        [TestCase(0.6f)]
        [TestCase(0.75f)]
        public void APulse_EndsWhenItsCueDoes(float cueSeconds)
        {
            Tween pulse = TheTweenStartedBy(new PulseCue(), cueSeconds);

            Assert.AreEqual(cueSeconds, pulse.Duration(includeLoops: true), 1e-4f);
        }

        [Test]
        public void APulseShorterThanOneFlash_StillFlashesOnce()
        {
            Tween pulse = TheTweenStartedBy(new PulseCue(), DefaultSecondsPerPulseFlash / 4f);

            Assert.AreEqual(LoopsPerFlash, pulse.Loops());
        }

        [Test]
        public void AFlash_TintsEveryRendererOfTheBody()
        {
            GameObject secondPart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            secondPart.hideFlags = HideFlags.HideAndDontSave;
            Renderer[] parts = { body.GetComponent<Renderer>(), secondPart.GetComponent<Renderer>() };
            TintedCueTarget twoPartTarget = new TintedCueTarget(body.transform, parts);
            try
            {
                new FlashCue().Play(twoPartTarget, new PresentationCue(firesAt: 0f, new FlashCue(), 0.3f));
                DOTween.TweensById(twoPartTarget.TweenId)[0].Goto(0.15f);

                Assert.IsTrue(parts[0].HasPropertyBlock(), "the body is tinted");
                Assert.IsTrue(parts[1].HasPropertyBlock(), "its second part is tinted too");
            }
            finally
            {
                DOTween.Kill(twoPartTarget.TweenId);
                Object.DestroyImmediate(secondPart);
            }
        }

        [Test]
        public void AColourCue_OnABodyWithNothingToTint_DoesNothing()
        {
            TintedCueTarget targetWithoutARenderer = new TintedCueTarget(body.transform);

            Assert.DoesNotThrow(() => new PulseCue().Play(targetWithoutARenderer, new PresentationCue(firesAt: 0f, new PulseCue(), 0.6f)));
            Assert.IsNull(DOTween.TweensById(targetWithoutARenderer.TweenId));
        }

        private sealed class TintedCueTarget : ICueTarget
        {
            public TintedCueTarget(Transform body, params Renderer[] tintedRenderers)
            {
                Body = body;
                TintedRenderers = tintedRenderers;
                StandingPose = BodyPose.Of(body);
                RestPose = StandingPose;
            }

            public Transform Body { get; }
            public Transform Fist => null;
            public Renderer BodyRenderer => TintedRenderers.Count > 0 ? TintedRenderers[0] : null;
            public IReadOnlyList<Renderer> TintedRenderers { get; }
            public BodyPose RestPose { get; private set; }
            public BodyPose StandingPose { get; }
            public Vector3 FistRestLocalPosition => Vector3.zero;
            public object TweenId => this;

            public void RestAt(BodyPose pose) => RestPose = pose;
        }
    }
}
