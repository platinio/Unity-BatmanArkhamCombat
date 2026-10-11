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

            AssertSameRotation(Quaternion.Euler(-FallDegrees, 0f, 0f), target.RestLocalRotation);
            Assert.AreEqual(-(CapsuleHalfHeight - CapsuleHalfWidth), target.RestLocalPosition.y, Tolerance, "lowered so the capsule lies on the ground");
        }

        [Test]
        public void ARise_MakesStandingTheRestPoseAgain()
        {
            new FallCue().Play(target, Cue(new FallCue(), FallDegrees));

            new RiseCue().Play(target, Cue(new RiseCue(), 1f));

            AssertSameRotation(target.StandingLocalRotation, target.RestLocalRotation);
            Assert.AreEqual(target.StandingLocalPosition, target.RestLocalPosition);
        }

        [Test]
        public void ACrumple_LeavesTheRestPoseAlone()
        {
            new CrumpleCue().Play(target, Cue(new CrumpleCue(), 0.3f));

            AssertSameRotation(target.StandingLocalRotation, target.RestLocalRotation);
            Assert.AreEqual(target.StandingLocalPosition, target.RestLocalPosition);
        }

        private sealed class RecordingCueTarget : ICueTarget
        {
            public RecordingCueTarget(Transform body, Renderer bodyRenderer)
            {
                Body = body;
                BodyRenderer = bodyRenderer;
                StandingLocalPosition = body.localPosition;
                StandingLocalRotation = body.localRotation;
                RestLocalPosition = StandingLocalPosition;
                RestLocalRotation = StandingLocalRotation;
                RestLocalScale = body.localScale;
            }

            public Transform Body { get; }
            public Transform Fist => null;
            public Renderer BodyRenderer { get; }
            public Vector3 RestLocalPosition { get; private set; }
            public Quaternion RestLocalRotation { get; private set; }
            public Vector3 RestLocalScale { get; }
            public Vector3 FistRestLocalPosition => Vector3.zero;
            public Vector3 StandingLocalPosition { get; }
            public Quaternion StandingLocalRotation { get; }
            public object TweenId => this;

            public void RestAt(Vector3 localPosition, Quaternion localRotation)
            {
                RestLocalPosition = localPosition;
                RestLocalRotation = localRotation;
            }
        }
    }
}
