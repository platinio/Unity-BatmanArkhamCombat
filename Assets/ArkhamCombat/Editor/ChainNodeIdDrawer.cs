using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
    /// <summary>
    /// Draws a chain node id as a dropdown of the owning stance's node ids, so an edge destination
    /// is picked rather than typed. Falls back to a text field when the field is not on a stance,
    /// and shows an id that names no node so the typo is visible rather than silently kept.
    /// </summary>
    [CustomPropertyDrawer(typeof(ChainNodeIdAttribute))]
    public sealed class ChainNodeIdDrawer : PropertyDrawer
    {
        private const string EmptyIdChoice = "(none)";
        private const string MissingIdPrefix = "(missing) ";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Stance stance = property.serializedObject.targetObject as Stance;
            if (stance == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            DrawNodeIdDropdown(position, property, label, stance);
        }

        private static void DrawNodeIdDropdown(Rect position, SerializedProperty property, GUIContent label, Stance stance)
        {
            string currentId = property.stringValue ?? string.Empty;
            List<string> choices = NodeIdsOf(stance);
            int currentIndex = choices.IndexOf(currentId);
            if (currentIndex < 0)
            {
                choices.Insert(0, UnknownIdChoice(currentId));
                currentIndex = 0;
            }

            EditorGUI.BeginProperty(position, label, property);
            int pickedIndex = EditorGUI.Popup(position, label.text, currentIndex, choices.ToArray());
            if (pickedIndex != currentIndex)
            {
                property.stringValue = choices[pickedIndex];
            }

            EditorGUI.EndProperty();
        }

        private static List<string> NodeIdsOf(Stance stance)
        {
            List<string> ids = new List<string>();
            foreach (ChainNode node in stance.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Id))
                {
                    ids.Add(node.Id);
                }
            }

            return ids;
        }

        private static string UnknownIdChoice(string id)
        {
            return string.IsNullOrEmpty(id) ? EmptyIdChoice : MissingIdPrefix + id;
        }
    }
}
