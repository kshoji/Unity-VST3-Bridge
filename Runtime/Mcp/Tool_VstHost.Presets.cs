#nullable enable
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;
using PresetAsset = jp.kshoji.unity.vst3nativehost.VstPresetAsset;
using PresetBrowser = jp.kshoji.unity.vst3nativehost.VstPresetBrowser;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-preset-capture", Title = "VST3 / Preset Capture")]
        [Description(
            "Capture current plugin state into an existing VstPresetAsset. " +
            "assetPath: Assets/... in Editor or Resources path (e.g. Presets/MyPreset) at runtime.")]
        public string PresetCapture
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Asset or Resources path to VstPresetAsset.")]
            string assetPath,
            [Description("Optional display name override.")]
            string? displayName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-capture", pluginId, out var error))
                    return error!;

                var preset = VstPresetMcpLoader.Load(assetPath, out var loadErr);
                if (preset == null)
                    return loadErr!;

                if (!preset.CaptureFrom(pluginId, displayName))
                    return $"[Error] vst3-preset-capture failed pluginId={pluginId}";

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
#endif
                return
                    $"[Success] vst3-preset-capture path={assetPath} pluginId={pluginId} " +
                    $"bytes={preset.State.Length} displayName={preset.DisplayName}";
            });
        }

        [AiTool("vst3-preset-apply", Title = "VST3 / Preset Apply")]
        [Description(
            "Apply a VstPresetAsset state blob to a loaded plugin instance. " +
            "assetPath: Assets/... in Editor or Resources path at runtime.")]
        public string PresetApply
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Asset or Resources path to VstPresetAsset.")]
            string assetPath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-apply", pluginId, out var error))
                    return error!;

                var preset = VstPresetMcpLoader.Load(assetPath, out var loadErr);
                if (preset == null)
                    return loadErr!;
                if (!preset.HasState)
                    return $"[Error] vst3-preset-apply: asset has no state: {assetPath}";

                if (!preset.ApplyTo(pluginId))
                    return $"[Error] vst3-preset-apply failed pluginId={pluginId}";

                Activity.Raise(
                    VstHostActivityKind.State,
                    pluginId,
                    $"presetApply={preset.DisplayName}");
                return
                    $"[Success] vst3-preset-apply path={assetPath} pluginId={pluginId} " +
                    $"displayName={preset.DisplayName} bytes={preset.State.Length}";
            });
        }

        [AiTool(
            "vst3-preset-list",
            Title = "VST3 / Preset List",
            ReadOnlyHint = true)]
        [Description(
            "List VstPresetAsset from Resources (and project Assets in Editor). " +
            "Optionally list host programs for a pluginId.")]
        public string PresetList
        (
            [Description("Optional plugin id to also list host programs.")]
            int pluginId = 0,
            [Description("Max preset assets to list.")]
            int maxPresets = 100
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var entries = new System.Collections.Generic.List<(string path, PresetAsset preset)>();

#if UNITY_EDITOR
                var guids = AssetDatabase.FindAssets("t:VstPresetAsset");
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var preset = AssetDatabase.LoadAssetAtPath<PresetAsset>(path);
                    if (preset != null)
                        entries.Add((path, preset));
                }
