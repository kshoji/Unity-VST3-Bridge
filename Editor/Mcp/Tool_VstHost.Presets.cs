#nullable enable
using System.ComponentModel;
using System.IO;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;
using UnityEngine;
using PresetAsset = jp.kshoji.unity.vst3nativehost.VstPresetAsset;
using PresetBrowser = jp.kshoji.unity.vst3nativehost.VstPresetBrowser;
using ParamPanel = jp.kshoji.unity.vst3nativehost.VstHostParameterPanel;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-preset-create-asset", Title = "VST3 / Preset Create Asset")]
        [Description(
            "Create a VstPresetAsset at the given project path (Assets/... .asset). " +
            "Does not capture state unless capturePluginId is set.")]
        public string PresetCreateAsset
        (
            [Description("Asset path e.g. Assets/Presets/MyPreset.asset")]
            string assetPath,
            [Description("Optional display name.")]
            string? displayName = null,
            [Description("If >0, CaptureFrom this plugin id after create.")]
            int capturePluginId = 0
        )
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "[Error] vst3-preset-create-asset: assetPath is required.";

            return MainThread.Instance.Run(() =>
            {
                var path = assetPath.Trim().Replace('\\', '/');
                if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    return "[Error] vst3-preset-create-asset: path must start with Assets/.";
                if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                    path += ".asset";

                var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    // Create nested folders under Assets/
                    EnsureAssetFolder(dir);
                }

                var existing = AssetDatabase.LoadAssetAtPath<PresetAsset>(path);
                if (existing != null)
                    return $"[Error] vst3-preset-create-asset: asset already exists at {path}";

                var preset = ScriptableObject.CreateInstance<PresetAsset>();
                if (!string.IsNullOrWhiteSpace(displayName))
                    preset.DisplayName = displayName.Trim();

                AssetDatabase.CreateAsset(preset, path);

                if (capturePluginId >= 1)
                {
                    if (!TryRequireLoaded("vst3-preset-create-asset", capturePluginId, out var loadErr))
                    {
                        AssetDatabase.SaveAssets();
                        return loadErr + $" (asset created at {path} without capture)";
                    }

                    if (!preset.CaptureFrom(capturePluginId, displayName))
                    {
                        EditorUtility.SetDirty(preset);
                        AssetDatabase.SaveAssets();
                        return $"[Error] asset created at {path} but CaptureFrom failed for id={capturePluginId}";
                    }

                    EditorUtility.SetDirty(preset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return
                    $"[Success] vst3-preset-create-asset path={path} hasState={preset.HasState} " +
                    $"displayName={preset.DisplayName}";
            });
        }

        [AiTool("vst3-preset-capture", Title = "VST3 / Preset Capture")]
        [Description("Capture current plugin state into an existing VstPresetAsset (by asset path).")]
        public string PresetCapture
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Asset path to VstPresetAsset.")]
            string assetPath,
            [Description("Optional display name override.")]
            string? displayName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-capture", pluginId, out var error))
                    return error!;

                var preset = LoadPreset(assetPath, out var loadErr);
                if (preset == null)
                    return loadErr!;

                if (!preset.CaptureFrom(pluginId, displayName))
                    return $"[Error] vst3-preset-capture failed pluginId={pluginId}";

                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
                return
                    $"[Success] vst3-preset-capture path={assetPath} pluginId={pluginId} " +
                    $"bytes={preset.State.Length} displayName={preset.DisplayName}";
            });
        }

        [AiTool("vst3-preset-apply", Title = "VST3 / Preset Apply")]
        [Description("Apply a VstPresetAsset state blob to a loaded plugin instance.")]
        public string PresetApply
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Asset path to VstPresetAsset.")]
            string assetPath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-apply", pluginId, out var error))
                    return error!;

                var preset = LoadPreset(assetPath, out var loadErr);
                if (preset == null)
                    return loadErr!;
                if (!preset.HasState)
                    return $"[Error] vst3-preset-apply: asset has no state: {assetPath}";

                if (!preset.ApplyTo(pluginId))
                    return $"[Error] vst3-preset-apply failed pluginId={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.Raise(
                    jp.kshoji.unity.vst3nativehost.VstHostActivityKind.State,
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
            "List project VstPresetAsset assets and optionally host programs for a pluginId. " +
            "Edit Mode OK.")]
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
                var guids = AssetDatabase.FindAssets("t:VstPresetAsset");
                var sb = new StringBuilder();
                sb.Append($"[Success] vst3-preset-list assetCount={guids.Length}");
                var n = System.Math.Min(guids.Length, System.Math.Max(1, maxPresets));
                for (var i = 0; i < n; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var preset = AssetDatabase.LoadAssetAtPath<PresetAsset>(path);
                    sb.Append('\n');
                    if (preset == null)
                    {
                        sb.Append($"[{i}] path={path} (load failed)");
                        continue;
                    }

                    sb.Append(
                        $"[{i}] path={path} name={preset.DisplayName} hasState={preset.HasState} " +
                        $"bytes={(preset.HasState ? preset.State.Length : 0)} uid={preset.PluginUid}");
                }

                if (guids.Length > n)
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
            "A/B capture/restore/toggle via VstPresetBrowser on a GameObject. " +
            "action: capture-a|capture-b|apply-a|apply-b|toggle. Play Mode recommended.")]
        public string PresetAb
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("capture-a, capture-b, apply-a, apply-b, or toggle.")]
            string action,
            [Description("GameObject name. Empty = __VstHostMcpAudio.")]
            string? gameObjectName = null,
            [Description("When true, show the floating IMGUI browser.")]
            bool showGui = false
        )
        {
            if (string.IsNullOrWhiteSpace(action))
                return "[Error] vst3-preset-ab: action is required.";

            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-preset-ab", pluginId, out var error))
                    return error!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultAudioObjectName
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

                return ok
                    ? $"[Success] vst3-preset-ab action={act} pluginId={pluginId} gameObject={name}"
                    : $"[Error] vst3-preset-ab action={act} failed (empty slot or SetState error).";
            });
        }

        [AiTool("vst3-parameter-panel-setup", Title = "VST3 / Parameter Panel Setup")]
        [Description(
            "Ensure VstHostParameterPanel on a GameObject for Play Mode IMGUI sliders / MIDI Learn assist. " +
            "Play Mode recommended for visibility.")]
        public string ParameterPanelSetup
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("GameObject name. Empty = __VstHostMcpAudio.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-parameter-panel-setup", pluginId, out var error))
                    return error!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultAudioObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var panel = go.GetComponent<ParamPanel>();
                if (panel == null)
                    panel = go.AddComponent<ParamPanel>();
                panel.PluginId = pluginId;
                panel.Refresh();

                return
                    $"[Success] vst3-parameter-panel-setup gameObject={name} pluginId={pluginId} " +
                    $"playMode={IsPlayMode}";
            });
        }

        static PresetAsset? LoadPreset(string assetPath, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "[Error] assetPath is required.";
                return null;
            }

            var path = assetPath.Trim().Replace('\\', '/');
            var preset = AssetDatabase.LoadAssetAtPath<PresetAsset>(path);
            if (preset == null)
            {
                error = $"[Error] VstPresetAsset not found at '{path}'.";
                return null;
            }

            return preset;
        }

        static void EnsureAssetFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
