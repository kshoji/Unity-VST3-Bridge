#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;
using EventSink = jp.kshoji.unity.vst3nativehost.VstHostEventSink;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-event-sink-setup", Title = "VST3 / Event Sink Setup")]
        [Description(
            "Ensure GameObject has VstHostEventSink (UnityEvent / MidiInputRouter friendly). " +
            "Works without MIDI Plugin. Creates __VstHostMcpMidi when gameObjectName is empty.")]
        public string EventSinkSetup
        (
            [Description("Target plugin instance id (or leave -1 and set later).")]
            int pluginId = -1,
            [Description("MIDI channel 0–15 for Note/CC helpers.")]
            int channel = 0,
            [Description("Default NoteOn velocity.")]
            int defaultVelocity = 100,
            [Description("Target GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? VstHostToolHelpers.DefaultMidiObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var sink = go.GetComponent<EventSink>();
                if (sink == null)
                    sink = go.AddComponent<EventSink>();

                if (pluginId >= 1)
                    sink.PluginId = pluginId;

                sink.ConfigureMidi(channel, defaultVelocity);

                return
                    $"[Success] vst3-event-sink-setup gameObject={go.name} pluginId={sink.PluginId} " +
                    $"channel={channel} defaultVelocity={defaultVelocity}";
            });
        }
    }
}
