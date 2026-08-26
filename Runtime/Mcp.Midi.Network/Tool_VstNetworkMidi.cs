#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.midi
{
    [AiToolType]
    public class Tool_VstNetworkMidi
    {
        internal const string DefaultObjectName = "__VstHostMcpNetwork";

        [AiTool("vst3-network-midi-link", Title = "VST3 / Network MIDI Link")]
        [Description(
            "Ensure VstHostNetworkMidiLink + Adapter filtered to network virtual device " +
            "(default network:remote). Requires FEATURE_MIDI_NETWORK.")]
        public string NetworkMidiLink
        (
            [Description("Virtual device id override.")]
            string? virtualDeviceId = null,
            [Description("Target plugin id for Adapter.")]
            int targetPluginId = 0,
            [Description("GameObject name. Empty = __VstHostMcpNetwork.")]
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

                var adapter = go.GetComponent<VstHostMidiAdapter>() ?? go.AddComponent<VstHostMidiAdapter>();
                if (targetPluginId >= 1)
                    adapter.TargetPluginId = targetPluginId;

                var link = go.GetComponent<VstHostNetworkMidiLink>()
                           ?? go.AddComponent<VstHostNetworkMidiLink>();
                if (!string.IsNullOrWhiteSpace(virtualDeviceId))
                    link.VirtualDeviceId = virtualDeviceId.Trim();

                link.Configure(adapter);
                link.Apply();

                return
                    $"[Success] vst3-network-midi-link gameObject={go.name} " +
                    $"deviceId={link.VirtualDeviceId} adapterPluginId={adapter.TargetPluginId}";
            });
        }
    }
}
