#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.scriptableaudio;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.scriptableaudio
{
    [AiToolType]
    public class Tool_VstScriptableAudio
    {
        const string DefaultObjectName = "__VstHostMcpSa";

        [AiTool("vst3-sa-generator-setup", Title = "VST3 / Scriptable Audio Generator")]
        [Description(
            "Ensure VstHostGenerator + AudioSource (Scriptable Audio). Unity 6000.3+ non-WebGL. " +
            "Play Mode required for audible Process. Prefer vst3-dsp-midi-schedule for timed notes.")]
        public string SaGeneratorSetup
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Generator gain.")]
            float gain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpSa.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (pluginId < 1)
                    return "[Error] vst3-sa-generator-setup: pluginId must be >= 1.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                // Avoid dual Filter+Generator path.
                var filter = go.GetComponent<jp.kshoji.unity.vst3nativehost.VstHostAudioFilter>();
                if (filter != null)
                    filter.enabled = false;
                var graph = go.GetComponent<jp.kshoji.unity.vst3nativehost.VstAudioGraph>();
                if (graph != null)
                    graph.enabled = false;

                var gen = go.GetComponent<VstHostGenerator>() ?? go.AddComponent<VstHostGenerator>();
                gen.PluginId = pluginId;
                gen.Gain = gain;
                var source = gen.SetupAudioSource();

                return
                    $"[Success] vst3-sa-generator-setup gameObject={go.name} pluginId={pluginId} " +
                    $"gain={gain} audioPlaying={source != null && source.isPlaying} " +
                    $"playMode={UnityEditor.EditorApplication.isPlaying}";
            });
        }
    }
}
