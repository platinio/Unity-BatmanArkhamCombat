using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    public class CharacterClipsTests
    {
        private const float ClipSeconds = 2f;

        private CharacterClips clips;
        private ActionDefinition jab;
        private ActionDefinition cross;
        private AnimationClip clip;
        private AnimatorController locomotion;
        private List<string> errors;

        [SetUp]
        public void SetUp()
        {
            clips = ScriptableObject.CreateInstance<CharacterClips>();
            clips.name = "Adam";
            jab = Action("Jab", 0.45f);
            cross = Action("Cross", 0.55f);
            clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, ClipSeconds, 1f));
            locomotion = new AnimatorController { hideFlags = HideFlags.HideAndDontSave };
            errors = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clips);
            Object.DestroyImmediate(jab);
            Object.DestroyImmediate(cross);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(locomotion);
        }

        [Test]
        public void AWellFormedSet_HasNoErrors()
        {
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip), new ClipEntry(cross, clip, start: 0.2f, end: 0.8f) });

            Assert.IsTrue(clips.Validate(errors), string.Join("\n", errors));
            Assert.IsEmpty(errors);
        }

        [Test]
        public void TheEntryOfAnAction_IsFoundByTheAction()
        {
            ClipEntry crossEntry = new ClipEntry(cross, clip);
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip), crossEntry });

            Assert.IsTrue(clips.TryFindEntry(cross, out ClipEntry found));
            Assert.AreSame(crossEntry, found);
        }

        [Test]
        public void AnActionWithNoEntry_IsNotFound()
        {
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip) });

            Assert.IsFalse(clips.TryFindEntry(cross, out ClipEntry found));
            Assert.IsNull(found);
        }

        [Test]
        public void AnEntryWithNoClip_IsNotFound_AndIsReported()
        {
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip: null) });

            Assert.IsFalse(clips.TryFindEntry(jab, out _), "an entry with no clip plays as a hold until it is fixed");
            Assert.IsFalse(clips.Validate(errors));
            StringAssert.Contains("entry 0 has no clip", errors[0]);
        }

        [Test]
        public void AnEntryWithNoAction_IsReported()
        {
            clips.Configure(locomotion, new[] { new ClipEntry(action: null, clip) });

            Assert.IsFalse(clips.Validate(errors));
            StringAssert.Contains("entry 0 names no action", errors[0]);
        }

        [TestCase(0.5f, 0.5f)]
        [TestCase(0.6f, 0.4f)]
        public void AnEntryThatEndsBeforeOrAtItsStart_IsReported(float start, float end)
        {
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip, start, end) });

            Assert.IsFalse(clips.Validate(errors));
            StringAssert.Contains("not after its start", errors[0]);
        }

        [Test]
        public void AnActionMappedTwice_IsReported()
        {
            clips.Configure(locomotion, new[] { new ClipEntry(jab, clip), new ClipEntry(jab, clip) });

            Assert.IsFalse(clips.Validate(errors));
            StringAssert.Contains("'Jab' has two entries", errors[0]);
        }

        [Test]
        public void ASetWithNoLocomotion_IsReported()
        {
            clips.Configure(locomotion: null, new[] { new ClipEntry(jab, clip) });

            Assert.IsFalse(clips.Validate(errors));
            StringAssert.Contains("no locomotion controller", errors[0]);
        }

        [Test]
        public void TheWholeClip_SpansTheAction()
        {
            ClipEntry entry = new ClipEntry(jab, clip);

            Assert.AreEqual(0f, entry.ClipSecondsAt(0f), 1e-4f);
            Assert.AreEqual(ClipSeconds * 0.5f, entry.ClipSecondsAt(0.5f), 1e-4f);
            Assert.AreEqual(ClipSeconds, entry.ClipSecondsAt(1f), 1e-4f);
        }

        [Test]
        public void ATrimmedEntry_StretchesItsPartOverTheAction()
        {
            ClipEntry entry = new ClipEntry(jab, clip, start: 0.25f, end: 0.75f);

            Assert.AreEqual(ClipSeconds * 0.25f, entry.ClipSecondsAt(0f), 1e-4f);
            Assert.AreEqual(ClipSeconds * 0.5f, entry.ClipSecondsAt(0.5f), 1e-4f);
            Assert.AreEqual(ClipSeconds * 0.75f, entry.ClipSecondsAt(1f), 1e-4f);
        }
    }
}
