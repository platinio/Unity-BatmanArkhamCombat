using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
    /// <summary>
    /// Knockdown's duration is the time spent on the floor: the body stays down until GetUp stands
    /// it up.
    /// </summary>
    public static class ReactionActionsFixtureBuilder
    {
        private const string ReactionsFolder = "Assets/ArkhamCombat/Actions/Reactions";
        private const float FallDegrees = 90f;
        private const string LogPrefix = "[Fixture]";

        [MenuItem("ArkhamCombat/Build Reaction Actions Fixture")]
        public static void Build()
        {
            FixtureAssets.EnsureFolder(ReactionsFolder);

            Reaction("Flinch", duration: 0.35f,
                new PresentationCue(firesAt: 0f, new LeanCue(), duration: 0.25f, strength: -10f),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.15f, strength: 1f));

            Reaction("Stagger", duration: 0.8f,
                new PresentationCue(firesAt: 0f, new CrumpleCue(), duration: 0.25f, strength: 0.3f),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.2f, strength: 1f));

            Reaction("Knockdown", duration: 1.6f,
                new PresentationCue(firesAt: 0f, new FallCue(), duration: 0.3f, strength: FallDegrees),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.2f, strength: 1f));

            Reaction("GetUp", duration: 0.6f,
                new PresentationCue(firesAt: 0f, new RiseCue(), duration: 0.5f, strength: 1f));

            Reaction("Death", duration: 0.6f,
                new PresentationCue(firesAt: 0f, new FallCue(), duration: 0.45f, strength: FallDegrees),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.3f, strength: 1f));

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} Reaction actions fixture built: {ReactionsFolder}");
        }

        private static void Reaction(string name, float duration, params PresentationCue[] cues)
        {
            ActionDefinition action = FixtureAssets.LoadOrCreate<ActionDefinition>($"{ReactionsFolder}/{name}.asset");
            action.Configure(duration, cues);
            EditorUtility.SetDirty(action);
        }
    }
}
