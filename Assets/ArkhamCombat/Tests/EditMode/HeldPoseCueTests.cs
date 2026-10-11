using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // The tweens are not ticked here. What these cues promise is where the body rests afterwards.
    public class HeldPoseCueTests
    {
        private const float FallDegrees = 90f;
        private const float CueSeconds = 0.3f;
        private const float CapsuleHalfHeight = 1f;
        private const float CapsuleHalfWidth = 0.5f;
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

        private static PresentationCue Cue(ICueKind kind, float strength) => new PresentationCue(0f, kind, CueSeconds, strength);

        private static void AssertSameRotation(Quaternion expected, Quaternion actual) =>
            Assert.Less(Quaternion.Angle(expected, actual), Tolerance);

        [Test]
        public void AFall_MakesLyingDownTheRestPose()
        {
            new FallCue().Play(target, Cue(new FallCue(), FallDegrees));

            AssertSameRotation(Quaternion.Euler(-FallDegrees, 0f, 0f), target.RestPose.LocalRotation);
            Assert.AreEqual(-(CapsuleHalfHeight - CapsuleHalfWidth), target.RestPose.LocalPosition.y, Tolerance, "lowered so the capsule lies on the ground");
        }

        [Test]
        public void ARise_MakesStandingTheRestPoseAgain()
        {
            new FallCue().Play(target, Cue(new FallCue(), FallDegrees));

            new RiseCue().Play(target, Cue(new RiseCue(), 1f));

            AssertSameRotation(target.StandingPose.LocalRotation, target.RestPose.LocalRotation);
            Assert.AreEqual(target.StandingPose.LocalPosition, target.RestPose.LocalPosition);
        }

        [Test]
        public void ACrumple_LeavesTheRestPoseAlone()
        {
            new CrumpleCue().Play(target, Cue(new CrumpleCue(), 0.3f));

            AssertSameRotation(target.StandingPose.LocalRotation, target.RestPose.LocalRotation);
            Assert.AreEqual(target.StandingPose.LocalPosition, target.RestPose.LocalPosition);
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
            public BodyPose RestPose { get; private set; }
            public BodyPose StandingPose { get; }
            public Vector3 FistRestLocalPosition => Vector3.zero;
            public object TweenId => this;

            public void RestAt(BodyPose pose) => RestPose = pose;
        }
    }
}