#endif
                var resources = VstPresetMcpLoader.LoadAllFromResources();
                for (var i = 0; i < resources.Count; i++)
                {
                    var preset = resources[i];
                    var path = $"Resources/{preset.name}";
                    var duplicate = false;
                    for (var j = 0; j < entries.Count; j++)
                    {
                        if (ReferenceEquals(entries[j].preset, preset))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                        entries.Add((path, preset));
                }

                var sb = new StringBuilder();
                sb.Append($"[Success] vst3-preset-list assetCount={entries.Count}");
                var n = System.Math.Min(entries.Count, System.Math.Max(1, maxPresets));
                for (var i = 0; i < n; i++)
                {
                    var (path, preset) = entries[i];
                    sb.Append('\n');
                    sb.Append(
                        $"[{i}] path={path} name={preset.DisplayName} hasState={preset.HasState} " +
                        $"bytes={(preset.HasState ? preset.State.Length : 0)} uid={preset.PluginUid}");
                }

                if (entries.Count > n)
                    sb.Append($"\n… truncated at maxPresets={maxPresets}");

                if (pluginId >= 1 && Host.IsInitialized && Host.LoadedPlugins.ContainsKey(pluginId))
                {
                    var programs = Host.GetPrograms(pluginId);
                    sb.Append($"\nhostPrograms pluginId={pluginId} count={programs.Count}");
                    for (var i = 0; i < programs.Count; i++)
                        sb.Append($"\n  prog[{i}]={programs[i]}");
                }

                return sb.ToString();
            });
        }

        [AiTool("vst3-preset-ab", Title = "VST3 / Preset A/B")]
        [Description(
            "A/B capture/restore/toggle via VstPresetBrowser. " +
            "action: capture-a|capture-b|apply-a|apply-b|toggle. " +
            "Success includes slotABytes/slotBBytes/sha8/slotsEqual/preferSlotB/applied. " +
            "Param changes may lag in GetState until audio Process — flushProcessBeforeCapture " +
            "(default true on capture) runs one silent block. Play Mode / runtime recommended.")]
        public string PresetAb
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("capture-a, capture-b, apply-a, apply-b, or toggle.")]
            string action,
            [Description("GameObject name. Empty = __VstHostMcpAudio.")]
            string? gameObjectName = null,
            [Description("When true, show the floating IMGUI browser.")]
            bool showGui = false,
            [Description(
                "On capture-*: run one silent Process block before GetState so controller changes " +
                "reach the component state chunk. Default true.")]
            bool flushProcessBeforeCapture = true
        )
        {
            if (string.IsNullOrWhiteSpace(action))
                return "[Error] vst3-preset-ab: action is required.";

            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-ab", pluginId, out var error))
                    return error!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? VstHostToolHelpers.DefaultAudioObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var browser = go.GetComponent<PresetBrowser>();
                if (browser == null)
                    browser = go.AddComponent<PresetBrowser>();

                browser.PluginId = pluginId;
                browser.ShowGui = showGui;
                browser.RefreshPrograms();

                var act = action.Trim().ToLowerInvariant();
                var isCapture = act is "capture-a" or "capturea" or "a" or "capture-b" or "captureb" or "b";
                var flushed = false;
                if (isCapture && flushProcessBeforeCapture)
                    flushed = TryFlushProcessForState(pluginId);

                string? applied = null;
                var ok = act switch
                {
                    "capture-a" or "capturea" or "a" => browser.CaptureToSlotA(),
                    "capture-b" or "captureb" or "b" => browser.CaptureToSlotB(),
                    "apply-a" or "restore-a" or "restorea" => browser.ApplySlotA(),
                    "apply-b" or "restore-b" or "restoreb" => browser.ApplySlotB(),
                    "toggle" or "ab" => browser.ToggleAb(),
                    _ => false,
                };

                if (!ok && act is not ("capture-a" or "capturea" or "a" or "capture-b" or "captureb" or "b"
                    or "apply-a" or "restore-a" or "restorea" or "apply-b" or "restore-b" or "restoreb"
                    or "toggle" or "ab"))
                {
                    return
                        "[Error] vst3-preset-ab: action must be capture-a|capture-b|apply-a|apply-b|toggle.";
                }

                if (ok && act is "apply-a" or "restore-a" or "restorea" or "apply-b" or "restore-b"
                    or "restoreb" or "toggle" or "ab")
                {
                    applied = browser.PreferSlotB ? "B" : "A";
                    Activity.PumpMainThread();
                }

                if (!ok)
                {
                    return
                        $"[Error] vst3-preset-ab action={act} failed (empty slot or SetState error). " +
                        browser.FormatAbDiagnostics();
                }

                var warn = browser.SlotsEqual
                    ? " [Warning] slotsEqual=true — A and B are identical; wait after param-set " +
                      "(or rely on flushProcessBeforeCapture) before capture-b, then re-check sha8."
                    : string.Empty;

                return
                    $"[Success] vst3-preset-ab action={act} pluginId={pluginId} gameObject={name} " +
                    $"flushedProcess={flushed} applied={applied ?? "none"} " +
                    $"{browser.FormatAbDiagnostics()}{warn}";
            });
        }

        static bool TryFlushProcessForState(int pluginId)
        {
            var frames = Host.BlockSize;
            if (frames < 1)
                frames = 256;
            var outL = new float[frames];
            var outR = new float[frames];
            return Host.Process(pluginId, null, null, outL, outR, frames);
        }
    }
}
