using System.Collections.Generic;
using ArkhamCombat.Combat;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
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
