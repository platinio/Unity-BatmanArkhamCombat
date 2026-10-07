using System.Collections.Generic;
using System.IO;
using ArcaneOnyx.TPCharacterController.Inputs;
using ArkhamCombat.Combat;
using ArkhamCombat.Player;
using ArkhamCombat.Presentation;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
    /// <summary>
    /// Builds the Ground stance fixture from spec 01 as assets: four attacks with windows and cues,
    /// the stance, and a combat config pointing at it. Rebuilding updates the existing assets in
    /// place so references and GUIDs survive. The GlideKick and Takedown edges wait for the
    /// Functions of T9 and are not authored here.
    /// </summary>
    public static class GroundStanceFixtureBuilder
    {
        private const string ActionsFolder = "Assets/ArkhamCombat/Actions";
        private const string StancesFolder = "Assets/ArkhamCombat/Stances";
        private const string SettingsFolder = "Assets/ArkhamCombat/Settings";

        [MenuItem("ArkhamCombat/Build Ground Stance Fixture")]
        public static void Build()
        {
            EnsureFolder(ActionsFolder);
            EnsureFolder(StancesFolder);
            EnsureFolder(SettingsFolder);

            AttackDefinition jabL = Attack("Jab_L", 0.45f, new Window(0.25f, 0.45f), new Window(0.45f, 0.95f), new Window(0f, 0.25f), new Window(0f, 0.25f), 1.1f, 4f,
                new PresentationCue(0f, new LeanCue(), 0.2f, 8f),
                new PresentationCue(0.2f, new PunchCue(), 0.25f, 0.35f));

            AttackDefinition jabR = Attack("Jab_R", 0.45f, new Window(0.25f, 0.45f), new Window(0.45f, 0.95f), new Window(0f, 0.25f), new Window(0f, 0.25f), 1.1f, 4f,
                new PresentationCue(0f, new LeanCue(), 0.2f, 8f),
                new PresentationCue(0.2f, new PunchCue(), 0.25f, 0.35f));

            AttackDefinition cross = Attack("Cross", 0.55f, new Window(0.3f, 0.5f), new Window(0.5f, 0.95f), new Window(0f, 0.3f), new Window(0f, 0.3f), 1.1f, 4f,
                new PresentationCue(0.05f, new LeanCue(), 0.3f, 12f),
                new PresentationCue(0.25f, new PunchCue(), 0.3f, 0.5f),
                new PresentationCue(0.3f, new SquashCue(), 0.2f, 0.15f));

            AttackDefinition roundhouse = Attack("RoundhouseKick", 0.8f, new Window(0.4f, 0.6f), new Window(0.6f, 0.95f), new Window(0f, 0.35f), new Window(0f, 0.35f), 1.4f, 4.5f,
                new PresentationCue(0f, new LeanCue(), 0.25f, -10f),
                new PresentationCue(0.2f, new SpinCue(), 0.5f, 1f),
                new PresentationCue(0.55f, new SquashCue(), 0.2f, 0.2f));

            Stance stance = LoadOrCreate<Stance>($"{StancesFolder}/Ground.asset");
            stance.Configure(
                "Neutral",
                new[]
                {
                    new ChainNode("Neutral", (AttackDefinition)null, new Edge(IntentKind.Strike, "S1", 2)),
                    new ChainNode("S1", new VariantPool(new TargetSidePolicy(), jabL, jabR), new Edge(IntentKind.Strike, "S2")),
                    new ChainNode("S2", cross, new Edge(IntentKind.Strike, "S3")),
                    new ChainNode("S3", roundhouse, new Edge(IntentKind.Strike, "S1"))
                },
                chainResetSeconds: 0.6f);
            EditorUtility.SetDirty(stance);

            List<string> errors = new List<string>();
            if (!stance.Validate(errors))
            {
                Debug.LogError($"[Fixture] Ground stance has problems:\n{string.Join("\n", errors)}", stance);
            }

            CombatConfig config = LoadOrCreate<CombatConfig>($"{SettingsFolder}/CombatConfig.asset");
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("stance").objectReferenceValue = stance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fixture] Ground stance fixture built: {ActionsFolder}, {StancesFolder}/Ground.asset, {SettingsFolder}/CombatConfig.asset");
        }

        private static AttackDefinition Attack(
            string name,
            float duration,
            Window active,
            Window cancelAttack,
            Window cancelEvade,
            Window warp,
            float strikeDistance,
            float maxLunge,
            params PresentationCue[] cues)
        {
            AttackDefinition attack = LoadOrCreate<AttackDefinition>($"{ActionsFolder}/{name}.asset");
            attack.Configure(duration, cues);
            attack.ConfigureAttack(active, cancelAttack, cancelEvade, warp, strikeDistance, maxLunge);
            EditorUtility.SetDirty(attack);
            return attack;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
