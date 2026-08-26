#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using BuildVerify = jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-verify-platforms",
            Title = "VST3 / Verify Platforms",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Verify native VstHostNative binaries / importer flags (Window → VST3 Host → Verify Plugin Platforms). " +
            "Edit Mode OK. Does not run Standalone/IL2CPP builds.")]
        public string VerifyPlatforms()
        {
            return MainThread.Instance.Run(() =>
            {
                var ok = BuildVerify.VerifyPluginPlatforms(out var message);
                return ok
                    ? $"[Success] vst3-verify-platforms {message}"
                    : $"[Error] vst3-verify-platforms {message}";
            });
        }
    }
}
