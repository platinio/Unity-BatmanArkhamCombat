using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

namespace ArcaneOnyx.ModuleDefines
{
    // Presence of SerializeReferenceExtensions declares
    // MODULE_SERIALIZE_REFERENCE_EXTENSIONS_EXIST so other modules can gate optional
    // integration code behind `#if MODULE_SERIALIZE_REFERENCE_EXTENSIONS_EXIST`. Applied
    // to every build target the editor knows about, so switching platform never drops it.
    [InitializeOnLoad]
    internal static class SerializeReferenceExtensionsModuleDefine
    {
        private const string Symbol = "MODULE_SERIALIZE_REFERENCE_EXTENSIONS_EXIST";

        static SerializeReferenceExtensionsModuleDefine()
        {
            foreach (var named in AllNamedBuildTargets())
            {
                try
                {
                    var defines = PlayerSettings.GetScriptingDefineSymbols(named)
                                                .Split(';')
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();
                    if (defines.Contains(Symbol)) continue;
                    defines.Add(Symbol);
                    PlayerSettings.SetScriptingDefineSymbols(named, string.Join(";", defines));
                }
                catch (Exception)
                {
                    // Platform not supported by this editor install; nothing to define for it.
                }
            }
        }

        // NamedBuildTarget exposes its platforms only as static fields, so read them by
        // reflection instead of hardcoding a list that would rot across editor versions.
        private static IEnumerable<NamedBuildTarget> AllNamedBuildTargets()
        {
            var targets = typeof(NamedBuildTarget)
                          .GetFields(BindingFlags.Public | BindingFlags.Static)
                          .Where(f => f.FieldType == typeof(NamedBuildTarget))
                          .Select(f => (NamedBuildTarget)f.GetValue(null))
                          .Where(t => !string.IsNullOrEmpty(t.TargetName))
                          .ToList();

            if (targets.Count > 0) return targets;

            // Reflection came up empty (API shape changed): fall back to the active target
            // so the define is at least correct for what the user is building right now.
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            return new[] { NamedBuildTarget.FromBuildTargetGroup(group) };
        }
    }
}
