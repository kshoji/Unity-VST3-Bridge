using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Phase 7 helpers: plugin platform flags + optional IL2CPP Win64 player build.
    /// Batchmode: -executeMethod jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildIl2CppWin64
    /// </summary>
    public static class VstHostBuildVerify
    {
        private const string DllRelativeX64 = "Plugins/Windows/x86_64/VstHostNative.dll";
        private const string DllRelativeArm64 = "Plugins/Windows/ARM64/VstHostNative.dll";

        [MenuItem("Window/VST3 Host/Verify Plugin Platforms")]
        public static void VerifyPluginPlatformsMenu()
        {
            if (VerifyPluginPlatforms(out var message))
                Debug.Log($"[VstHost] {message}");
            else
                Debug.LogError($"[VstHost] {message}");
        }

        [MenuItem("Window/VST3 Host/Build IL2CPP Win64 (Verify)")]
        public static void BuildIl2CppWin64Menu()
        {
            var code = BuildIl2CppWin64Internal();
            if (code != 0)
                throw new BuildFailedException($"IL2CPP Win64 verify build failed with code {code}");
        }

        public static void BuildIl2CppWin64()
        {
            var code = BuildIl2CppWin64Internal();
            EditorApplication.Exit(code);
        }

        private static int BuildIl2CppWin64Internal()
        {
            if (!VerifyPluginPlatforms(out var pluginMsg))
            {
                Debug.LogError($"[VstHost] {pluginMsg}");
                return 2;
            }

            Debug.Log($"[VstHost] {pluginMsg}");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2); // x86_64

            var scene = FindFirstEnabledScene();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogError("[VstHost] No enabled scene in Build Settings.");
                return 3;
            }

            // Keep build settings in sync for subsequent editor opens.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scene, true)
            };

            var outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds", "Win64-IL2CPP-Verify");
            Directory.CreateDirectory(outDir);
            var exe = Path.Combine(outDir, "VstHostVerify.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[VstHost] Build failed: {report.summary.result}");
                return 4;
            }

            var dllHits = Directory.GetFiles(outDir, "VstHostNative.dll", SearchOption.AllDirectories);
            if (dllHits.Length == 0)
            {
                Debug.LogError("[VstHost] Built player is missing VstHostNative.dll");
                return 5;
            }

            Debug.Log($"[VstHost] IL2CPP Win64 verify OK. DLL: {dllHits[0]}");
            return 0;
        }

        public static bool VerifyPluginPlatforms(out string message)
        {
            if (!TryResolveDllAssetPath(DllRelativeX64, out var x64Path))
            {
                message = $"Could not locate {DllRelativeX64} in the package.";
                return false;
            }

            if (!VerifyX64Importer(x64Path, out message))
                return false;

            if (!TryResolveDllAssetPath(DllRelativeArm64, out var arm64Path))
            {
                message = $"Could not locate {DllRelativeArm64} in the package.";
                return false;
            }

            if (!VerifyArm64Importer(arm64Path, out message))
                return false;

            message = $"{x64Path}: Editor + Win64 OK; {arm64Path}: Windows ARM64 OK.";
            return true;
        }

        private static bool TryResolveDllAssetPath(string dllRelative, out string dllAssetPath)
        {
            dllAssetPath = null;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(VstHostManager).Assembly);

            if (packageInfo != null)
            {
                var candidate = Path.Combine(packageInfo.assetPath, dllRelative).Replace('\\', '/');
                if (File.Exists(Path.GetFullPath(candidate)))
                {
                    dllAssetPath = candidate;
                    return true;
                }
            }

            var needle = dllRelative.Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("VstHostNative t:DefaultAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').EndsWith(needle, StringComparison.OrdinalIgnoreCase)
                    || (path.EndsWith("VstHostNative.dll", StringComparison.OrdinalIgnoreCase)
                        && path.Replace('\\', '/').IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    dllAssetPath = path;
                    return true;
                }
            }

            return false;
        }

        private static bool VerifyX64Importer(string dllAssetPath, out string message)
        {
            var importer = AssetImporter.GetAtPath(dllAssetPath) as PluginImporter;
            if (importer == null)
            {
                message = $"PluginImporter missing for {dllAssetPath}";
                return false;
            }

            var editorOk = importer.GetCompatibleWithEditor();
            var win64Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64);
            var win32Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows);
            var anyOk = importer.GetCompatibleWithAnyPlatform();

            if (anyOk)
            {
                message = $"{dllAssetPath}: must not be Compatible With Any Platform (keep Win64/Editor only).";
                return false;
            }

            if (!editorOk || !win64Ok)
            {
                message = $"{dllAssetPath}: enable Editor + StandaloneWindows64 (editorOk={editorOk}, win64Ok={win64Ok}).";
                return false;
            }

            if (win32Ok)
            {
                message = $"{dllAssetPath}: StandaloneWindows (x86) should stay disabled.";
                return false;
            }

            message = $"{dllAssetPath}: platforms OK (Editor + Win64 only).";
            return true;
        }

        private static bool VerifyArm64Importer(string dllAssetPath, out string message)
        {
            var importer = AssetImporter.GetAtPath(dllAssetPath) as PluginImporter;
            if (importer == null)
            {
                message = $"PluginImporter missing for {dllAssetPath}";
                return false;
            }

            var anyOk = importer.GetCompatibleWithAnyPlatform();
            var editorOk = importer.GetCompatibleWithEditor();
            var win64Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64);
            var win32Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows);

            if (anyOk)
            {
                message = $"{dllAssetPath}: must not be Compatible With Any Platform (Windows ARM64 only).";
                return false;
            }

            if (editorOk || win64Ok || win32Ok)
            {
                message = $"{dllAssetPath}: disable Editor/Win64/Win32 (Standalone Windows ARM64 only; editorOk={editorOk}, win64Ok={win64Ok}).";
                return false;
            }

            // Platform enablement for ARM64 is primarily driven by the .meta
            // (Standalone: Windows ARM64). Presence of the asset is enough here.
            message = $"{dllAssetPath}: platforms OK (Windows ARM64 only).";
            return true;
        }

        private static string FindFirstEnabledScene()
        {
            var scenes = EditorBuildSettings.scenes;
            var enabled = scenes?.FirstOrDefault(s =>
                s.enabled && !string.IsNullOrEmpty(s.path) && s.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
            if (enabled != null)
                return enabled.path;

            var guids = AssetDatabase.FindAssets("t:Scene");
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
        }
    }
}
