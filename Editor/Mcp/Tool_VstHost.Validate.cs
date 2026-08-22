#nullable enable
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;
using Filter = jp.kshoji.unity.vst3nativehost.VstHostAudioFilter;
using BuildVerify = jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-validate",
            Title = "VST3 / Validate",
            ReadOnlyHint = true)]
        [Description(
            "Diagnostics checklist: native plugin platforms, host init, loaded instances, " +
            "VstHostAudioFilter / AudioSource in the scene, Play Mode. " +
            "Does not guarantee commercial plugin compatibility. Edit Mode OK for most checks.")]
        public string Validate()
        {
            return MainThread.Instance.Run(() =>
            {
                var sb = new StringBuilder();
                sb.Append("[Success] vst3-validate");
                sb.Append($"\nplayMode={IsPlayMode}");

                var platformsOk = BuildVerify.VerifyPluginPlatforms(out var platformsMsg);
                sb.Append($"\nnativePlatforms={(platformsOk ? "ok" : "fail")}: {platformsMsg}");

                sb.Append($"\nhostInitialized={Host.IsInitialized}");
                if (Host.IsInitialized)
                    sb.Append($" sampleRate={Host.SampleRate} blockSize={Host.BlockSize}");
                else
                    sb.Append(" hint=call vst3-host-init");

                sb.Append($"\nloadedCount={Host.LoadedPlugins.Count}");

#if UNITY_2023_1_OR_NEWER
                var filters = Object.FindObjectsByType<Filter>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
#else
                var filters = Object.FindObjectsOfType<Filter>();
#endif
                sb.Append($"\naudioFilterCount={filters.Length}");
                for (var i = 0; i < filters.Length; i++)
                {
                    var f = filters[i];
                    var src = f.GetComponent<AudioSource>();
                    sb.Append(
                        $"\n  filter[{i}] go={f.gameObject.name} pluginId={f.PluginId} mode={f.Mode} " +
                        $"gain={f.OutputGain} audioSource={(src != null)} playing={(src != null && src.isPlaying)}");
                }

                if (!IsPlayMode)
                    sb.Append("\nhint=Enter Play Mode for note/Process validation");
                if (Host.IsInitialized && Host.LoadedPlugins.Count == 0)
                    sb.Append("\nhint=No loaded instances — vst3-scan then vst3-load");
                if (IsPlayMode && filters.Length == 0 && Host.LoadedPlugins.Count > 0)
                    sb.Append("\nhint=Loaded plugins have no VstHostAudioFilter — vst3-setup-audio-filter");

                sb.Append($"\ndocs={DocsBaseUrl}verification.md");
                return sb.ToString();
            });
        }

        [AiTool(
            "vst3-verify-platforms",
            Title = "VST3 / Verify Platforms",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Verify native VstHostNative binaries / importer flags (Window → VST3 Host → Verify Plugin Platforms). " +
            "Edit Mode OK. Does not run Standalone/IL2CPP builds.")]
        public string VerifyPlatforms()
        {
            return MainThread.Instance.Run(() =>
            {
                var ok = BuildVerify.VerifyPluginPlatforms(out var message);
                return ok
                    ? $"[Success] vst3-verify-platforms {message}"
                    : $"[Error] vst3-verify-platforms {message}";
            });
        }
    }
}
