using System;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// When the package is linked via <c>file:</c> / Git from the repo root,
    /// <c>native/**/build/*.dll</c> can appear as extra plugins. Keep only
    /// <c>Plugins/Windows/x86_64/VstHostNative.dll</c> enabled.
    /// </summary>
    [InitializeOnLoad]
    static class VstHostPluginImportGuard
    {
        static VstHostPluginImportGuard()
        {
            EditorApplication.delayCall += Sanitize;
        }

        [MenuItem("Window/VST3 Host/Sanitize Extra Native Plugins")]
        static void SanitizeFromMenu()
        {
            Sanitize();
            Debug.Log("[VstHost] Extra native plugin sanitize done.");
        }

        static void Sanitize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var guids = AssetDatabase.FindAssets("VstHostNative");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("VstHostNative.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                var normalized = path.Replace('\\', '/');
                var isCanonical = normalized.IndexOf("Plugins/Windows/x86_64/VstHostNative.dll",
                    StringComparison.OrdinalIgnoreCase) >= 0;
                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null)
                    continue;

                if (isCanonical)
                    continue;

                if (importer.GetCompatibleWithAnyPlatform()
                    || importer.GetCompatibleWithEditor()
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows))
                {
                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithEditor(false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                    importer.SaveAndReimport();
                    Debug.LogWarning($"[VstHost] Disabled non-package plugin import: {path}");
                }
            }
        }
    }
}
