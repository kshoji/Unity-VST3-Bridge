using System;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>Project-wide VST host editor preferences (scan folders, defaults).</summary>
    [FilePath("ProjectSettings/VstHostSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class VstHostProjectSettings : ScriptableSingleton<VstHostProjectSettings>
    {
        [SerializeField] private string[] extraScanFolders = Array.Empty<string>();
        [SerializeField] private string preferredPluginNameContains = string.Empty;
        [SerializeField] private bool autoInitializeOnPlay = true;

        public string[] ExtraScanFolders
        {
            get => extraScanFolders ?? Array.Empty<string>();
            set => extraScanFolders = value ?? Array.Empty<string>();
        }

        public string PreferredPluginNameContains
        {
            get => preferredPluginNameContains ?? string.Empty;
            set => preferredPluginNameContains = value ?? string.Empty;
        }

        public bool AutoInitializeOnPlay
        {
            get => autoInitializeOnPlay;
            set => autoInitializeOnPlay = value;
        }

        public void Save() => Save(true);
    }

    static class VstHostSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            var provider = new SettingsProvider("Project/VST3 Host", SettingsScope.Project)
            {
                label = "VST3 Host",
                guiHandler = _ =>
                {
                    var settings = VstHostProjectSettings.instance;
                    EditorGUI.BeginChangeCheck();

                    settings.AutoInitializeOnPlay = EditorGUILayout.Toggle(
                        new GUIContent("Auto Initialize On Play", "Call InitializeFromAudioSettings when entering Play Mode if not already initialized."),
                        settings.AutoInitializeOnPlay);

                    settings.PreferredPluginNameContains = EditorGUILayout.TextField(
                        new GUIContent("Preferred Plugin Filter", "Substring used by the plugin browser auto-select / sample helpers."),
                        settings.PreferredPluginNameContains);

                    EditorGUILayout.LabelField("Extra Scan Folders", EditorStyles.boldLabel);
                    var folders = settings.ExtraScanFolders;
                    var newCount = Mathf.Max(0, EditorGUILayout.IntField("Count", folders.Length));
                    if (newCount != folders.Length)
                        Array.Resize(ref folders, newCount);

                    for (var i = 0; i < folders.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        folders[i] = EditorGUILayout.TextField($"Folder {i}", folders[i] ?? string.Empty);
                        if (GUILayout.Button("...", GUILayout.Width(30)))
                        {
                            var picked = EditorUtility.OpenFolderPanel("VST3 Scan Folder", folders[i], string.Empty);
                            if (!string.IsNullOrEmpty(picked))
                                folders[i] = picked;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    settings.ExtraScanFolders = folders;

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Export Runtime MCP Config to Resources…"))
                    {
                        EditorApplication.ExecuteMenuItem(
                            "Window/VST3 Host/Export Runtime MCP Config to Resources");
                    }
                    EditorGUILayout.HelpBox(
                        "Standalone builds read Resources/VstHostRuntimeMcpConfig.asset " +
                        "(not Project Settings). Export copies scan folders and auto-init; set MCP host/token on the asset.",
                        MessageType.Info);

                    if (EditorGUI.EndChangeCheck())
                        settings.Save();
                },
                keywords = new[] { "VST3", "VST", "scan", "plugin" }
            };
            return provider;
        }
    }

    [InitializeOnLoad]
    static class VstHostPlayModeAutoInit
    {
        static VstHostPlayModeAutoInit()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode)
                    return;
                if (!VstHostProjectSettings.instance.AutoInitializeOnPlay)
                    return;
                if (VstHostManager.Instance.IsInitialized)
                    return;
                VstHostManager.Instance.InitializeFromAudioSettings();
            };
        }
    }
}
