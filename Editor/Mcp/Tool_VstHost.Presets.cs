#nullable enable
using System.ComponentModel;
using System.IO;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEditor;
using UnityEngine;
using PresetAsset = jp.kshoji.unity.vst3nativehost.VstPresetAsset;
using ParamPanel = jp.kshoji.unity.vst3nativehost.VstHostParameterPanel;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-preset-create-asset", Title = "VST3 / Preset Create Asset")]
        [Description(
            "Editor only — create or upsert a VstPresetAsset at Assets/... .asset. " +
            "Runtime preset apply uses Resources or pre-authored assets.")]
        public string PresetCreateAsset
        (
            [Description("Asset path e.g. Assets/Presets/MyPreset.asset")]
            string assetPath,
            [Description("Optional display name.")]
            string? displayName = null,
            [Description("If >0, CaptureFrom this plugin id after create/overwrite.")]
            int capturePluginId = 0,
            [Description("When true, reuse/overwrite an existing asset at the path.")]
            bool overwrite = false
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
                    EnsureAssetFolder(dir);
                }

                var existing = AssetDatabase.LoadAssetAtPath<PresetAsset>(path);
                PresetAsset preset;
                var reused = false;
                if (existing != null)
                {
                    if (!overwrite)
                        return $"[Error] vst3-preset-create-asset: asset already exists at {path} (set overwrite=true).";
                    preset = existing;
                    reused = true;
                }
                else
                {
                    preset = ScriptableObject.CreateInstance<PresetAsset>();
                    AssetDatabase.CreateAsset(preset, path);
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                    preset.DisplayName = displayName.Trim();

                if (capturePluginId >= 1)
                {
                    if (!TryRequireLoaded("vst3-preset-create-asset", capturePluginId, out var loadErr))
                    {
                        EditorUtility.SetDirty(preset);
                        AssetDatabase.SaveAssets();
                        return loadErr + $" (asset at {path} without capture reused={reused})";
                    }

                    if (!preset.CaptureFrom(capturePluginId, displayName))
                    {
                        EditorUtility.SetDirty(preset);
                        AssetDatabase.SaveAssets();
                        return $"[Error] asset at {path} but CaptureFrom failed for id={capturePluginId}";
                    }

                    EditorUtility.SetDirty(preset);
                }
                else if (reused)
                {
                    EditorUtility.SetDirty(preset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return
                    $"[Success] vst3-preset-create-asset path={path} hasState={preset.HasState} " +
                    $"displayName={preset.DisplayName} overwritten={reused}";
            });
        }

        [AiTool("vst3-parameter-panel-setup", Title = "VST3 / Parameter Panel Setup")]
        [Description(
            "Editor only — ensure VstHostParameterPanel for Play Mode IMGUI sliders / MIDI Learn assist. " +
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
                    ? VstHostToolHelpers.DefaultAudioObjectName
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
