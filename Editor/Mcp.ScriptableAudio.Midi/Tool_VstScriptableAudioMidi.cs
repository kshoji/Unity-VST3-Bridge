#nullable enable
#if FEATURE_SCRIPTABLE_AUDIO && UNITY_6000_3_OR_NEWER && !UNITY_WEBGL
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.scriptableaudio;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.scriptableaudio
{
    [AiToolType]
    public class Tool_VstScriptableAudioMidi
    {
        const string DefaultObjectName = "__VstHostMcpSa";

        [AiTool("vst3-sa-midi-bridge", Title = "VST3 / SA DSP MIDI Out Bridge")]
        [Description(
            "Ensure VstHostDspMidiOutBridge for MidiDspSequenceScheduler.extraTimedMidiOutput. " +
            "Requires FEATURE_SCRIPTABLE_AUDIO + FEATURE_MIDI_PLUGIN.")]
        public string SaMidiBridge
        (
            [Description("Default target plugin id.")]
            int targetPluginId,
            [Description("Optional VstAudioGraph on same GO for channel→instrument resolve.")]
            bool attachAudioGraph = false,
            [Description("GameObject name. Empty = __VstHostMcpSa.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (targetPluginId < 1)
                    return "[Error] vst3-sa-midi-bridge: targetPluginId must be >= 1.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var bridge = go.GetComponent<VstHostDspMidiOutBridge>()
                             ?? go.AddComponent<VstHostDspMidiOutBridge>();
                bridge.TargetPluginId = targetPluginId;
                bridge.BridgeEnabled = true;

                var so = new SerializedObject(bridge);
                if (attachAudioGraph)
                {
                    var graph = go.GetComponent<VstAudioGraph>() ?? go.AddComponent<VstAudioGraph>();
                    so.FindProperty("audioGraph").objectReferenceValue = graph;
                }

                var target = go.GetComponent<VstParameterTarget>() ?? go.AddComponent<VstParameterTarget>();
                target.PluginId = targetPluginId;
                so.FindProperty("parameterTarget").objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();

                return
                    $"[Success] vst3-sa-midi-bridge gameObject={go.name} pluginId={targetPluginId} " +
                    "Assign this component to MidiDspSequenceScheduler.extraTimedMidiOutput.";
            });
        }
    }
}
