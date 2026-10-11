using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Presentation;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
    public static class ReactionActionsFixtureBuilder
    {
        private const string ReactionsFolder = "Assets/ArkhamCombat/Actions/Reactions";
        private const float FlatOnTheGroundDegrees = 90f;

        // The body stays down for as long as the knockdown plays; GetUp is what stands it up.
        private const float KnockdownSecondsOnTheFloor = 1.6f;

        [MenuItem("ArkhamCombat/Build Reaction Actions Fixture")]
        public static void Build()
        {
            FixtureAssets.EnsureFolder(ReactionsFolder);
            List<string> errors = new List<string>();

            Reaction("Flinch", duration: 0.35f, errors,
                new PresentationCue(firesAt: 0f, new LeanCue(), duration: 0.25f, strength: -10f),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.15f, strength: 1f));

            Reaction("Stagger", duration: 0.8f, errors,
                new PresentationCue(firesAt: 0f, new CrumpleCue(), duration: 0.25f, strength: 0.3f),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.2f, strength: 1f));

            Reaction("Knockdown", KnockdownSecondsOnTheFloor, errors,
                new PresentationCue(firesAt: 0f, new FallCue(), duration: 0.3f, strength: FlatOnTheGroundDegrees),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.2f, strength: 1f));

            Reaction("GetUp", duration: 0.6f, errors,
                new PresentationCue(firesAt: 0f, new RiseCue(), duration: 0.5f, strength: 1f));

            Reaction("Death", duration: 0.6f, errors,
                new PresentationCue(firesAt: 0f, new FallCue(), duration: 0.45f, strength: FlatOnTheGroundDegrees),
                new PresentationCue(firesAt: 0f, new FlashCue(), duration: 0.3f, strength: 1f));

            AssetDatabase.SaveAssets();
            ReportProblems(errors);
            Debug.Log($"{FixtureAssets.LogPrefix} Reaction actions fixture built: {ReactionsFolder}");
        }

        private static void Reaction(string name, float duration, List<string> errors, params PresentationCue[] cues)
        {
            ActionDefinition action = FixtureAssets.LoadOrCreate<ActionDefinition>($"{ReactionsFolder}/{name}.asset");
            action.Configure(duration, cues);
            action.Validate(errors);
            EditorUtility.SetDirty(action);
        }

        private static void ReportProblems(List<string> errors)
        {
            if (errors.Count > 0)
            {
                Debug.LogError($"{FixtureAssets.LogPrefix} The reaction actions have problems:\n{string.Join("\n", errors)}");
            }
        }
    }
}
