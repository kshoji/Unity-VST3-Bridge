#nullable enable
using com.IvanMurzak.McpPlugin;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using VstHost = jp.kshoji.unity.vst3nativehost.VstHostManager;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    [AiToolType]
    public partial class Tool_VstHost
    {
        internal static VstHost Host => VstHostToolHelpers.Host;

        internal static bool TryRequireAudioSession(string toolId, out string? error) =>
            VstHostToolHelpers.TryRequireAudioSession(toolId, out error);

        internal static bool TryRequireInitialized(string toolId, out string? error) =>
            VstHostToolHelpers.TryRequireInitialized(toolId, out error);

        internal static bool TryRequireLoaded(string toolId, int pluginId, out string? error) =>
            VstHostToolHelpers.TryRequireLoaded(toolId, pluginId, out error);

        internal static void RememberScan(System.Collections.Generic.List<VstHost.ScannedPlugin> plugins) =>
            VstHostToolHelpers.RememberScan(plugins);

        internal static bool TryResolveParam(
            int pluginId,
            long? paramId,
            string? title,
            out jp.kshoji.unity.vst3nativehost.VstParamInfo info,
            out string? error) =>
            VstHostToolHelpers.TryResolveParam(pluginId, paramId, title, out info, out error);

        internal static string FormatParamsList(
            int pluginId,
            bool includeHidden,
            int max = 200,
            bool preferAutomate = false) =>
            VstHostToolHelpers.FormatParamsList(pluginId, includeHidden, max, preferAutomate);

        internal static bool HasLoadedAssembly(string assemblyName) =>
            VstHostToolHelpers.HasLoadedAssembly(assemblyName);

        internal static string DescribeFeature(
            string id,
            bool available,
            string detail,
            string? docsRelative = null) =>
            VstHostToolHelpers.DescribeFeature(id, available, detail, docsRelative);
    }
}
