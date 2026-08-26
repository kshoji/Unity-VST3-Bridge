#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using com.IvanMurzak.McpPlugin;
using UnityEditor;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using VstHost = jp.kshoji.unity.vst3nativehost.VstHostManager;
using VstEditorSettings = jp.kshoji.unity.vst3nativehost.Editor.VstHostProjectSettings;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    /// <summary>
    /// Unity-MCP tools for Unity Plugin Host for VST3 (<c>vst3-*</c>).
    /// Editor-only MCP tools (settings, verify, asset authoring). Runtime host tools are in Mcp.Runtime.
    /// </summary>
    [AiToolType]
    public partial class Tool_VstHost
    {
        internal const string PackageName = VstHostToolHelpers.PackageName;
        internal const string McpAssemblyName = VstHostToolHelpers.McpEditorAssemblyName;
        internal const string DocsBaseUrl = VstHostToolHelpers.DocsBaseUrl;

        internal static string ErrorRequiresPlayMode(string toolId) =>
            VstHostToolHelpers.ErrorRequiresPlayMode(toolId);

        internal static string ErrorRequiresInitialized(string toolId) =>
            VstHostToolHelpers.ErrorRequiresInitialized(toolId);

        internal static string ErrorPluginNotLoaded(string toolId, int pluginId) =>
            VstHostToolHelpers.ErrorPluginNotLoaded(toolId, pluginId);

        internal static bool IsPlayMode => McpExecutionContext.IsEditorPlayMode;

        internal static VstHost Host => VstHostToolHelpers.Host;

        internal static bool TryRequirePlayMode(string toolId, out string? error) =>
            VstHostToolHelpers.TryRequireAudioSession(toolId, out error);

        internal static bool TryRequireInitialized(string toolId, out string? error) =>
            VstHostToolHelpers.TryRequireInitialized(toolId, out error);

        internal static bool TryRequireLoaded(string toolId, int pluginId, out string? error) =>
            VstHostToolHelpers.TryRequireLoaded(toolId, pluginId, out error);

        internal static void RememberScan(List<VstHost.ScannedPlugin> plugins) =>
            VstHostToolHelpers.RememberScan(plugins);

        internal static IReadOnlyList<VstHost.ScannedPlugin> LastScannedOrEmpty =>
            VstHostToolHelpers.LastScannedOrEmpty;

        public static IReadOnlyList<VstHost.ScannedPlugin> LastScanned =>
            VstHostToolHelpers.LastScanned;

        public static string FormatScanned(IReadOnlyList<VstHost.ScannedPlugin> plugins, int max = 200) =>
            VstHostToolHelpers.FormatScanned(plugins, max);

        internal static string FormatInstances() => VstHostToolHelpers.FormatInstances();

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

        internal static bool HasLoadedAssembly(string assemblyName) =>
            VstHostToolHelpers.HasLoadedAssembly(assemblyName);

        internal static bool HasScriptingDefine(string symbol) =>
            McpEditorFeatureProbe.HasScriptingDefine(symbol);

        internal static string DescribeFeature(
            string id,
            bool available,
            string detail,
            string? docsRelative = null) =>
            VstHostToolHelpers.DescribeFeature(id, available, detail, docsRelative);

        internal static bool TryResolveParam(
            int pluginId,
            long? paramId,
            string? title,
            out jp.kshoji.unity.vst3nativehost.VstParamInfo info,
            out string? error) =>
            VstHostToolHelpers.TryResolveParam(pluginId, paramId, title, out info, out error);

        public static string FormatParamsList(
            int pluginId,
            bool includeHidden,
            int max = 200,
            bool preferAutomate = false) =>
            VstHostToolHelpers.FormatParamsList(pluginId, includeHidden, max, preferAutomate);
    }
}
