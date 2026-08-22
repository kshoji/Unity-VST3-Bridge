#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.IvanMurzak.McpPlugin;
using UnityEditor;
using UnityEngine;
using VstHost = jp.kshoji.unity.vst3nativehost.VstHostManager;
using VstEditorSettings = jp.kshoji.unity.vst3nativehost.Editor.VstHostProjectSettings;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    /// <summary>
    /// Unity-MCP tools for Unity Plugin Host for VST3 (<c>vst3-*</c>).
    /// Compiles only when <c>com.ivanmurzak.unity.mcp</c> is present and
    /// <c>UNITY_MCP_READY</c> (NuGet deps resolved).
    /// </summary>
    [AiToolType]
    public partial class Tool_VstHost
    {
        internal const string PackageName = "jp.kshoji.unity.vst3nativehost";
        internal const string McpAssemblyName = "jp.kshoji.unity.vst3nativehost.Mcp";
        internal const string DocsBaseUrl =
            "https://github.com/kshoji/Unity-VST3-Bridge/blob/main/Documentation~/";
        internal const string DefaultAudioObjectName = "__VstHostMcpAudio";

        private static List<VstHost.ScannedPlugin>? lastScanned;

        internal static string ErrorRequiresPlayMode(string toolId) =>
            $"[Error] {toolId} requires Play Mode (audio Process / note playback). " +
            "Enter Play Mode and retry. Scan/list/status tools may work in Edit Mode.";

        internal static string ErrorRequiresInitialized(string toolId) =>
            $"[Error] {toolId} requires an initialized VST host. " +
            "Call vst3-host-init first (prefer Play Mode + InitializeFromAudioSettings).";

        internal static string ErrorPluginNotLoaded(string toolId, int pluginId) =>
            $"[Error] {toolId}: pluginId={pluginId} is not loaded. Call vst3-load / vst3-list-instances.";

        internal static bool IsPlayMode => EditorApplication.isPlaying;

        internal static VstHost Host => VstHost.Instance;

        internal static bool TryRequirePlayMode(string toolId, out string? error)
        {
            if (IsPlayMode)
            {
                error = null;
                return true;
            }

            error = ErrorRequiresPlayMode(toolId);
            return false;
        }

        internal static bool TryRequireInitialized(string toolId, out string? error)
        {
            if (Host.IsInitialized)
            {
                error = null;
                return true;
            }

            error = ErrorRequiresInitialized(toolId);
            return false;
        }

        internal static bool TryRequireLoaded(string toolId, int pluginId, out string? error)
        {
            if (!TryRequireInitialized(toolId, out error))
                return false;
            if (pluginId < 1 || !Host.LoadedPlugins.ContainsKey(pluginId))
            {
                error = ErrorPluginNotLoaded(toolId, pluginId);
                return false;
            }

            error = null;
            return true;
        }

        internal static void RememberScan(List<VstHost.ScannedPlugin> plugins) =>
            lastScanned = plugins;

        internal static IReadOnlyList<VstHost.ScannedPlugin> LastScannedOrEmpty =>
            (IReadOnlyList<VstHost.ScannedPlugin>?)lastScanned
            ?? Array.Empty<VstHost.ScannedPlugin>();

        /// <summary>Last scan results for MCP resources (empty until vst3-scan).</summary>
        public static IReadOnlyList<VstHost.ScannedPlugin> LastScanned => LastScannedOrEmpty;

        /// <summary>Format scanned plugins for tools / resources.</summary>
        public static string FormatScanned(IReadOnlyList<VstHost.ScannedPlugin> plugins, int max = 200)
        {
            var sb = new StringBuilder();
            var n = Math.Min(plugins.Count, Math.Max(1, max));
            sb.Append($"count={plugins.Count}");
            if (plugins.Count > n)
                sb.Append($" showing={n}");
            for (var i = 0; i < n; i++)
            {
                var p = plugins[i];
                sb.Append('\n');
                sb.Append(
                    $"[{i}] name={p.Name} vendor={p.Vendor} category={p.Category} uid={p.Uid} path={p.FilePath}");
            }

            return sb.ToString();
        }

        internal static string FormatInstances()
        {
            var sb = new StringBuilder();
            sb.Append($"loadedCount={Host.LoadedPlugins.Count}");
            foreach (var kv in Host.LoadedPlugins.OrderBy(k => k.Key))
            {
                sb.Append('\n');
                sb.Append($"id={kv.Key} uid={kv.Value.Uid} path={kv.Value.FilePath}");
            }

            return sb.ToString();
        }

        internal static string FormatSettings()
        {
            var s = VstEditorSettings.instance;
            var folders = s.ExtraScanFolders ?? Array.Empty<string>();
            var sb = new StringBuilder();
            sb.Append($"autoInitializeOnPlay={s.AutoInitializeOnPlay}");
            sb.Append($" preferredPluginNameContains={s.PreferredPluginNameContains}");
            sb.Append($" extraScanFolderCount={folders.Length}");
            for (var i = 0; i < folders.Length; i++)
            {
                sb.Append('\n');
                sb.Append($"extra[{i}]={folders[i]}");
            }

            return sb.ToString();
        }

        internal static bool TryGetPackageVersion(out string version, out string source)
        {
            version = "unknown";
            source = "fallback";

            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(VstHost).Assembly);
                if (info != null && !string.IsNullOrEmpty(info.version))
                {
                    version = info.version;
                    source = info.source.ToString();
                    return true;
                }
            }
            catch (Exception)
            {
                // PackageInfo may throw when assembly is not under Packages/.
            }

            var asm = typeof(VstHost).Assembly;
            var ver = asm.GetName().Version;
            if (ver != null)
            {
                version = ver.ToString();
                source = "assembly";
                return true;
            }

            return false;
        }

        internal static bool HasLoadedAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal static bool HasScriptingDefine(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return false;

#if UNITY_2021_2_OR_NEWER
            var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(named);
#else
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            return defines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(d => string.Equals(d.Trim(), symbol, StringComparison.Ordinal));
        }

        internal static string DescribeFeature(
            string id,
            bool available,
            string detail,
            string? docsRelative = null)
        {
            var docs = string.IsNullOrEmpty(docsRelative)
                ? string.Empty
                : $" docs={DocsBaseUrl}{docsRelative}";
            return $"{id}: {(available ? "available" : "unavailable")} ({detail}){docs}";
        }
    }
}
