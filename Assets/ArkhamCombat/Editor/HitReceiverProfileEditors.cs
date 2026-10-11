using ArcaneOnyx.ScriptableObjectDatabase;
using ArkhamCombat.Combat;
using UnityEditor;

namespace ArkhamCombat.Editor
{
    public sealed class HitReceiverProfileEditorWindow : DatabaseEditorWindow<HitReceiverProfileDatabase, HitReceiverProfile>
    {
        [MenuItem("Window/Arcane Onyx/Hit Receiver Profiles")]
        public static void Open() => GetWindow<HitReceiverProfileEditorWindow>("Hit Receiver Profiles");

        public override string GetWindowTitle() => "Hit Receiver Profiles";
    }

    [CustomEditor(typeof(HitReceiverProfileDatabase))]
    public sealed class HitReceiverProfileDatabaseEditor : ScriptableDatabaseEditor<HitReceiverProfileEditorWindow, HitReceiverProfileDatabase, HitReceiverProfile>
    {
    }

    [CustomEditor(typeof(HitReceiverProfile))]
    public sealed class HitReceiverProfileEditor : ScriptableItemDefaultEditor<HitReceiverProfileEditorWindow, HitReceiverProfileDatabase, HitReceiverProfile>
    {
    }
}
