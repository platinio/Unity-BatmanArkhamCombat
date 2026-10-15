using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    public class HeldPoseCueTests
    {
        private const float FlatOnTheGroundDegrees = 90f;
        private const float HalfWayDownDegrees = 60f;
        private const float CueSeconds = 0.3f;
        private const float CapsuleHalfHeight = 1f;
        private const float CapsuleHalfDepth = 0.5f;
        private const float AnySquash = 0.3f;
        private const float AnyStrength = 1f;
        private const float Tolerance = 1e-3f;

        private GameObject body;
        private RecordingCueTarget target;

        [SetUp]
        public void SetUp()
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.hideFlags = HideFlags.HideAndDontSave;
            target = new RecordingCueTarget(body.transform, body.GetComponent<Renderer>());
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.Kill(target.TweenId);
            Object.DestroyImmediate(body);
        }

        private void Play(ICueKind kind, float strength) =>
            kind.Play(target, new PresentationCue(firesAt: 0f, kind, CueSeconds, strength));

        // Edit mode ticks no tween, so the tweens a cue started are jumped to their end.
        private void PlayToTheEnd(ICueKind kind, float strength)
        {
            Play(kind, strength);
            DOTween.Complete(target.TweenId);
        }

        private static void AssertSameRotation(Quaternion expected, Quaternion actual) =>
            Assert.Less(Quaternion.Angle(expected, actual), Tolerance);

        [Test]
        public void AFall_MakesLyingDownTheRestPose()
        {
            Play(new FallCue(), FlatOnTheGroundDegrees);

            AssertSameRotation(Quaternion.Euler(-FlatOnTheGroundDegrees, 0f, 0f), target.RestPose.LocalRotation);
            Assert.AreEqual(-(CapsuleHalfHeight - CapsuleHalfDepth), target.RestPose.LocalPosition.y, Tolerance, "lowered so the capsule lies on the ground");
        }

        [Test]
        public void AFallThatStopsHalfWay_IsLoweredByLess()
        {
            float shareTurned = 1f - Mathf.Cos(HalfWayDownDegrees * Mathf.Deg2Rad);

            Play(new FallCue(), HalfWayDownDegrees);

            Assert.AreEqual(-(CapsuleHalfHeight - CapsuleHalfDepth) * shareTurned, target.RestPose.LocalPosition.y, Tolerance);
        }

        [Test]
        public void AFallOnACrumpledBody_IsLoweredAsFarAsOnAStandingOne()
        {
            PlayToTheEnd(new CrumpleCue(), AnySquash);

            Play(new FallCue(), FlatOnTheGroundDegrees);

            Assert.AreEqual(-(CapsuleHalfHeight - CapsuleHalfDepth), target.RestPose.LocalPosition.y, Tolerance, "measured from the standing body, not from the pose it was hit in");
            Assert.AreEqual(target.StandingPose.LocalScale, target.RestPose.LocalScale, "a body lying down is no longer crumpled");
        }

        [Test]
        public void AFallWithNoRendererToMeasure_IsNotLowered()
        {
            RecordingCueTarget targetWithoutARenderer = new RecordingCueTarget(body.transform, bodyRenderer: null);

            new FallCue().Play(targetWithoutARenderer, new PresentationCue(firesAt: 0f, new FallCue(), CueSeconds, FlatOnTheGroundDegrees));
            DOTween.Kill(targetWithoutARenderer.TweenId);

            Assert.AreEqual(targetWithoutARenderer.StandingPose.LocalPosition, targetWithoutARenderer.RestPose.LocalPosition);
        }

        [Test]
        public void AFallPlayedToItsEnd_LeavesTheBodyLying()
        {
            PlayToTheEnd(new FallCue(), FlatOnTheGroundDegrees);

            AssertSameRotation(target.RestPose.LocalRotation, body.transform.localRotation);
            Assert.AreEqual(target.RestPose.LocalPosition.y, body.transform.localPosition.y, Tolerance);
        }

        [Test]
        public void ARise_MakesStandingTheRestPoseAgain()
        {
            Play(new FallCue(), FlatOnTheGroundDegrees);

            Play(new RiseCue(), AnyStrength);

            AssertSameRotation(target.StandingPose.LocalRotation, target.RestPose.LocalRotation);
            Assert.AreEqual(target.StandingPose.LocalPosition, target.RestPose.LocalPosition);
        }

        [Test]
        public void ACrumple_SquashesTheBody_AndLeavesTheRestPoseAlone()
        {
            PlayToTheEnd(new CrumpleCue(), AnySquash);

            Assert.AreEqual(target.StandingPose.LocalScale.y * (1f - AnySquash), body.transform.localScale.y, Tolerance);
            Assert.AreEqual(target.StandingPose.LocalPosition, target.RestPose.LocalPosition);
            Assert.AreEqual(target.StandingPose.LocalScale, target.RestPose.LocalScale);
        }

        private sealed class RecordingCueTarget : ICueTarget
        {
            public RecordingCueTarget(Transform body, Renderer bodyRenderer)
            {
                Body = body;
                BodyRenderer = bodyRenderer;
                StandingPose = BodyPose.Of(body);
                RestPose = StandingPose;
            }

            public Transform Body { get; }
            public Transform Fist => null;
            public Renderer BodyRenderer { get; }
            public IReadOnlyList<Renderer> TintedRenderers => BodyRenderer != null ? new[] { BodyRenderer } : new Renderer[0];
            public BodyPose RestPose { get; private set; }
            public BodyPose StandingPose { get; }
            public Vector3 FistRestLocalPosition => Vector3.zero;
            public object TweenId => this;

            public void RestAt(BodyPose pose) => RestPose = pose;
        }
    }
}
