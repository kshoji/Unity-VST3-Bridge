#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.inputsystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jp.kshoji.unity.vst3nativehost.mcp.inputsystem
{
    [AiToolType]
    public class Tool_VstInputSystem
    {
        const string DefaultObjectName = "__VstHostMcpInput";

        [AiTool("vst3-inputsystem-bridge", Title = "VST3 / Input System Bridge")]
        [Description(
            "Ensure InputSystemToVstBridge + VstParameterTarget. Assign InputActionAsset by path. " +
            "Bindings are edited in Inspector (tool wires asset + pluginId).")]
        public string InputSystemBridge
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Optional InputActionAsset path under Assets/.")]
            string? actionAssetPath = null,
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
                if (!string.IsNullOrWhiteSpace(actionAssetPath))
                {
                    var path = actionAssetPath.Trim().Replace('\\', '/');
                    var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                    if (asset == null)
                        return $"[Error] InputActionAsset not found at '{path}'.";
                    bridge.ActionAsset = asset;
                    assetMsg += path;
                }
                else
                {
                    assetMsg += bridge.ActionAsset != null ? bridge.ActionAsset.name : "(none)";
                }

                return
                    $"[Success] vst3-inputsystem-bridge gameObject={go.name} pluginId={pluginId} " +
                    $"{assetMsg}. Configure InputToVstBinding[] in Inspector.";
            });
        }
    }
}
