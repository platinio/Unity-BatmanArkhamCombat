using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Enemies;
using UnityEditor;

namespace ArkhamCombat.Editor
{
    public sealed class EnemyArchetypeEditorWindow : DatabaseEditorWindow<EnemyArchetypeDatabase, EnemyArchetype>
    {
        [MenuItem("Window/Arcane Onyx/Enemy Archetypes")]
        public static void Open() => GetWindow<EnemyArchetypeEditorWindow>("Enemy Archetypes");

        public override string GetWindowTitle() => "Enemy Archetypes";
    }

    [CustomEditor(typeof(EnemyArchetypeDatabase))]
    public sealed class EnemyArchetypeDatabaseEditor : ScriptableDatabaseEditor<EnemyArchetypeEditorWindow, EnemyArchetypeDatabase, EnemyArchetype>
    {
    }

    [CustomEditor(typeof(EnemyArchetype))]
    public sealed class EnemyArchetypeEditor : ScriptableItemDefaultEditor<EnemyArchetypeEditorWindow, EnemyArchetypeDatabase, EnemyArchetype>
    {
    }
}
