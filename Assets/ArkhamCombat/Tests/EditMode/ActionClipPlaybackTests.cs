using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The graph is evaluated by hand, so the pose the Animator writes is what the tests read.
    public class ActionClipPlaybackTests
    {
        private const float ClipSeconds = 2f;
        private const float ActionSeconds = 0.5f;
        private const float ACut = 0f;
        private const float IdleBoneX = 0.25f;

        private GameObject model;
        private Transform bone;
        private Animator animator;
        private AnimationClip clip;
        private AnimationClip idle;
        private AnimatorController locomotion;
        private CharacterClips clips;
        private ActionDefinition jab;
        private ActionDefinition cross;
        private ActionClipPlayback playback;

        [SetUp]
        public void SetUp()
        {
            model = HiddenObject("Model");
            animator = model.AddComponent<Animator>();
            bone = new GameObject("Bone").transform;
            bone.SetParent(model.transform);
            clip = ClipMovingTheBoneFromZeroToOne();
            idle = ClipHoldingTheBoneAt(IdleBoneX);
            locomotion = ControllerPlaying(idle);
            clips = ScriptableObject.CreateInstance<CharacterClips>();
            jab = Action("Jab", ActionSeconds);
            cross = Action("Cross", ActionSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            playback?.Destroy();
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(locomotion);
            Object.DestroyImmediate(clips);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(cross);
        }

        private static AnimationClip ClipMovingTheBoneFromZeroToOne()
        {
            AnimationClip clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, ClipSeconds, 1f));
            return clip;
        }

        private static AnimationClip ClipHoldingTheBoneAt(float x)
        {
            AnimationClip clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, x));
            return clip;
        }

        private static AnimatorController ControllerPlaying(AnimationClip clip)
        {
            AnimatorController controller = new AnimatorController { hideFlags = HideFlags.HideAndDontSave };
            controller.AddLayer("Locomotion");
            controller.layers[0].stateMachine.AddState("Idle").motion = clip;
            return controller;
        }

        private ActionClipPlayback PlaybackWith(params ClipEntry[] entries)
        {
            clips.Configure(locomotion, entries, holdBlendOutSeconds: 0.2f);
            playback = new ActionClipPlayback(animator, clips);
            return playback;
        }

        private ClipEntry WholeClipFor(ActionDefinition action, float blendInSeconds = ACut, float blendOutSeconds = ACut) =>
            new ClipEntry(action, clip, 0f, 1f, blendInSeconds, blendOutSeconds);

        private void EvaluateTheGraphByHand() => playback.Graph.Evaluate();

        [Test]
        public void TheClipFollowsTheClock_StretchedToTheAction()
        {
            PlaybackWith(WholeClipFor(jab)).Show(jab);

            playback.FollowTheClock(0.5f);
            EvaluateTheGraphByHand();

            Assert.AreEqual(ClipSeconds * 0.5f, playback.ClipSeconds, 1e-4f);
            Assert.AreEqual(0.5f, bone.localPosition.x, 1e-3f, "half the action is half the clip, whatever their lengths");
        }

        [Test]
        public void ATrimmedEntry_PlaysOnlyItsPart()
        {
            PlaybackWith(new ClipEntry(jab, clip, start: 0.5f, end: 1f, ACut, ACut)).Show(jab);

            Assert.AreEqual(ClipSeconds * 0.5f, playback.ClipSeconds, 1e-4f);
            playback.FollowTheClock(1f);
            Assert.AreEqual(ClipSeconds, playback.ClipSeconds, 1e-4f);
        }

        [Test]
        public void BetweenActions_LocomotionShows()
        {
            PlaybackWith(WholeClipFor(jab));

            EvaluateTheGraphByHand();

            Assert.AreEqual(IdleBoneX, bone.localPosition.x, 1e-3f);
            Assert.AreEqual(0f, playback.Weight);
        }

        [Test]
        public void ShowingAClip_BlendsInOverItsSeconds()
        {
            PlaybackWith(WholeClipFor(jab, blendInSeconds: 0.1f)).Show(jab);
            Assert.AreEqual(0f, playback.Weight);

            playback.Blend(0.05f);
            Assert.AreEqual(0.5f, playback.Weight, 1e-4f);
            playback.Blend(0.05f);
            Assert.AreEqual(1f, playback.Weight, 1e-4f);
        }

        [Test]
        public void FadingOut_ReturnsToLocomotion_AndDropsTheClipOnceFaded()
        {
            PlaybackWith(WholeClipFor(jab, blendOutSeconds: 0.1f)).Show(jab);
            playback.FollowTheClock(1f);

            playback.FadeOut();

            Assert.IsFalse(playback.IsShowingAClip);
            Assert.AreEqual(1f, playback.Weight, "the fade starts from where the clip was");
            playback.Blend(0.05f);
            Assert.AreEqual(0.5f, playback.Weight, 1e-4f);
            Assert.AreEqual(ClipSeconds, playback.ClipSeconds, 1e-4f, "the clip fades out at the pose it stopped in");
            playback.Blend(0.05f);
            Assert.AreEqual(0f, playback.Weight, 1e-4f);
            Assert.AreEqual(0f, playback.ClipSeconds, "the faded clip is dropped");
            EvaluateTheGraphByHand();
            Assert.AreEqual(IdleBoneX, bone.localPosition.x, 1e-3f);
        }

        [Test]
        public void AnActionWithNoEntry_IsAHold()
        {
            PlaybackWith().Show(jab);

            EvaluateTheGraphByHand();

            Assert.IsFalse(playback.IsShowingAClip);
            Assert.AreEqual(0f, playback.Weight);
            Assert.AreEqual(IdleBoneX, bone.localPosition.x, 1e-3f);
        }

        [Test]
        public void AHoldAfterAClip_FadesTheClipOutOverTheHoldBlend()
        {
            PlaybackWith(WholeClipFor(jab)).Show(jab);

            playback.Show(cross);

            Assert.IsFalse(playback.IsShowingAClip);
            Assert.AreEqual(1f, playback.Weight);
            playback.Blend(0.1f);
            Assert.AreEqual(0.5f, playback.Weight, 1e-4f);
        }

        [Test]
        public void AFollowUp_TakesTheWeightTheOldClipHad()
        {
            PlaybackWith(WholeClipFor(jab, blendInSeconds: 0.1f), WholeClipFor(cross, blendInSeconds: 0.1f)).Show(jab);
            playback.Blend(0.1f);

            Assert.DoesNotThrow(() => playback.Show(cross));

            Assert.AreSame(cross, playback.Entry.Action);
            Assert.AreEqual(1f, playback.Weight, "a follow-up at full weight is a cut, not a dip through locomotion");
        }

        [Test]
        public void RootMotionIsOff_SoGameplayMovesTheCharacter()
        {
            animator.applyRootMotion = true;

            PlaybackWith();

            Assert.IsFalse(animator.applyRootMotion);
        }
    }
}
