#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-state-get",
            Title = "VST3 / State Get",
            ReadOnlyHint = true)]
        [Description("Get opaque plugin state blob as Base64.")]
        public string StateGet
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Max Base64 characters to include (0 = metadata only). Default 8192.")]
            int maxBase64Chars = 8192
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-state-get", pluginId, out var error))
                    return error!;

                var blob = Host.GetState(pluginId);
                if (blob == null)
                    return $"[Error] vst3-state-get failed pluginId={pluginId}";

                var b64 = Convert.ToBase64String(blob);
                var truncated = maxBase64Chars > 0 && b64.Length > maxBase64Chars;
                var payload = maxBase64Chars <= 0
                    ? string.Empty
                    : (truncated ? b64.Substring(0, maxBase64Chars) : b64);

                return
                    $"[Success] vst3-state-get pluginId={pluginId} bytes={blob.Length} " +
                    $"base64Chars={b64.Length} truncated={truncated}" +
                    (string.IsNullOrEmpty(payload) ? string.Empty : $"\nbase64={payload}");
            });
        }

        [AiTool("vst3-state-set", Title = "VST3 / State Set")]
        [Description("Apply opaque plugin state from Base64.")]
        public string StateSet
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Base64-encoded state blob.")]
            string base64
        )
        {
            if (string.IsNullOrWhiteSpace(base64))
                return "[Error] vst3-state-set: base64 is required.";

            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-state-set", pluginId, out var error))
                    return error!;

                byte[] blob;
                try
                {
                    blob = Convert.FromBase64String(base64.Trim());
                }
                catch (FormatException)
                {
                    return "[Error] vst3-state-set: invalid Base64.";
                }

                if (!Host.SetState(pluginId, blob))
                    return $"[Error] vst3-state-set failed pluginId={pluginId} bytes={blob.Length}";

                Activity.PumpMainThread();
                return $"[Success] vst3-state-set pluginId={pluginId} bytes={blob.Length}";
            });
        }
    }
}
