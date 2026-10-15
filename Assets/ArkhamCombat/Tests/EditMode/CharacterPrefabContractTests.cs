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
    // Nothing compiles the prefab, its clips and its controller together, so these read them.
    [TestFixture("Player_Adam")]
    public class CharacterPrefabContractTests
    {
        private const string ActionsFolder = "Assets/ArkhamCombat/Actions";
        private const string TheOneReactionThatIsAHold = "Stunned";

        private static bool ThePacksAreImported =>
            AssetDatabase.IsValidFolder("Assets/Frank_Assassin") && AssetDatabase.IsValidFolder("Assets/UnityTechnologies/Adam Character Pack");

        private readonly string prefabPath;

        private GameObject character;
        private AnimationPresentationDriver driver;
        private CharacterClips clips;

        public CharacterPrefabContractTests(string prefabName) => prefabPath = $"Assets/ArkhamCombat/Prefabs/Characters/{prefabName}.prefab";

        [SetUp]
        public void SetUp()
        {
            Assume.That(ThePacksAreImported, "needs the Adam Character Pack and the Frank RPG Assassin pack at their default paths");
            character = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(character, $"no prefab at {prefabPath}");
            driver = character.GetComponentInChildren<AnimationPresentationDriver>();
            Assert.IsNotNull(driver, "the model child carries the animation driver");
            clips = driver.Clips;
            Assert.IsNotNull(clips, "the driver names the character's clips");
        }

        private static ActionDefinition ActionAt(string pathUnderActions) => AssetDatabase.LoadAssetAtPath<ActionDefinition>($"{ActionsFolder}/{pathUnderActions}.asset");

        private static IEnumerable<ActionDefinition> ActionsIn(string folder) =>
            AssetDatabase.FindAssets("t:ActionDefinition", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetDirectoryName(path).Replace(Path.DirectorySeparatorChar, '/') == folder)
                .Select(AssetDatabase.LoadAssetAtPath<ActionDefinition>);

        [Test]
        public void TheAnimationDriver_IsTheOneTheInstallerFinds_BecauseTheCapsuleChildIsDeactivated()
        {
            Assert.AreSame(driver, character.GetComponentInChildren<IPresentationDriver>());
        }

        [Test]
        public void TheDriverSitsBesideAHumanoidAnimator()
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
            IEnumerable<string> floatParameterNames = controller.parameters.Where(parameter => parameter.type == AnimatorControllerParameterType.Float).Select(parameter => parameter.name);

            CollectionAssert.IsSubsetOf(new[] { LocomotionParameterNames.MoveRight, LocomotionParameterNames.MoveForward }, floatParameterNames);
        }

        [TestCase("Reactions/Knockdown")]
        [TestCase("Reactions/Death")]
        public void AClipThatLeavesTheBodyDown_KeepsItsLastPose(string pathUnderActions)
        {
            Assert.IsTrue(clips.TryFindEntry(ActionAt(pathUnderActions), out ClipEntry entry), $"{pathUnderActions} has a clip");
            Assert.IsTrue(entry.KeepsItsLastPose, $"{pathUnderActions} ends lying down and must stay there");
        }

        [Test]
        public void EveryPlayerAction_HasAClip()
        {
            IEnumerable<ActionDefinition> playerActions = ActionsIn(ActionsFolder);

            Assert.IsNotEmpty(playerActions);
            foreach (ActionDefinition action in playerActions)
            {
                Assert.IsTrue(clips.TryFindEntry(action, out _), $"{action.name} would play as a hold");
            }
        }

        [Test]
        public void EveryReaction_HasAClip_ExceptTheHold()
        {
            IEnumerable<ActionDefinition> reactions = ActionsIn($"{ActionsFolder}/Reactions").Where(reaction => reaction.name != TheOneReactionThatIsAHold);

            Assert.IsNotEmpty(reactions);
            foreach (ActionDefinition reaction in reactions)
            {
                Assert.IsTrue(clips.TryFindEntry(reaction, out _), $"{reaction.name} would play as a hold");
            }
        }
    }
}
