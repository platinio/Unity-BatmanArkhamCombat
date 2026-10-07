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
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Stance stance = property.serializedObject.targetObject as Stance;
            if (stance == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            List<string> ids = new List<string>();
            foreach (ChainNode node in stance.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Id))
                {
                    ids.Add(node.Id);
                }
            }

            string current = property.stringValue ?? string.Empty;
            int index = ids.IndexOf(current);
            if (index < 0)
            {
                ids.Insert(0, string.IsNullOrEmpty(current) ? "(none)" : $"(missing) {current}");
                index = 0;
            }

            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label.text, index, ids.ToArray());
            if (picked != index)
            {
                property.stringValue = ids[picked];
            }

            EditorGUI.EndProperty();
        }
    }
}
