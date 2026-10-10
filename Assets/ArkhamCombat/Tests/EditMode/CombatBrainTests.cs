using System.Collections.Generic;
using ArkhamCombat.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Tests
{
    // Unity refuses to add a component defined in an editor-only assembly such as this one, so these
    // tests use the game's own CombatFactsUpdater rather than a recording stand-in.
    public class CombatBrainTests
    {
        private const float AnyDeltaTime = 0.016f;

        private GameObject character;

        [SetUp]
        public void SetUp()
        {
            // Hidden and never saved, so the open scene is left untouched.
            character = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(character);

        private static void SetCombatComponents(CombatBrain brain, params CombatComponent[] combatComponents)
        {
            SerializedObject serializedBrain = new SerializedObject(brain);
            SerializedProperty list = serializedBrain.FindProperty("combatComponents");
            list.arraySize = combatComponents.Length;
            for (int i = 0; i < combatComponents.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = combatComponents[i];
            }

            serializedBrain.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<Object> CombatComponentsOf(CombatBrain brain)
        {
            SerializedProperty list = new SerializedObject(brain).FindProperty("combatComponents");
            List<Object> combatComponents = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                combatComponents.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            return combatComponents;
        }

        [Test]
        public void AddingTheBrainListsTheCombatComponentsAlreadyOnTheCharacter()
        {
            CombatFactsUpdater factsUpdater = character.AddComponent<CombatFactsUpdater>();

            CombatBrain brain = character.AddComponent<CombatBrain>();

            CollectionAssert.AreEqual(new Object[] { factsUpdater }, CombatComponentsOf(brain));
        }

        [Test]
        public void SkipsAnEmptyEntryInTheList()
        {
            CombatBrain brain = character.AddComponent<CombatBrain>();
            SetCombatComponents(brain, new CombatComponent[] { null });

            Assert.DoesNotThrow(() => brain.TickCombatComponents(AnyDeltaTime));
        }
    }
}
