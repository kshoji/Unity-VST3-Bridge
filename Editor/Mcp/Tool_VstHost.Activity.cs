#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;
using Diagnostics = jp.kshoji.unity.vst3nativehost.VstHostAudioDiagnostics;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-activity-read",
            Title = "VST3 / Activity Read",
            ReadOnlyHint = true)]
        [Description(
            "Read recent VST host activity (MIDI / parameter / program / state). " +
            "Pumps deferred MIDI first. Works without the Activity Monitor window. " +
            "Edit Mode OK; Play Mode needed to generate audio-path activity.")]
        public string ActivityRead
        (
            [Description("Max recent lines to return (default 64, cap 256).")]
            int maxCount = 64
        )
        {
            return MainThread.Instance.Run(() =>
            {
                Activity.PumpMainThread();
                var entries = Activity.GetRecent(Math.Max(1, Math.Min(maxCount, 256)));
                var sb = new StringBuilder();
                sb.Append(
                    $"[Success] vst3-activity-read enabled={Activity.Enabled} " +
                    $"recentCount={Activity.RecentCount} returned={entries.Length}");
                for (var i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    sb.Append('\n');
                    sb.Append($"{e.TimeSeconds:0.000} [{e.Kind}] plugin={e.PluginId} {e.Detail}");
                }

                return sb.ToString();
            });
        }

        [AiTool("vst3-activity-clear", Title = "VST3 / Activity Clear")]
        [Description("Clear the recent activity ring buffer.")]
        public string ActivityClear()
        {
            return MainThread.Instance.Run(() =>
            {
                Activity.ClearRecent();
                return "[Success] vst3-activity-clear";
            });
        }

        [AiTool("vst3-activity-enable", Title = "VST3 / Activity Enable")]
        [Description("Enable or disable activity capture (VstHostActivity.Enabled).")]
        public string ActivityEnable
        (
            [Description("When true, capture activity; false disables.")]
            bool enabled = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                Activity.Enabled = enabled;
                return $"[Success] vst3-activity-enable enabled={Activity.Enabled}";
            });
        }

        [AiTool(
            "vst3-diagnostics-read",
            Title = "VST3 / Diagnostics Read",
            ReadOnlyHint = true)]
        [Description(
            "Read lifetime audio diagnostics counters: Process fail, blockSize skip, buffer capacity clip. " +
            "Optional clearLifetime resets counters after read.")]
        public string DiagnosticsRead
        (
            [Description("When true, clear lifetime counters after reading.")]
            bool clearLifetime = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                Diagnostics.PumpMainThreadDiagnostics();
                Diagnostics.Snapshot(
                    out var processFail,
                    out var blockSkip,
                    out var bufferClip,
                    out var skipFrames,
                    out var skipLimit,
                    out var clipFrames,
                    out var clipCap);

                if (clearLifetime)
                    Diagnostics.ClearLifetime();

                return
                    $"[Success] vst3-diagnostics-read processFail={processFail} " +
                    $"blockSizeSkip={blockSkip} (last frames={skipFrames} limit={skipLimit}) " +
                    $"bufferCapacityClip={bufferClip} (last frames={clipFrames} capacity={clipCap}) " +
                    $"cleared={clearLifetime}";
            });
        }
    }
}
