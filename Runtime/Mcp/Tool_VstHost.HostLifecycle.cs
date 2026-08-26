#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-host-init", Title = "VST3 / Host Init")]
        [Description(
            "Initialize the VST3 native host. Prefer Play Mode / running build so AudioSettings match the DSP path. " +
            "Default: InitializeFromAudioSettings.")]
        public string HostInit
        (
            [Description("When true (default), use InitializeFromAudioSettings. When false, use sampleRate/blockSize.")]
            bool fromAudioSettings = true,
            [Description("Sample rate when fromAudioSettings=false.")]
            int sampleRate = 44100,
            [Description("Block size when fromAudioSettings=false (clamped 256–8192).")]
            int blockSize = 512,
            [Description("Headroom multiplier for InitializeFromAudioSettings (default 2).")]
            int blockSizeHeadroom = 2
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var ok = fromAudioSettings
                    ? Host.InitializeFromAudioSettings(blockSizeHeadroom)
                    : Host.Initialize(sampleRate, blockSize);
                if (!ok)
                    return "[Error] vst3-host-init failed (see Console).";

                return
                    $"[Success] vst3-host-init sampleRate={Host.SampleRate} blockSize={Host.BlockSize} " +
                    $"session={McpExecutionContext.SessionKind} fromAudioSettings={fromAudioSettings}";
            });
        }

        [AiTool("vst3-host-terminate", Title = "VST3 / Host Terminate")]
        [Description(
            "Terminate the native VST host and clear loaded instances. " +
            "May return busy if Process is still active — retry later.")]
        public string HostTerminate()
        {
            return MainThread.Instance.Run(() =>
            {
                Host.Terminate();
                return
                    $"[Success] vst3-host-terminate isInitialized={Host.IsInitialized} " +
                    $"loadedCount={Host.LoadedPlugins.Count}";
            });
        }
    }
}
