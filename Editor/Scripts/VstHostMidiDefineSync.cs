using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Syncs <c>FEATURE_MIDI_PLUGIN</c> when the Unity MIDI Plugin asmdef
    /// (<c>jp.kshoji.midi</c>) is present. MIDI is typically under Assets (not UPM),
    /// so asmdef <c>versionDefines</c> cannot gate the optional adapter assembly.
    /// </summary>
    [InitializeOnLoad]
    public static class VstHostMidiDefineSync
    {
        public const string DefineSymbol = "FEATURE_MIDI_PLUGIN";

        static VstHostMidiDefineSync()
        {
            EditorApplication.delayCall += SyncDefines;
        }

        [MenuItem("Window/VST3 Host/Sync MIDI Plugin Define")]
        static void SyncDefinesFromMenu()
        {
            SyncDefines();
            Debug.Log(HasMidiAsmdef()
                ? $"[VstHost] {DefineSymbol} synced (jp.kshoji.midi present)."
                : $"[VstHost] {DefineSymbol} cleared (jp.kshoji.midi.asmdef not found).");
        }

        /// <summary>Public entry for MCP / tooling. No-ops while entering Play Mode.</summary>
        public static void SyncDefines()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var shouldHave = HasMidiAsmdef();
            var targets = Enum.GetValues(typeof(BuildTargetGroup))
                .Cast<BuildTargetGroup>()
                .Where(g => g != BuildTargetGroup.Unknown)
                .Distinct()
                .ToArray();

            foreach (var group in targets)
            {
                try
                {
                    ApplyForGroup(group, shouldHave);
                }
                catch (ArgumentException)
                {
                    // Obsolete / unsupported groups throw; ignore.
                }
            }
        }

        /// <summary>True when <c>jp.kshoji.midi.asmdef</c> is present in the project.</summary>
        public static bool HasMidiAsmdef()
        {
            var guids = AssetDatabase.FindAssets("jp.kshoji.midi t:asmdef");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("jp.kshoji.midi.asmdef", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static void ApplyForGroup(BuildTargetGroup group, bool shouldHave)
        {
#if UNITY_2021_2_OR_NEWER
            NamedBuildTarget named;
            try
            {
                named = NamedBuildTarget.FromBuildTargetGroup(group);
            }
            catch (ArgumentException)
            {
                return;
            }

            var defines = PlayerSettings.GetScriptingDefineSymbols(named);
#else
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
            var list = new List<string>(
                defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            var has = list.Contains(DefineSymbol);
            if (shouldHave == has)
                return;

            if (shouldHave)
                list.Add(DefineSymbol);
            else
                list.Remove(DefineSymbol);

            var joined = string.Join(";", list);
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(named, joined);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
#endif
        }
    }
}
