using System;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// When the package is linked via <c>file:</c> / Git from the repo root,
    /// <c>native/**/build*</c> outputs can appear as extra plugins. Keep only
    /// package <c>Plugins/Windows/.../VstHostNative.dll</c> and
    /// <c>Plugins/macOS/VstHostNative.bundle</c> enabled.
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

        static bool IsCanonicalPackagePlugin(string normalizedPath)
        {
            return normalizedPath.IndexOf("Plugins/Windows/x86_64/VstHostNative.dll",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedPath.IndexOf("Plugins/Windows/ARM64/VstHostNative.dll",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedPath.IndexOf("Plugins/macOS/VstHostNative.bundle",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsVstHostNativePluginAsset(string path)
        {
            return path.EndsWith("VstHostNative.dll", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith("VstHostNative.bundle", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith("VstHostNative.dylib", StringComparison.OrdinalIgnoreCase);
        }

        static void Sanitize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var guids = AssetDatabase.FindAssets("VstHostNative");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsVstHostNativePluginAsset(path))
                    continue;

                var normalized = path.Replace('\\', '/');
                if (IsCanonicalPackagePlugin(normalized))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null)
                    continue;

                if (importer.GetCompatibleWithAnyPlatform()
                    || importer.GetCompatibleWithEditor()
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows)
                    || importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX)
                    || IsWindowsArm64Compatible(importer))
                {
                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithEditor(false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
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
