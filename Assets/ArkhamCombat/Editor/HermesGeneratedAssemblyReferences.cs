using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArkhamCombat.Editor
{
    /// <summary>
    /// The strike and combo events carry <c>AttackDefinition</c> and <c>ComboResetReason</c>, so the two
    /// assemblies Hermes generates must reference the combat core. Hermes rewrites their assembly
    /// definitions from its own templates on every Regenerate Events, and those templates cannot know
    /// about this game, so this puts the reference back each time they are imported.
    /// </summary>
    internal sealed class HermesGeneratedAssemblyReferences : AssetPostprocessor
    {
        private const string CombatCoreAssembly = "ArkhamCombat.Combat";
        private const string StartOfReferences = "\"references\": [";

        private static readonly string[] GeneratedAssemblyDefinitions = { "Hermes.EventArgs.asmdef", "Hermes.Events.asmdef" };

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (IsGeneratedByHermes(assetPath))
                {
                    MakeSureItReferencesTheCombatCore(assetPath);
                }
            }
        }

        private static bool IsGeneratedByHermes(string assetPath) =>
            Array.IndexOf(GeneratedAssemblyDefinitions, Path.GetFileName(assetPath)) >= 0;

        private static void MakeSureItReferencesTheCombatCore(string assemblyDefinitionPath)
        {
            string definition = File.ReadAllText(assemblyDefinitionPath);
            bool isAlreadyReferenced = definition.Contains($"\"{CombatCoreAssembly}\"");
            if (isAlreadyReferenced)
            {
                return;
            }

            int startOfReferences = definition.IndexOf(StartOfReferences, StringComparison.Ordinal);
            if (startOfReferences < 0)
            {
                Debug.LogError(
                    $"[{nameof(HermesGeneratedAssemblyReferences)}] '{assemblyDefinitionPath}' has no references list, so " +
                    $"{CombatCoreAssembly} could not be added and the generated events will not compile.");
                return;
            }

            int firstReference = startOfReferences + StartOfReferences.Length;
            string withTheCombatCore = definition.Insert(firstReference, $"\r\n        \"{CombatCoreAssembly}\",");

            File.WriteAllText(assemblyDefinitionPath, withTheCombatCore, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            AssetDatabase.ImportAsset(assemblyDefinitionPath);
        }
    }
}
