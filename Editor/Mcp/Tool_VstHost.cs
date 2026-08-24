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

        /// <summary>
        /// Resolve a parameter by numeric id and/or title / shortTitle substring (case-insensitive).
        /// Exact title match wins over partial; first partial match otherwise.
        /// </summary>
        internal static bool TryResolveParam(
            int pluginId,
            long? paramId,
            string? title,
            out jp.kshoji.unity.vst3nativehost.VstParamInfo info,
            out string? error)
        {
            info = default;
            error = null;
            var list = Host.GetParameters(pluginId);
            if (list.Count == 0)
            {
                error = $"[Error] No parameters for pluginId={pluginId}.";
                return false;
            }

            if (paramId.HasValue)
            {
                var id = unchecked((uint)paramId.Value);
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Id == id)
                    {
                        info = list[i];
                        return true;
                    }
                }

                error = $"[Error] paramId={id} not found on pluginId={pluginId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                error = "[Error] Provide paramId or title (substring of Title/ShortTitle).";
                return false;
            }

            var needle = title.Trim();
            jp.kshoji.unity.vst3nativehost.VstParamInfo? exact = null;
            jp.kshoji.unity.vst3nativehost.VstParamInfo? partial = null;
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (string.Equals(p.Title, needle, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.ShortTitle, needle, StringComparison.OrdinalIgnoreCase))
                {
                    exact = p;
                    break;
                }

                if (partial == null
                    && ((p.Title != null && p.Title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (p.ShortTitle != null
                            && p.ShortTitle.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    partial = p;
                }
            }

            if (exact.HasValue)
            {
                info = exact.Value;
                return true;
            }

            if (partial.HasValue)
            {
                info = partial.Value;
                return true;
            }

            error = $"[Error] No parameter matching title '{needle}' on pluginId={pluginId}.";
            return false;
        }

        /// <summary>Format parameter list for tools / resources.</summary>
        public static string FormatParamsList(
            int pluginId,
            bool includeHidden,
            int max = 200,
            bool preferAutomate = false)
        {
            var list = Host.GetParameters(pluginId);
            var sb = new StringBuilder();
            var shown = 0;
            sb.Append($"pluginId={pluginId} paramCount={list.Count} preferAutomate={preferAutomate}");

            void AppendOne(jp.kshoji.unity.vst3nativehost.VstParamInfo p, bool suggested)
            {
                if (shown >= max)
                    return;
                Host.TryGetParameterNormalized(pluginId, p.Id, out var v);
                sb.Append('\n');
                if (suggested)
                    sb.Append("suggested ");
                sb.Append(
                    $"id={p.Id} title={p.Title} short={p.ShortTitle} units={p.Units} " +
                    $"step={p.StepCount} default={p.DefaultNormalized:0.###} value={v:0.###} " +
                    $"flags={p.ParamFlags} readOnly={p.IsReadOnly}");
                shown++;
            }

            bool IsPreferred(jp.kshoji.unity.vst3nativehost.VstParamInfo p)
            {
                if (p.IsReadOnly)
                    return false;
                var flags = p.ParamFlags;
                if ((flags & jp.kshoji.unity.vst3nativehost.VstParamFlags.IsProgramChange) != 0)
                    return false;
                if ((flags & jp.kshoji.unity.vst3nativehost.VstParamFlags.IsList) != 0 && p.StepCount > 1)
                    return false;
                return (flags & jp.kshoji.unity.vst3nativehost.VstParamFlags.CanAutomate) != 0;
            }

            if (preferAutomate)
            {
                for (var i = 0; i < list.Count && shown < max; i++)
                {
                    var p = list[i];
                    if (!includeHidden && (p.ParamFlags & jp.kshoji.unity.vst3nativehost.VstParamFlags.IsHidden) != 0)
                        continue;
                    if (!IsPreferred(p))
                        continue;
                    AppendOne(p, suggested: true);
                }

                if (shown == 0)
                    sb.Append("\n(no preferred automate params; listing all non-hidden)");
            }

            var suggestedCount = shown;
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (!includeHidden && (p.ParamFlags & jp.kshoji.unity.vst3nativehost.VstParamFlags.IsHidden) != 0)
                    continue;
                if (preferAutomate && suggestedCount > 0 && IsPreferred(p))
                    continue; // already listed as suggested
                if (shown >= max)
                {
                    sb.Append($"\n… truncated at max={max}");
                    break;
                }

                AppendOne(p, suggested: false);
            }

            return sb.ToString();
        }
    }
}
