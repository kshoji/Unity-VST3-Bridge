#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;
using BuildVerify = jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-host-status",
            Title = "VST3 / Host Status",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Read-only VST host snapshot: IsInitialized, SampleRate, BlockSize, " +
            "loaded plugin count, and Play Mode. Safe in Edit Mode. " +
            "Does not call Initialize or Process. " +
            "Note: after domain reload the C# manager may report not initialized while " +
            "native is still up — use vst3-host-init / terminate to recover.")]
        public string HostStatus()
        {
            return MainThread.Instance.Run(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[Success] vst3-host-status");
                sb.Append($" isInitialized={Host.IsInitialized}");
                sb.Append($" sampleRate={Host.SampleRate}");
                sb.Append($" blockSize={Host.BlockSize}");
                sb.Append($" loadedCount={Host.LoadedPlugins.Count}");
                sb.Append($" playMode={IsPlayMode}");
                sb.Append($" audioOutputSampleRate={AudioSettings.outputSampleRate}");

                if (Host.LoadedPlugins.Count > 0)
                {
                    sb.Append(" loaded=[");
                    var first = true;
                    foreach (var kv in Host.LoadedPlugins)
                    {
                        if (!first)
                            sb.Append("; ");
                        first = false;
                        sb.Append($"id={kv.Key} uid={kv.Value.Uid} path={kv.Value.FilePath}");
                    }

                    sb.Append(']');
                }

                return sb.ToString();
            });
        }

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
