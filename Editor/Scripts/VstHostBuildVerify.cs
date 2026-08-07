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
    /// Plugin platform flags + optional Standalone verify builds (Win64 IL2CPP / OSX / Linux64).
    /// Batchmode Win64: -executeMethod jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildIl2CppWin64
    /// Batchmode OSX: -executeMethod jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildStandaloneOSX
    /// Batchmode Linux: -executeMethod jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildStandaloneLinux64
    /// Batchmode platforms: -executeMethod jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.VerifyPluginPlatformsExit
    /// Phase 5 smoke: native~/windows-vst-host/Run-Phase5Verify.ps1
    /// </summary>
    public static class VstHostBuildVerify
    {
        private const string DllRelativeX64 = "Plugins/Windows/x86_64/VstHostNative.dll";
        private const string DllRelativeArm64 = "Plugins/Windows/ARM64/VstHostNative.dll";
        private const string BundleRelativeMac = "Plugins/macOS/VstHostNative.bundle";
        private const string SoRelativeLinux = "Plugins/Linux/x86_64/VstHostNative.so";

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

        [MenuItem("Window/VST3 Host/Build Standalone OSX (Verify)")]
        public static void BuildStandaloneOSXMenu()
        {
            var code = BuildStandaloneOSXInternal();
            if (code != 0)
                throw new BuildFailedException($"Standalone OSX verify build failed with code {code}");
        }

        [MenuItem("Window/VST3 Host/Build Standalone Linux64 (Verify)")]
        public static void BuildStandaloneLinux64Menu()
        {
            var code = BuildStandaloneLinux64Internal();
            if (code != 0)
                throw new BuildFailedException($"Standalone Linux64 verify build failed with code {code}");
        }

        public static void BuildIl2CppWin64()
        {
            var code = BuildIl2CppWin64Internal();
            EditorApplication.Exit(code);
        }

        public static void BuildStandaloneOSX()
        {
            var code = BuildStandaloneOSXInternal();
            EditorApplication.Exit(code);
        }

        public static void BuildStandaloneLinux64()
        {
            var code = BuildStandaloneLinux64Internal();
            EditorApplication.Exit(code);
        }

        /// <summary>Batchmode: exit 0 when plugin platform flags are OK.</summary>
        public static void VerifyPluginPlatformsExit()
        {
            var ok = VerifyPluginPlatforms(out var message);
            if (ok)
                Debug.Log($"[VstHost] {message}");
            else
                Debug.LogError($"[VstHost] {message}");
            EditorApplication.Exit(ok ? 0 : 2);
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

        private static int BuildStandaloneOSXInternal()
        {
            if (!VerifyPluginPlatforms(out var pluginMsg))
            {
                Debug.LogError($"[VstHost] {pluginMsg}");
                return 2;
            }

            Debug.Log($"[VstHost] {pluginMsg}");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            // Mono is fine for verify; IL2CPP requires matching toolchain on the Mac.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var scene = FindFirstEnabledScene();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogError("[VstHost] No enabled scene in Build Settings.");
                return 3;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scene, true)
            };

            var outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds", "OSX-Verify");
            Directory.CreateDirectory(outDir);
            var app = Path.Combine(outDir, "VstHostVerify.app");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = app,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[VstHost] Build failed: {report.summary.result}");
                return 4;
            }

            var bundleHits = Directory.GetDirectories(outDir, "VstHostNative.bundle", SearchOption.AllDirectories);
            if (bundleHits.Length == 0)
            {
                Debug.LogError("[VstHost] Built player is missing VstHostNative.bundle");
                return 5;
            }

            Debug.Log($"[VstHost] Standalone OSX verify OK. Bundle: {bundleHits[0]}");
            return 0;
        }

        private static int BuildStandaloneLinux64Internal()
        {
            if (!VerifyPluginPlatforms(out var pluginMsg))
            {
                Debug.LogError($"[VstHost] {pluginMsg}");
                return 2;
            }

            Debug.Log($"[VstHost] {pluginMsg}");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

            var scene = FindFirstEnabledScene();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogError("[VstHost] No enabled scene in Build Settings.");
                return 3;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scene, true)
            };

            var outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds", "Linux64-Verify");
            Directory.CreateDirectory(outDir);
            var exe = Path.Combine(outDir, "VstHostVerify.x86_64");

            // Prefer IL2CPP when the Linux sysroot toolchain is present; otherwise Mono
            // (common when verifying from Windows without com.unity.sysroot.* packages).
            var backends = new[]
            {
                ScriptingImplementation.IL2CPP,
                ScriptingImplementation.Mono2x
            };

            BuildReport report = null;
            foreach (var backend in backends)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.None
                };

                report = BuildPipeline.BuildPlayer(options);
                if (report != null && report.summary.result == BuildResult.Succeeded)
                {
                    if (backend != ScriptingImplementation.IL2CPP)
                        Debug.LogWarning($"[VstHost] Linux64 verify used {backend} (IL2CPP unavailable or failed).");
                    break;
                }

                var result = report != null ? report.summary.result.ToString() : "null-report";
                Debug.LogWarning($"[VstHost] Linux64 build with {backend} failed ({result}); trying next backend.");
            }

            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[VstHost] Build failed: {(report != null ? report.summary.result.ToString() : "null report")}");
                return 4;
            }

            var soHits = Directory.GetFiles(outDir, "VstHostNative.so", SearchOption.AllDirectories);
            if (soHits.Length == 0)
            {
                Debug.LogError("[VstHost] Built player is missing VstHostNative.so");
                return 5;
            }

            Debug.Log($"[VstHost] Standalone Linux64 verify OK. SO: {soHits[0]}");
            return 0;
        }

        public static bool VerifyPluginPlatforms(out string message)
        {
            if (!TryResolvePluginAssetPath(DllRelativeX64, out var x64Path))
            {
                message = $"Could not locate {DllRelativeX64} in the package.";
                return false;
            }

            if (!VerifyX64Importer(x64Path, out message))
                return false;

            if (!TryResolvePluginAssetPath(DllRelativeArm64, out var arm64Path))
            {
                message = $"Could not locate {DllRelativeArm64} in the package.";
                return false;
            }

            if (!VerifyArm64Importer(arm64Path, out message))
                return false;

            if (!TryResolvePluginAssetPath(BundleRelativeMac, out var macPath))
            {
                message = $"Could not locate {BundleRelativeMac} in the package.";
                return false;
            }

            if (!VerifyMacImporter(macPath, out message))
                return false;

            if (!TryResolvePluginAssetPath(SoRelativeLinux, out var linuxPath))
            {
                message = $"Could not locate {SoRelativeLinux} in the package.";
                return false;
            }

            if (!VerifyLinuxImporter(linuxPath, out message))
                return false;

            message =
                $"{x64Path}: Editor + Win64 OK; {arm64Path}: Windows ARM64 OK; {macPath}: Editor OSX + OSXUniversal OK; {linuxPath}: Editor Linux + Linux64 OK.";
            return true;
        }

        private static bool TryResolvePluginAssetPath(string relative, out string assetPath)
        {
            assetPath = null;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(VstHostManager).Assembly);

            if (packageInfo != null)
            {
                var candidate = Path.Combine(packageInfo.assetPath, relative).Replace('\\', '/');
                var full = Path.GetFullPath(candidate);
                if (File.Exists(full) || Directory.Exists(full))
                {
                    assetPath = candidate;
                    return true;
                }
            }

            var needle = relative.Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("VstHostNative");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var normalized = path.Replace('\\', '/');
                if (normalized.EndsWith(needle, StringComparison.OrdinalIgnoreCase)
                    || normalized.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    assetPath = path;
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
            var osxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX);
            var linuxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64);
            var arm64Ok = IsWindowsArm64Compatible(importer);
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

            if (win32Ok || osxOk || linuxOk || arm64Ok)
            {
                message = $"{dllAssetPath}: disable Win32 / OSX / Linux / Windows ARM64 (x86_64 Editor+Win64 only).";
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
            var osxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX);
            var linuxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64);
            var arm64Ok = IsWindowsArm64Compatible(importer);

            if (anyOk)
            {
                message = $"{dllAssetPath}: must not be Compatible With Any Platform (Windows ARM64 only).";
                return false;
            }

            if (!arm64Ok)
            {
                message = $"{dllAssetPath}: enable Standalone Windows ARM64.";
                return false;
            }

            if (editorOk || win64Ok || win32Ok || osxOk || linuxOk)
            {
                message =
                    $"{dllAssetPath}: disable Editor/Win64/Win32/OSX/Linux (Standalone Windows ARM64 only; editorOk={editorOk}, win64Ok={win64Ok}).";
                return false;
            }

            message = $"{dllAssetPath}: platforms OK (Windows ARM64 only).";
            return true;
        }

        private static bool VerifyMacImporter(string bundleAssetPath, out string message)
        {
            var importer = AssetImporter.GetAtPath(bundleAssetPath) as PluginImporter;
            if (importer == null)
            {
                message = $"PluginImporter missing for {bundleAssetPath}";
                return false;
            }

            var anyOk = importer.GetCompatibleWithAnyPlatform();
            var editorOk = importer.GetCompatibleWithEditor();
            var osxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX);
            var win64Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64);
            var win32Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows);
            var linuxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64);
            var arm64Ok = IsWindowsArm64Compatible(importer);

            if (anyOk)
            {
                message = $"{bundleAssetPath}: must not be Compatible With Any Platform (OSX only).";
                return false;
            }

            if (!editorOk || !osxOk)
            {
                message = $"{bundleAssetPath}: enable Editor (OS=OSX) + StandaloneOSX (editorOk={editorOk}, osxOk={osxOk}).";
                return false;
            }

            if (win64Ok || win32Ok || linuxOk || arm64Ok)
            {
                message = $"{bundleAssetPath}: Windows/Linux platforms must stay disabled.";
                return false;
            }

            message = $"{bundleAssetPath}: platforms OK (Editor OSX + OSXUniversal).";
            return true;
        }

        private static bool VerifyLinuxImporter(string soAssetPath, out string message)
        {
            var importer = AssetImporter.GetAtPath(soAssetPath) as PluginImporter;
            if (importer == null)
            {
                message = $"PluginImporter missing for {soAssetPath}";
                return false;
            }

            var anyOk = importer.GetCompatibleWithAnyPlatform();
            var editorOk = importer.GetCompatibleWithEditor();
            var linuxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64);
            var win64Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64);
            var win32Ok = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows);
            var osxOk = importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX);
            var arm64Ok = IsWindowsArm64Compatible(importer);

            if (anyOk)
            {
                message = $"{soAssetPath}: must not be Compatible With Any Platform (Linux only).";
                return false;
            }

            if (!editorOk || !linuxOk)
            {
                message = $"{soAssetPath}: enable Editor (OS=Linux) + StandaloneLinux64 (editorOk={editorOk}, linuxOk={linuxOk}).";
                return false;
            }

            if (win64Ok || win32Ok || osxOk || arm64Ok)
            {
                message = $"{soAssetPath}: Windows/OSX platforms must stay disabled.";
                return false;
            }

            message = $"{soAssetPath}: platforms OK (Editor Linux + Linux64).";
            return true;
        }

        private static bool IsWindowsArm64Compatible(PluginImporter importer)
        {
            foreach (var platformId in new[]
                     {
                         "WindowsStandaloneArm64",
                         "StandaloneWindowsArm64",
                         "WinArm64"
                     })
            {
                try
                {
                    if (importer.GetCompatibleWithPlatform(platformId))
                        return true;
                }
                catch
                {
                    // Platform id unknown on this Editor.
                }
            }

            // Editors without the Windows ARM64 player module often cannot report the
            // platform via GetCompatibleWithPlatform. Use package layout + meta flags.
            var assetPath = AssetDatabase.GetAssetPath(importer)?.Replace('\\', '/') ?? string.Empty;
            if (assetPath.IndexOf("/Plugins/Windows/ARM64/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (var anyKey in new[] { "Any", "" })
            {
                try
                {
                    var exclude = importer.GetPlatformData(anyKey, "Exclude Windows ARM64");
                    if (string.Equals(exclude, "0", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(exclude, "false", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Ignore missing keys.
                }
            }

            foreach (var armKey in new[] { "Windows ARM64", "WindowsStandaloneArm64", "StandaloneWindowsArm64" })
            {
                try
                {
                    var cpu = importer.GetPlatformData(armKey, "CPU");
                    if (!string.IsNullOrEmpty(cpu)
                        && !string.Equals(cpu, "None", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Ignore missing keys.
                }
            }

            return false;
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
