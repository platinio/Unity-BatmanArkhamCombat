using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // A character variant swaps the picture and nothing else; nothing compiles the prefab, its clips and its controller together, so these read them.
    [TestFixture("Player_Adam", "Assets/ArkhamCombat/Actions")]
    [TestFixture("Thug_Guard", "Assets/ArkhamCombat/Actions/Enemies")]
    [TestFixture("Knife_Guard", "Assets/ArkhamCombat/Actions/Enemies")]
    [TestFixture("Armored_Guard", "Assets/ArkhamCombat/Actions/Enemies")]
    public class CharacterPrefabContractTests
    {
        private static readonly string[] ActionsThatLeaveTheBodyDown = { "Knockdown", "Death" };

        private readonly string prefabPath;
        private readonly string ownActionsFolder;

        private GameObject character;
        private AnimationPresentationDriver driver;
        private CharacterClips clips;

        public CharacterPrefabContractTests(string prefabName, string ownActionsFolder)
        {
            prefabPath = $"Assets/ArkhamCombat/Prefabs/Characters/{prefabName}.prefab";
            this.ownActionsFolder = ownActionsFolder;
        }

        [SetUp]
        public void SetUp()
        {
            character = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(character, $"no prefab at {prefabPath}");
            driver = character.GetComponentInChildren<AnimationPresentationDriver>();
            Assert.IsNotNull(driver, "the model child carries the animation driver");
            clips = (CharacterClips)new SerializedObject(driver).FindProperty("clips").objectReferenceValue;
            Assert.IsNotNull(clips, "the driver names the character's clips");
        }

        [Test]
        public void TheAnimationDriver_IsTheOneTheInstallerFinds_BecauseTheCapsuleChildIsDeactivated()
        {
            Assert.AreSame(driver, character.GetComponentInChildren<IPresentationDriver>());
        }

        [Test]
        public void TheDriverSitsOnTheModel_BesideItsAnimator()
        {
            Assert.IsTrue(driver.TryGetComponent(out Animator animator));
            Assert.IsTrue(animator.isHuman, "Frank's clips are humanoid, so the model must be too");
        }

        [Test]
        public void TheClips_AreWellFormed()
        {
            List<string> errors = new List<string>();

            Assert.IsTrue(clips.Validate(errors), string.Join("\n", errors));
        }

        [Test]
        public void TheLocomotionController_ReadsTheTwoFloatsTheDriverWrites()
        {
            AnimatorController controller = (AnimatorController)clips.Locomotion;
            IEnumerable<string> floats = controller.parameters.Where(parameter => parameter.type == AnimatorControllerParameterType.Float).Select(parameter => parameter.name);

            CollectionAssert.IsSubsetOf(new[] { LocomotionParameterNames.MoveRight, LocomotionParameterNames.MoveForward }, floats);
        }

        [Test]
        public void EveryClipThatLeavesTheBodyDown_HoldsItsLastPose()
        {
            IEnumerable<ClipEntry> entriesThatLeaveTheBodyDown = clips.Entries.Where(entry => ActionsThatLeaveTheBodyDown.Contains(entry.Action.name));

            Assert.IsNotEmpty(entriesThatLeaveTheBodyDown, "a character that can be knocked down or killed has these clips");
            foreach (ClipEntry entry in entriesThatLeaveTheBodyDown)
            {
                Assert.IsTrue(entry.HoldsItsLastPose, $"{entry.Action.name} ends lying down and must stay there");
            }
        }

        [Test]
        public void EveryActionOfItsOwn_HasAClip()
        {
            IEnumerable<ActionDefinition> ownActions = AssetDatabase.FindAssets("t:ActionDefinition", new[] { ownActionsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetDirectoryName(path).Replace(Path.DirectorySeparatorChar, '/') == ownActionsFolder)
                .Select(AssetDatabase.LoadAssetAtPath<ActionDefinition>);

            Assert.IsNotEmpty(ownActions);
            foreach (ActionDefinition action in ownActions)
            {
                Assert.IsTrue(clips.TryFindEntry(action, out _), $"{action.name} would play as a hold");
            }
        }
    }
}
