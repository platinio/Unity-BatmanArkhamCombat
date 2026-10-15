using System.Text.RegularExpressions;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class AnimationPresentationDriverTests
    {
        private const float ClipSeconds = 2f;
        private const float ActionSeconds = 0.5f;
        private const float BlendSeconds = 0.1f;

        private GameObject model;
        private Transform bone;
        private GameObject skin;
        private AnimationClip clip;
        private AnimatorController locomotion;
        private CharacterClips clips;
        private ActionDefinition jab;
        private TestIntentKinds kinds;
        private AnimationPresentationDriver driver;

        [SetUp]
        public void SetUp()
        {
            model = HiddenObject("Model");
            model.AddComponent<Animator>();
            bone = new GameObject("Bone").transform;
            bone.SetParent(model.transform);
            skin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skin.hideFlags = HideFlags.HideAndDontSave;
            skin.transform.SetParent(model.transform);
            clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, ClipSeconds, 1f));
            locomotion = new AnimatorController { hideFlags = HideFlags.HideAndDontSave };
            locomotion.AddParameter(LocomotionParameterNames.MoveRight, AnimatorControllerParameterType.Float);
            locomotion.AddParameter(LocomotionParameterNames.MoveForward, AnimatorControllerParameterType.Float);
            locomotion.AddLayer("Locomotion");
            clips = ScriptableObject.CreateInstance<CharacterClips>();
            jab = Action("Jab", ActionSeconds);
            kinds = new TestIntentKinds();
            driver = model.AddComponent<AnimationPresentationDriver>();
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.Kill(driver.TweenId);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(locomotion);
            Object.DestroyImmediate(clips);
            Object.DestroyImmediate(jab);
            kinds.Destroy();
        }

        private void WakeWith(params ClipEntry[] entries)
        {
            clips.Configure(locomotion, entries);
            SerializedObject serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("clips").objectReferenceValue = clips;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            driver.Awake();
        }

        private ClipEntry TheJabOnTheWholeClip() => new ClipEntry(jab, clip, 0f, 1f, BlendSeconds, BlendSeconds);

        [Test]
        public void HitStop_HoldsTheClipTimeAndTheBlendAlike()
        {
            WakeWith(TheJabOnTheWholeClip());
            driver.Play(jab);
            driver.Tick(0.05f);
            float clipSecondsAtTheStop = driver.ClipSeconds;
            float weightAtTheStop = driver.ClipWeight;

            driver.Speed = 0f;
            driver.Tick(0.05f);

            Assert.AreEqual(clipSecondsAtTheStop, driver.ClipSeconds, 1e-4f);
            Assert.AreEqual(weightAtTheStop, driver.ClipWeight, 1e-4f);
        }

        [Test]
        public void AfterStop_TheClipFadesOutOverTheFollowingTicks()
        {
            WakeWith(TheJabOnTheWholeClip());
            driver.Play(jab);
            driver.Tick(BlendSeconds);
            Assert.AreEqual(1f, driver.ClipWeight, 1e-4f);

            driver.Stop();
            driver.Tick(BlendSeconds * 0.5f);
            Assert.AreEqual(0.5f, driver.ClipWeight, 1e-4f);
            driver.Tick(BlendSeconds * 0.5f);

            Assert.AreEqual(0f, driver.ClipWeight, 1e-4f);
            Assert.IsFalse(driver.IsPlaying);
        }

        [Test]
        public void ACueThatBendsACapsule_StaysSilentOnTheModel()
        {
            WakeWith(TheJabOnTheWholeClip());
            jab.Configure(ActionSeconds, new[] { new PresentationCue(0f, new LeanCue(), 0.3f, strength: 30f) });

            driver.Play(jab);

            Assert.IsNull(DOTween.TweensById(driver.TweenId), "no tween was started");
            Assert.AreEqual(Quaternion.identity, model.transform.localRotation);
        }

        [Test]
        public void AColourCue_TintsEveryRendererUnderTheModel()
        {
            WakeWith(TheJabOnTheWholeClip());
            jab.Configure(ActionSeconds, new[] { new PresentationCue(0f, new FlashCue(), 0.3f) });

            driver.Play(jab);
            DOTween.TweensById(driver.TweenId)[0].Goto(0.15f);

            Assert.IsTrue(skin.GetComponent<Renderer>().HasPropertyBlock());
        }

        [Test]
        public void TheInjectedSignal_DrivesTheWalk_InTheModelsFrame()
        {
            WakeWith(TheJabOnTheWholeClip());
            driver.Construct(new SidewaysLocomotionSignal());
            model.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            driver.Tick(0.05f);

            Assert.AreEqual(2f, driver.MoveForward, 1e-4f, "facing +X, a velocity along +X is straight ahead");
            Assert.AreEqual(0f, driver.MoveRight, 1e-4f);
        }

        [Test]
        public void AnActionWithNoClip_ReadsAsAHoldOnTheOverlay()
        {
            WakeWith();

            driver.Play(jab);

            StringAssert.StartsWith("hold", driver.FrameDataLine);
        }

        private sealed class SidewaysLocomotionSignal : ILocomotionSignal
        {
            public Vector3 PlanarVelocity => Vector3.right * 2f;
        }

        [Test]
        public void WithoutAClipsAsset_TheClockStillRunsTheFight()
        {
            LogAssert.Expect(LogType.Error, new Regex("No character clips"));
            driver.Awake();

            Assert.DoesNotThrow(() => driver.Play(jab));
            driver.Tick(ActionSeconds * 0.5f);

            Assert.IsTrue(driver.IsPlaying);
            Assert.AreEqual(0.5f, driver.NormalizedTime, 1e-4f);
        }

        [Test]
        public void BehindTheRunner_TheClipFadesOutOnceTheActionEnds()
        {
            WakeWith(TheJabOnTheWholeClip());
            ActionRunner runner = RunnerDriving(driver);

            runner.PlayAction(jab, Vector3.zero);
            runner.Tick(ActionSeconds, Vector3.zero, canStartFromIdle: true);
            Assert.IsFalse(runner.IsPlaying, "the jab ended");
            runner.Tick(BlendSeconds, Vector3.zero, canStartFromIdle: true);

            Assert.AreEqual(0f, driver.ClipWeight, 1e-4f, "idle ticks reach the driver, so it fades back to locomotion");
        }

        private ActionRunner RunnerDriving(IPresentationDriver presentationDriver) =>
            new ActionRunner(stance: null, new IntentBuffer(), new CombatFacts(), new AlwaysConditionEvaluator(), kinds.InterruptKinds, presentationDriver, new RecordingWarpMover(), new RecordingHitWindowListener());
    }
}
