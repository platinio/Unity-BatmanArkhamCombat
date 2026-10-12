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
        public void APulse_FlashesAsManyTimesAsFitTheCuesDuration()
        {
            const int flashesThatFit = 3;

            Tween pulse = TheTweenStartedBy(new PulseCue(), flashesThatFit * DefaultSecondsPerPulseFlash + 0.05f);

            Assert.AreEqual(LoopsPerFlash * flashesThatFit, pulse.Loops());
        }

        [Test]
        public void APulseShorterThanOneFlash_StillFlashesOnce()
        {
            Tween pulse = TheTweenStartedBy(new PulseCue(), DefaultSecondsPerPulseFlash / 2f);

            Assert.AreEqual(LoopsPerFlash, pulse.Loops());
        }

        [Test]
        public void AColourCue_OnABodyWithNothingToTint_DoesNothing()
        {
            TintedCueTarget targetWithoutARenderer = new TintedCueTarget(body.transform, bodyRenderer: null);

            Assert.DoesNotThrow(() => new PulseCue().Play(targetWithoutARenderer, new PresentationCue(firesAt: 0f, new PulseCue(), 0.6f)));
            Assert.IsNull(DOTween.TweensById(targetWithoutARenderer.TweenId));
        }

        private sealed class TintedCueTarget : ICueTarget
        {
            public TintedCueTarget(Transform body, Renderer bodyRenderer)
            {
                Body = body;
                BodyRenderer = bodyRenderer;
                StandingPose = BodyPose.Of(body);
                RestPose = StandingPose;
            }

            public Transform Body { get; }
            public Transform Fist => null;
            public Renderer BodyRenderer { get; }
            public BodyPose RestPose { get; private set; }
            public BodyPose StandingPose { get; }
            public Vector3 FistRestLocalPosition => Vector3.zero;
            public object TweenId => this;

            public void RestAt(BodyPose pose) => RestPose = pose;
        }
    }
}
