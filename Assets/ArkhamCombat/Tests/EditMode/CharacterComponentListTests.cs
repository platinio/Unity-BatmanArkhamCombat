using System.Collections.Generic;
using ArcaneOnyx.TPCharacterController;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ArkhamCombat.Tests.CombatTestDoubles;

namespace ArkhamCombat.Tests
{
    // The controller has no character component of its own, and Unity refuses to add one defined in
    // an editor-only test assembly, so the character brain's list is tested here with the game's
    // components. Skipping and reporting an empty entry is tested in the controller, whose tests can
    // tick the list.
    public class CharacterComponentListTests
    {
        private GameObject character;

        [SetUp]
        public void SetUp()
        {
            character = HiddenObject("Character");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(character);

        private static List<Object> CharacterComponentsOf(CharacterBrain brain)
        {
            SerializedProperty list = new SerializedObject(brain).FindProperty("characterComponents");
            List<Object> characterComponents = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                characterComponents.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            return characterComponents;
        }

        [Test]
        public void AddingTheCharacterBrainListsTheCharacterComponentsAlreadyOnTheCharacter()
        {
            CombatFactsUpdater factsUpdater = character.AddComponent<CombatFactsUpdater>();
            ComboTracker comboTracker = character.AddComponent<ComboTracker>();
            CombatActions combatActions = character.AddComponent<CombatActions>();

            CharacterBrain brain = character.AddComponent<CharacterBrain>();

            CollectionAssert.AreEqual(
                new Object[] { factsUpdater, comboTracker, combatActions },
                CharacterComponentsOf(brain));
        }
    }
}
