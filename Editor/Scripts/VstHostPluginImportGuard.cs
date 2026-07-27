using System;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// When the package is linked via <c>file:</c> / Git from the repo root,
    /// <c>native/**/build*/*.dll</c> can appear as extra plugins. Keep only
    /// package <c>Plugins/Windows/{x86_64,ARM64}/VstHostNative.dll</c> enabled.
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

        static bool IsCanonicalPackageDll(string normalizedPath)
        {
            return normalizedPath.IndexOf("Plugins/Windows/x86_64/VstHostNative.dll",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedPath.IndexOf("Plugins/Windows/ARM64/VstHostNative.dll",
                       StringComparison.OrdinalIgnoreCase) >= 0;
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
                if (IsCanonicalPackageDll(normalized))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null)
                    continue;

                if (importer.GetCompatibleWithAnyPlatform()
                    || importer.GetCompatibleWithEditor()
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows)
                    || IsWindowsArm64Compatible(importer))
                {
                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithEditor(false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                    TrySetWindowsArm64Compatible(importer, false);
                    importer.SaveAndReimport();
                    Debug.LogWarning($"[VstHost] Disabled non-package plugin import: {path}");
                }
            }
        }

        static bool IsWindowsArm64Compatible(PluginImporter importer)
        {
            try
            {
                // Unity 2022.3+: StandaloneWindows64 covers x64; ARM64 is a separate platform id string.
                return importer.GetCompatibleWithPlatform("WindowsStandaloneArm64")
                       || importer.GetCompatibleWithPlatform("StandaloneWindowsArm64");
            }
            catch
            {
                return false;
            }
        }

        static void TrySetWindowsArm64Compatible(PluginImporter importer, bool enabled)
        {
            try
            {
                importer.SetCompatibleWithPlatform("WindowsStandaloneArm64", enabled);
            }
            catch
            {
                // Older editors without the platform id — ignore.
            }

            try
            {
                importer.SetCompatibleWithPlatform("StandaloneWindowsArm64", enabled);
            }
            catch
            {
                // ignore
            }
        }
    }
}
