#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-load", Title = "VST3 / Load")]
        [Description(
            "Create a VST3 plugin instance (CreateInstance). Requires vst3-host-init. " +
            "Audible preview needs an audio path (e.g. vst3-setup-audio-filter in Editor).")]
        public string Load
        (
            [Description("Absolute path to the .vst3 bundle/file.")]
            string filePath,
            [Description("Plugin class UID from scan. Optional.")]
            string? uid = null
        )
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "[Error] vst3-load: filePath is required.";

            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireInitialized("vst3-load", out var error))
                    return error!;

                var id = Host.CreateInstance(filePath.Trim(), string.IsNullOrWhiteSpace(uid) ? null : uid);
                if (id < 1)
                    return $"[Error] vst3-load failed path={filePath} uid={uid ?? "(null)"}";

                Host.LoadedPlugins.TryGetValue(id, out var info);
                return
                    $"[Success] vst3-load id={id} uid={info.Uid} path={info.FilePath}";
            });
        }

        [AiTool(
            "vst3-list-instances",
            Title = "VST3 / List Instances",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description("List loaded plugin instance ids with path/uid.")]
        public string ListInstances()
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireInitialized("vst3-list-instances", out var error))
                    return error!;

                return $"[Success] vst3-list-instances\n{VstHostToolHelpers.FormatInstances()}";
            });
        }

        [AiTool("vst3-unload", Title = "VST3 / Unload")]
        [Description("Destroy a loaded plugin instance. Detach audio filters first if routing this id.")]
        public string Unload
        (
            [Description("Plugin instance id from vst3-load.")]
            int pluginId
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireInitialized("vst3-unload", out var error))
                    return error!;

                if (!Host.DestroyInstance(pluginId))
                    return $"[Error] vst3-unload failed id={pluginId}";

                return $"[Success] vst3-unload id={pluginId} loadedCount={Host.LoadedPlugins.Count}";
            });
        }
    }
}
