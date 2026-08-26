#nullable enable
using System.ComponentModel;
using System.IO;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        internal const string DefaultAnimatorObjectName = "__VstHostMcpAnimator";

        [AiTool("vst3-animator-setup", Title = "VST3 / Animator Setup")]
        [Description(
            "Editor only — ensure VstAnimatorDriver + VstParameterTarget. " +
            "Optionally create/assign VstAnimatorMapping asset. Requires Animator.")]
        public string AnimatorSetup
        (
            [Description("Target plugin instance id.")]
            int pluginId,
            [Description("Optional mapping asset path Assets/... .asset (created if missing).")]
            string? mappingAssetPath = null,
            [Description("GameObject name. Empty = __VstHostMcpAnimator.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (pluginId < 1)
                    return "[Error] vst3-animator-setup: pluginId must be >= 1.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultAnimatorObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                {
                    go = new GameObject(name);
                    go.hideFlags = HideFlags.DontSave;
                }

                var target = go.GetComponent<VstParameterTarget>();
                if (target == null)
                    target = go.AddComponent<VstParameterTarget>();
                target.PluginId = pluginId;

                if (go.GetComponent<Animator>() == null)
                    go.AddComponent<Animator>();

                var driver = go.GetComponent<VstAnimatorDriver>();
                if (driver == null)
                    driver = go.AddComponent<VstAnimatorDriver>();
                driver.Target = target;
                driver.Animator = go.GetComponent<Animator>();

                var mapMsg = string.Empty;
                if (!string.IsNullOrWhiteSpace(mappingAssetPath))
                {
                    var path = mappingAssetPath.Trim().Replace('\\', '/');
                    if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                        return "[Error] mappingAssetPath must start with Assets/.";
                    if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                        path += ".asset";

                    var mapping = AssetDatabase.LoadAssetAtPath<VstAnimatorMapping>(path);
                    if (mapping == null)
                    {
                        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                            EnsureAssetFolder(dir!);
                        mapping = ScriptableObject.CreateInstance<VstAnimatorMapping>();
                        AssetDatabase.CreateAsset(mapping, path);
                        AssetDatabase.SaveAssets();
                    }

                    var so = new SerializedObject(driver);
                    so.FindProperty("mapping").objectReferenceValue = mapping;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    mapMsg = $" mapping={path}";
                }

                return
                    $"[Success] vst3-animator-setup gameObject={go.name} pluginId={pluginId}" +
                    $"{mapMsg} (edit mapping bindings in Inspector / asset)";
            });
        }

        [AiTool("vst3-vs-register", Title = "VST3 / Visual Scripting Register")]
        [Description(
            "Editor only — register Visual Scripting nodes (FEATURE_USE_VISUALSCRIPTING). " +
            "Not available in Standalone builds.")]
        public string VisualScriptingRegister()
        {
            return MainThread.Instance.Run(() =>
            {
                if (!HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.VisualScripting")
                    && !HasScriptingDefine("FEATURE_USE_VISUALSCRIPTING"))
                {
                    return
                        "[Error] vst3-vs-register: Visual Scripting package / FEATURE_USE_VISUALSCRIPTING " +
                        "not available. Check vst3-features-status.";
                }

                const string assemblyName = "jp.kshoji.unity.vst3nativehost.VisualScripting";
                var settingsType = System.Type.GetType(
                    "Unity.VisualScripting.BoltCoreConfiguration, Unity.VisualScripting.Core.Editor");
                if (settingsType == null)
                    return "[Error] vst3-vs-register: BoltCoreConfiguration not found.";

                var instanceProperty = settingsType.GetProperty(
                    "instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProperty?.GetValue(null);
                if (instance == null)
                    return "[Error] vst3-vs-register: VS settings instance null.";

                var assemblyOptionsProperty = settingsType.GetProperty("assemblyOptions");
                var assemblyOptions = assemblyOptionsProperty?.GetValue(instance) as System.Collections.IList;
                if (assemblyOptions == null)
                    return "[Error] vst3-vs-register: assemblyOptions missing.";

                var added = false;
                var found = false;
                foreach (var item in assemblyOptions)
                {
                    if (item is string s && s == assemblyName)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    assemblyOptions.Add(assemblyName);
                    settingsType.GetMethod("Save")?.Invoke(instance, null);
                    added = true;
                }

                var regenerated = false;
                var generatorType = System.Type.GetType(
                    "Unity.VisualScripting.NodeGenerator, Unity.VisualScripting.Flow.Editor");
                var generateMethod = generatorType?.GetMethod(
                    "Generate",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (generateMethod != null)
                {
                    generateMethod.Invoke(null, null);
                    regenerated = true;
                }

                return
                    $"[Success] vst3-vs-register assembly={assemblyName} added={added} " +
                    $"alreadyListed={found} regenerated={regenerated}";
            });
        }
    }
}
