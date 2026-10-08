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
        private const string StancePath = StancesFolder + "/Ground.asset";
        private const string CombatConfigPath = SettingsFolder + "/CombatConfig.asset";
        private const string CombatConfigStanceField = "stance";
        private const string LogPrefix = "[Fixture]";

        [MenuItem("ArkhamCombat/Build Ground Stance Fixture")]
        public static void Build()
        {
            EnsureFolder(ActionsFolder);
            EnsureFolder(StancesFolder);
            EnsureFolder(SettingsFolder);

            Stance stance = BuildStance();
            ReportStanceProblems(stance);
            PointCombatConfigAt(stance);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} Ground stance fixture built: {ActionsFolder}, {StancePath}, {CombatConfigPath}");
        }

        private static Stance BuildStance()
        {
            Stance stance = LoadOrCreate<Stance>(StancePath);
            stance.Configure(
                root: "Neutral",
                nodes: new[]
                {
                    new ChainNode("Neutral", attack: null, new Edge(IntentKind.Strike, "S1", priority: 2)),
                    new ChainNode("S1", new VariantPool(new TargetSidePolicy(), Jab("Jab_L"), Jab("Jab_R")), new Edge(IntentKind.Strike, "S2")),
                    new ChainNode("S2", Cross(), new Edge(IntentKind.Strike, "S3")),
                    new ChainNode("S3", RoundhouseKick(), new Edge(IntentKind.Strike, "S1"))
                },
                chainResetSeconds: 0.6f);
            EditorUtility.SetDirty(stance);
            return stance;
        }

        private static AttackDefinition Jab(string name)
        {
            return Attack(
                name,
                duration: 0.45f,
                hitWindow: new Window(0.25f, 0.45f),
                comboWindow: new Window(0.45f, 0.95f),
                evadeWindow: new Window(0f, 0.25f),
                warpWindow: new Window(0f, 0.25f),
                strikeDistance: 1.1f,
                maxLunge: 4f,
                cues: new[]
                {
                    new PresentationCue(firesAt: 0f, new LeanCue(), duration: 0.2f, strength: 8f),
                    new PresentationCue(firesAt: 0.2f, new PunchCue(), duration: 0.25f, strength: 0.35f)
                });
        }

        private static AttackDefinition Cross()
        {
            return Attack(
                "Cross",
                duration: 0.55f,
                hitWindow: new Window(0.3f, 0.5f),
                comboWindow: new Window(0.5f, 0.95f),
                evadeWindow: new Window(0f, 0.3f),
                warpWindow: new Window(0f, 0.3f),
                strikeDistance: 1.1f,
                maxLunge: 4f,
                cues: new[]
                {
                    new PresentationCue(firesAt: 0.05f, new LeanCue(), duration: 0.3f, strength: 12f),
                    new PresentationCue(firesAt: 0.25f, new PunchCue(), duration: 0.3f, strength: 0.5f),
                    new PresentationCue(firesAt: 0.3f, new SquashCue(), duration: 0.2f, strength: 0.15f)
                });
        }

        private static AttackDefinition RoundhouseKick()
        {
            return Attack(
                "RoundhouseKick",
                duration: 0.8f,
                hitWindow: new Window(0.4f, 0.6f),
                comboWindow: new Window(0.6f, 0.95f),
                evadeWindow: new Window(0f, 0.35f),
                warpWindow: new Window(0f, 0.35f),
                strikeDistance: 1.4f,
                maxLunge: 4.5f,
                cues: new[]
                {
                    new PresentationCue(firesAt: 0f, new LeanCue(), duration: 0.15f, strength: -10f),
                    new PresentationCue(firesAt: 0.2f, new SpinCue(), duration: 0.5f, strength: 1f),
                    new PresentationCue(firesAt: 0.55f, new SquashCue(), duration: 0.2f, strength: 0.2f)
                });
        }

        private static AttackDefinition Attack(
            string name,
            float duration,
            Window hitWindow,
            Window comboWindow,
            Window evadeWindow,
            Window warpWindow,
            float strikeDistance,
            float maxLunge,
            PresentationCue[] cues)
        {
            AttackDefinition attack = LoadOrCreate<AttackDefinition>($"{ActionsFolder}/{name}.asset");
            attack.Configure(duration, cues);
            attack.ConfigureAttack(hitWindow, comboWindow, evadeWindow, warpWindow, strikeDistance, maxLunge);
            EditorUtility.SetDirty(attack);
            return attack;
        }

        private static void ReportStanceProblems(Stance stance)
        {
            List<string> errors = new List<string>();
            if (!stance.Validate(errors))
            {
                Debug.LogError($"{LogPrefix} Ground stance has problems:\n{string.Join("\n", errors)}", stance);
            }
        }

        private static void PointCombatConfigAt(Stance stance)
        {
            CombatConfig config = LoadOrCreate<CombatConfig>(CombatConfigPath);
            SerializedObject serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty(CombatConfigStanceField).objectReferenceValue = stance;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
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
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
