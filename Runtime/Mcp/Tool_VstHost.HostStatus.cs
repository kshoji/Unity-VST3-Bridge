#nullable enable
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-host-status",
            Title = "VST3 / Host Status",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Read-only VST host snapshot: IsNativeHostSupported, IsInitialized, SampleRate, BlockSize, " +
            "loaded plugin count, session kind (editMode/playMode/runtime), and platform. " +
            "Does not call Initialize or Process.")]
        public string HostStatus()
        {
            return MainThread.Instance.Run(() =>
            {
                var sb = new StringBuilder();
                sb.Append("[Success] vst3-host-status");
                sb.Append($" nativeSupported={VstHostManager.IsNativeHostSupported}");
                sb.Append($" isInitialized={Host.IsInitialized}");
                sb.Append($" sampleRate={Host.SampleRate}");
                sb.Append($" blockSize={Host.BlockSize}");
                sb.Append($" loadedCount={Host.LoadedPlugins.Count}");
                sb.Append($" session={McpExecutionContext.SessionKind}");
                sb.Append($" audioOutputSampleRate={AudioSettings.outputSampleRate}");
                sb.Append($" platform={Application.platform}");

                if (!VstHostManager.IsNativeHostSupported)
                {
                    sb.Append(
                        " note=Native host requires Unity Editor or Desktop Standalone " +
                        "(Windows/macOS/Linux); Initialize is a no-op on this platform.");
                }

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
    }
}
