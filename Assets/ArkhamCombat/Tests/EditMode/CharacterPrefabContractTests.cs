using System.Collections.Generic;
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
    [TestFixture("Player_Adam")]
    public class CharacterPrefabContractTests
    {
        private readonly string prefabPath;

        private GameObject character;
        private AnimationPresentationDriver driver;
        private CharacterClips clips;

        public CharacterPrefabContractTests(string prefabName) => prefabPath = $"Assets/ArkhamCombat/Prefabs/Characters/{prefabName}.prefab";

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
            foreach (ClipEntry entry in clips.Entries)
            {
                bool leavesTheBodyDown = entry.Action.name == "Knockdown" || entry.Action.name == "Death";
                if (leavesTheBodyDown)
                {
                    Assert.IsTrue(entry.HoldsItsLastPose, $"{entry.Action.name} ends lying down and must stay there");
                }
            }
        }

        [Test]
        public void ThePlayersStrikes_EachHaveAClip()
        {
            foreach (string actionName in new[] { "Jab_L", "Jab_R", "Cross", "RoundhouseKick", "Evade", "Counter", "CapeStun" })
            {
                ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>($"Assets/ArkhamCombat/Actions/{actionName}.asset");

                Assert.IsTrue(clips.TryFindEntry(action, out _), $"{actionName} would play as a hold");
            }
        }
    }
}
