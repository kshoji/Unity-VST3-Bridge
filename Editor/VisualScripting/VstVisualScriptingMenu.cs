#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.visualscripting.Editor
{
    /// <summary>Registers the VST3 Host Visual Scripting assembly in Project Settings.</summary>
    static class VstVisualScriptingMenu
    {
        const string AssemblyName = "jp.kshoji.unity.vst3nativehost.VisualScripting";
        const string SettingsPath = "Project/Visual Scripting";

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += EnsureAssemblyRegistered;
        }

        [MenuItem("Window/VST3 Host/Visual Scripting/Register Nodes")]
        static void RegisterNodes()
        {
            EnsureAssemblyRegistered();

            if (TryRegenerateNodeLibrary())
            {
                Debug.Log("[VST3 Host Visual Scripting] Node library regeneration requested.");
                return;
            }

            EditorUtility.DisplayDialog(
                "VST3 Host Visual Scripting",
                "Open Project Settings > Visual Scripting and confirm that '" + AssemblyName +
                "' is listed under Node Library, then click Regenerate Nodes.\n\n" +
                "Requires FEATURE_USE_VISUALSCRIPTING.",
                "Open Settings");
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        static void EnsureAssemblyRegistered()
        {
            var settingsType = Type.GetType(
                "Unity.VisualScripting.BoltCoreConfiguration, Unity.VisualScripting.Core.Editor");
            if (settingsType == null)
                return;

            var instanceProperty = settingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instanceProperty?.GetValue(null);
            if (instance == null)
                return;

            var assemblyOptionsProperty = settingsType.GetProperty("assemblyOptions");
            var assemblyOptions = assemblyOptionsProperty?.GetValue(instance) as System.Collections.IList;
            if (assemblyOptions == null)
                return;

            if (!assemblyOptions.Cast<string>().Any(name => name == AssemblyName))
            {
                assemblyOptions.Add(AssemblyName);
                settingsType.GetMethod("Save")?.Invoke(instance, null);
            }
        }

        static bool TryRegenerateNodeLibrary()
        {
            var generatorType = Type.GetType(
                "Unity.VisualScripting.NodeGenerator, Unity.VisualScripting.Flow.Editor");
            var generateMethod = generatorType?.GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
            if (generateMethod == null)
                return false;
            generateMethod.Invoke(null, null);
            return true;
        }
    }
}
#endif
