#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.midi
{
    [AiToolType]
    public class Tool_VstChunity
    {
        internal const string DefaultObjectName = "__VstHostMcpChuck";

        [AiTool("vst3-chuck-effect", Title = "VST3 / Chunity Effect Bridge")]
        [Description(
            "Ensure VstHostChuckEffectBridge. target=AudioFilterEffect|AudioGraphExternalInput. " +
            "Place below Chuck in Inspector order. Requires FEATURE_CHUNITY.")]
        public string ChuckEffect
        (
            [Description("Effect plugin id.")]
            int effectPluginId,
            [Description("AudioFilterEffect or AudioGraphExternalInput.")]
            string target = "AudioFilterEffect",
            [Description("Output gain.")]
            float outputGain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpChuck.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (effectPluginId < 1)
                    return "[Error] vst3-chuck-effect: effectPluginId must be >= 1.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name) ?? new GameObject(name);
                if (go.hideFlags == HideFlags.None)
                    go.hideFlags = HideFlags.DontSave;

                var bridge = go.GetComponent<VstHostChuckEffectBridge>()
                             ?? go.AddComponent<VstHostChuckEffectBridge>();
                bridge.EffectPluginId = effectPluginId;

                var kind = target.IndexOf("Graph", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? VstHostChuckEffectBridge.TargetKind.AudioGraphExternalInput
                    : VstHostChuckEffectBridge.TargetKind.AudioFilterEffect;
                bridge.Target = kind;
                bridge.OutputGain = outputGain;
                bridge.Apply();

                return
                    $"[Success] vst3-chuck-effect gameObject={go.name} effectPluginId={effectPluginId} " +
                    $"target={kind} outputGain={outputGain}";
            });
        }

        [AiTool("vst3-chuck-event-midi-link", Title = "VST3 / Chunity Event MIDI Link")]
        [Description(
            "Ensure VstHostChuckEventMidiLink + Adapter; filter Adapter to Chuck event virtual device.")]
        public string ChuckEventMidiLink
        (
            [Description("Optional virtual device id override.")]
            string? virtualDeviceIdOverride = null,
            [Description("GameObject name. Empty = __VstHostMcpChuck.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name) ?? new GameObject(name);
                if (go.hideFlags == HideFlags.None)
                    go.hideFlags = HideFlags.DontSave;

                if (go.GetComponent<VstHostMidiAdapter>() == null)
                    go.AddComponent<VstHostMidiAdapter>();

                var link = go.GetComponent<VstHostChuckEventMidiLink>()
                           ?? go.AddComponent<VstHostChuckEventMidiLink>();
                link.Configure(
                    go.GetComponent<VstHostMidiAdapter>(),
                    string.IsNullOrWhiteSpace(virtualDeviceIdOverride)
                        ? null
                        : virtualDeviceIdOverride.Trim());
                link.Apply();

                return
                    $"[Success] vst3-chuck-event-midi-link gameObject={go.name} " +
                    $"deviceId={link.ResolveDeviceId()}";
            });
        }
    }
}
