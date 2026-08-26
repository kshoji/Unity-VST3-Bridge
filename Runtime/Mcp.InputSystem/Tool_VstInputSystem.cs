#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.inputsystem;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.vst3nativehost.mcp.inputsystem
{
    [AiToolType]
    public class Tool_VstInputSystem
    {
        internal const string DefaultObjectName = "__VstHostMcpInput";

        [AiTool("vst3-inputsystem-bridge", Title = "VST3 / Input System Bridge")]
        [Description(
            "Ensure InputSystemToVstBridge + VstParameterTarget. " +
            "Assign InputActionAsset from scene reference, Resources name, or (Editor) Assets path. " +
            "Bindings are edited in Inspector.")]
        public string InputSystemBridge
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Optional Resources path to InputActionAsset (no extension).")]
            string? actionAssetResource = null,
#if UNITY_EDITOR
            [Description("Editor only: optional InputActionAsset path under Assets/.")]
            string? actionAssetPath = null,
#endif
            [Description("GameObject name. Empty = __VstHostMcpInput.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (pluginId < 1)
                    return "[Error] vst3-inputsystem-bridge: pluginId must be >= 1.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var target = go.GetComponent<VstParameterTarget>() ?? go.AddComponent<VstParameterTarget>();
                target.PluginId = pluginId;

                var bridge = go.GetComponent<InputSystemToVstBridge>()
                             ?? go.AddComponent<InputSystemToVstBridge>();
                bridge.Target = target;

                var assetMsg = "actionAsset=";
                InputActionAsset? asset = null;

#if UNITY_EDITOR
                if (!string.IsNullOrWhiteSpace(actionAssetPath))
                {
                    var path = actionAssetPath.Trim().Replace('\\', '/');
                    asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                    if (asset == null)
                        return $"[Error] InputActionAsset not found at '{path}'.";
                    assetMsg += path;
                }
#endif
                if (asset == null && !string.IsNullOrWhiteSpace(actionAssetResource))
                {
                    var resourcePath = actionAssetResource.Trim().Replace('\\', '/');
                    if (resourcePath.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring(0, resourcePath.Length - ".inputactions".Length);
                    const string resourcesPrefix = "Resources/";
                    if (resourcePath.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring(resourcesPrefix.Length);
                    asset = Resources.Load<InputActionAsset>(resourcePath);
                    if (asset == null)
                        return $"[Error] InputActionAsset not found in Resources at '{resourcePath}'.";
                    assetMsg += $"Resources/{resourcePath}";
                }

                if (asset != null)
                    bridge.ActionAsset = asset;
                else
                    assetMsg += bridge.ActionAsset != null ? bridge.ActionAsset.name : "(none)";

                return
                    $"[Success] vst3-inputsystem-bridge gameObject={go.name} pluginId={pluginId} " +
                    $"{assetMsg}. Configure InputToVstBinding[] in Inspector.";
            });
        }
    }
}
