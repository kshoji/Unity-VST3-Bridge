using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Editor window to browse host programs and project <see cref="VstPresetAsset"/> files.
    /// </summary>
    internal sealed class VstPresetBrowserWindow : EditorWindow
    {
        private int pluginId = 1;
        private Vector2 scroll;
        private readonly List<string> programs = new List<string>();
        private readonly List<VstPresetAsset> presets = new List<VstPresetAsset>();
        private int programIndex;
        private string status = string.Empty;

        [MenuItem("Window/VST3 Host/Preset Browser")]
        private static void Open()
        {
            var window = GetWindow<VstPresetBrowserWindow>();
            window.titleContent = new GUIContent("VST Preset Browser");
            window.minSize = new Vector2(360, 320);
            window.RefreshPresets();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPresets();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Live plugin", EditorStyles.boldLabel);
            pluginId = EditorGUILayout.IntField("Plugin Id", pluginId);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !VstHostManager.Instance.IsInitialized || pluginId < 1))
            {
                if (GUILayout.Button("Refresh Host Programs"))
                    RefreshPrograms();

                if (programs.Count > 0)
                {
                    EditorGUILayout.LabelField($"Programs ({programs.Count})");
                    var next = GUILayout.SelectionGrid(programIndex, programs.ToArray(), 1);
                    if (next != programIndex)
                    {
                        programIndex = next;
                        if (VstHostManager.Instance.SetProgram(pluginId, programIndex))
                            status = $"Set program {programIndex}: {programs[programIndex]}";
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No programs loaded. Enter Play Mode, load a plugin, then Refresh.", MessageType.Info);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Project presets", EditorStyles.boldLabel);
            if (GUILayout.Button("Find VstPresetAsset in Project"))
                RefreshPresets();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var preset in presets)
            {
                if (preset == null)
                    continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(preset, typeof(VstPresetAsset), false);
                using (new EditorGUI.DisabledScope(
                           !Application.isPlaying || !preset.HasState || pluginId < 1 ||
                           !VstHostManager.Instance.IsInitialized))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    {
                        status = preset.ApplyTo(pluginId)
                            ? $"Applied '{preset.DisplayName}'"
                            : "Apply failed";
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !Application.isPlaying || pluginId < 1 || !VstHostManager.Instance.IsInitialized))
                {
                    if (GUILayout.Button("Capture", GUILayout.Width(70)))
                    {
                        if (preset.CaptureFrom(pluginId, preset.DisplayName))
                        {
                            EditorUtility.SetDirty(preset);
                            status = $"Captured into '{preset.DisplayName}' ({preset.State.Length} bytes)";
                        }
                        else
                        {
                            status = "Capture failed";
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.None);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Apply / Capture / SetProgram require Play Mode.", MessageType.Info);
        }

        private void RefreshPrograms()
        {
            programs.Clear();
            if (!Application.isPlaying || !VstHostManager.Instance.IsInitialized || pluginId < 1)
                return;

            programs.AddRange(VstHostManager.Instance.GetPrograms(pluginId));
            programIndex = Mathf.Clamp(programIndex, 0, Mathf.Max(0, programs.Count - 1));
            status = programs.Count == 0
                ? "Plugin exposes no program list."
                : $"Loaded {programs.Count} program(s).";
        }

        private void RefreshPresets()
        {
            presets.Clear();
            var guids = AssetDatabase.FindAssets("t:VstPresetAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<VstPresetAsset>(path);
                if (asset != null)
                    presets.Add(asset);
            }
        }
    }
}
