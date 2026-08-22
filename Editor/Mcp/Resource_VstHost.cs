#nullable enable
using System;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;
using Diagnostics = jp.kshoji.unity.vst3nativehost.VstHostAudioDiagnostics;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    using Consts = com.IvanMurzak.McpPlugin.Common.Consts;

    /// <summary>Read-only MCP resources for VST3 host state (Phase 1).</summary>
    [AiResourceType]
    public partial class Resource_VstHost
    {
        // NOTE: Every [AiResource] MUST set ListResources. An empty default makes
        // McpPluginBuilder.WithResource call Type.GetMethod("") and throw
        // "Method {FullName} not found in type {Name}".

        [AiResource(
            Name = "VST3 features",
            Route = "vst3://features",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Optional integration availability (same as vst3-features-status).")]
        public ResponseResourceContent[] Features(string uri) =>
            TextFromTool(uri, () => new Tool_VstHost().FeaturesStatus());

        [AiResource(
            Name = "VST3 scanned plugins",
            Route = "vst3://scanned",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Last scan result from vst3-scan / vst3-scan-folder (empty until scanned).")]
        public ResponseResourceContent[] Scanned(string uri) =>
            MainThread.Instance.Run(() =>
            {
                var text = Tool_VstHost.LastScanned.Count == 0
                    ? "{\"hint\":\"Call vst3-scan first\",\"count\":0}"
                    : Tool_VstHost.FormatScanned(Tool_VstHost.LastScanned);
                return AsArray(ResponseResourceContent.CreateText(uri, Consts.MimeType.TextJson, text));
            });

        [AiResource(
            Name = "VST3 loaded instances",
            Route = "vst3://instances",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Currently loaded plugin instances.")]
        public ResponseResourceContent[] Instances(string uri) =>
            MainThread.Instance.Run(() =>
            {
                var host = jp.kshoji.unity.vst3nativehost.VstHostManager.Instance;
                var text = !host.IsInitialized
                    ? "{\"hint\":\"Host not initialized\",\"loadedCount\":0}"
                    : new Tool_VstHost().ListInstances();
                return AsArray(ResponseResourceContent.CreateText(uri, Consts.MimeType.TextJson, text));
            });

        [AiResource(
            Name = "VST3 recent activity",
            Route = "vst3://activity/recent",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Recent host activity ring buffer.")]
        public ResponseResourceContent[] ActivityRecent(string uri) =>
            MainThread.Instance.Run(() =>
            {
                Activity.PumpMainThread();
                var entries = Activity.GetRecent(64);
                var sb = new StringBuilder();
                sb.Append($"{{\"enabled\":{Activity.Enabled.ToString().ToLowerInvariant()},\"count\":{entries.Length},\"lines\":[");
                for (var i = 0; i < entries.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    var e = entries[i];
                    sb.Append(
                        $"{{\"t\":{e.TimeSeconds:0.###},\"kind\":\"{e.Kind}\",\"pluginId\":{e.PluginId}," +
                        $"\"detail\":{EscapeJson(e.Detail)}}}");
                }

                sb.Append("]}");
                return AsArray(ResponseResourceContent.CreateText(uri, Consts.MimeType.TextJson, sb.ToString()));
            });

        [AiResource(
            Name = "VST3 audio diagnostics",
            Route = "vst3://diagnostics",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Lifetime Process/blockSize/buffer diagnostics counters.")]
        public ResponseResourceContent[] DiagnosticsResource(string uri) =>
            MainThread.Instance.Run(() =>
            {
                Diagnostics.Snapshot(
                    out var processFail,
                    out var blockSkip,
                    out var bufferClip,
                    out var skipFrames,
                    out var skipLimit,
                    out var clipFrames,
                    out var clipCap);
                var text =
                    $"{{\"processFail\":{processFail},\"blockSizeSkip\":{blockSkip}," +
                    $"\"lastSkipFrames\":{skipFrames},\"lastSkipLimit\":{skipLimit}," +
                    $"\"bufferCapacityClip\":{bufferClip},\"lastClipFrames\":{clipFrames}," +
                    $"\"lastClipCapacity\":{clipCap}}}";
                return AsArray(ResponseResourceContent.CreateText(uri, Consts.MimeType.TextJson, text));
            });

        [AiResource(
            Name = "VST3 project settings",
            Route = "vst3://settings",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(ListAll),
            Description = "Project Settings → VST3 Host.")]
        public ResponseResourceContent[] Settings(string uri) =>
            TextFromTool(uri, () => new Tool_VstHost().SettingsGet());

        public ResponseListResource[] ListAll() =>
            new[]
            {
                new ResponseListResource("vst3://features", "VST3 features", true, Consts.MimeType.TextJson),
                new ResponseListResource("vst3://scanned", "VST3 scanned plugins", true, Consts.MimeType.TextJson),
                new ResponseListResource("vst3://instances", "VST3 loaded instances", true, Consts.MimeType.TextJson),
                new ResponseListResource("vst3://activity/recent", "VST3 recent activity", true, Consts.MimeType.TextJson),
                new ResponseListResource("vst3://diagnostics", "VST3 audio diagnostics", true, Consts.MimeType.TextJson),
                new ResponseListResource("vst3://settings", "VST3 project settings", true, Consts.MimeType.TextJson),
            };

        static ResponseResourceContent[] TextFromTool(string uri, Func<string> tool)
        {
            return MainThread.Instance.Run(() =>
            {
                var text = tool();
                return AsArray(ResponseResourceContent.CreateText(uri, Consts.MimeType.TextJson, text));
            });
        }

        static ResponseResourceContent[] AsArray(ResponseResourceContent content) =>
            new[] { content };

        static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + "\"";
        }
    }
}
