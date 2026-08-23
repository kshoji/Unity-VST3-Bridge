#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;
using Filter = jp.kshoji.unity.vst3nativehost.VstHostAudioFilter;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-setup-audio-filter", Title = "VST3 / Setup Audio Filter")]
        [Description(
            "Ensure a GameObject has AudioSource + VstHostAudioFilter routed to pluginId. " +
            "Play Mode required for audible Process. Mode: Instrument (default) or Effect. " +
            "Creates __VstHostMcpAudio when gameObjectName is empty.")]
        public string SetupAudioFilter
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Instrument (ignore input) or Effect (process input).")]
            string mode = "Instrument",
            [Description("Output gain (default 1).")]
            float outputGain = 1f,
            [Description("Target GameObject name. Empty = __VstHostMcpAudio.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-setup-audio-filter", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-setup-audio-filter", pluginId, out var loadErr))
                    return loadErr!;

                if (!TryParseMode(mode, out var processMode, out var modeErr))
                    return modeErr!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultAudioObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                if (go.GetComponent<AudioSource>() == null)
                    go.AddComponent<AudioSource>();

                var filter = go.GetComponent<Filter>();
                if (filter == null)
                    filter = go.AddComponent<Filter>();

                filter.Mode = processMode;
                filter.OutputGain = outputGain;
                filter.AttachPlugin(pluginId);
                filter.EnsureSilentSourcePlaying();

                return
                    $"[Success] vst3-setup-audio-filter gameObject={go.name} pluginId={pluginId} " +
                    $"mode={processMode} outputGain={outputGain} audioSourcePlaying={go.GetComponent<AudioSource>().isPlaying}";
            });
        }

        [AiTool("vst3-audio-filter-detach", Title = "VST3 / Audio Filter Detach")]
        [Description(
            "Detach plugin routing from VstHostAudioFilter on a GameObject (before unload). " +
            "Play Mode recommended. Does not destroy the GameObject.")]
        public string AudioFilterDetach
        (
            [Description("GameObject name. Empty = __VstHostMcpAudio.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultAudioObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                    return $"[Error] vst3-audio-filter-detach: GameObject '{name}' not found.";

                var filter = go.GetComponent<Filter>();
                if (filter == null)
                    return $"[Error] vst3-audio-filter-detach: VstHostAudioFilter missing on '{name}'.";

                filter.DetachPlugin();
                return $"[Success] vst3-audio-filter-detach gameObject={name}";
            });
        }

        static bool TryParseMode(string mode, out Filter.ProcessMode processMode, out string? error)
        {
            processMode = Filter.ProcessMode.Instrument;
            error = null;
            if (string.IsNullOrWhiteSpace(mode)
                || mode.Equals("Instrument", System.StringComparison.OrdinalIgnoreCase)
                || mode.Equals("instrument", System.StringComparison.OrdinalIgnoreCase))
            {
                processMode = Filter.ProcessMode.Instrument;
                return true;
            }

            if (mode.Equals("Effect", System.StringComparison.OrdinalIgnoreCase)
                || mode.Equals("effect", System.StringComparison.OrdinalIgnoreCase))
            {
                processMode = Filter.ProcessMode.Effect;
                return true;
            }

            error = $"[Error] vst3-setup-audio-filter: mode must be Instrument or Effect (got '{mode}').";
            return false;
        }
    }
}
