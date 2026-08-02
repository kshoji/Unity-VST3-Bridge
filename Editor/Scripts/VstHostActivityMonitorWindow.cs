using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Live log of VST host MIDI sends and parameter / program changes.
    /// Complements the MIDI Plugin Monitor for host-side activity.
    /// </summary>
    internal sealed class VstHostActivityMonitorWindow : EditorWindow
    {
        private const int MaxLines = 500;
        private readonly List<string> lines = new List<string>();
        private Vector2 scroll;
        private bool paused;
        private bool dirty;

        [MenuItem("Window/VST3 Host/Activity Monitor")]
        private static void Open()
        {
            var window = GetWindow<VstHostActivityMonitorWindow>();
            window.titleContent = new GUIContent("VST Activity");
            window.minSize = new Vector2(420, 240);
            window.Show();
        }

        private void OnEnable()
        {
            VstHostActivity.Raised += OnActivity;
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            VstHostActivity.Raised -= OnActivity;
            EditorApplication.update -= OnUpdate;
        }

        private void OnActivity(VstHostActivityEntry entry)
        {
            if (paused)
                return;

            var line =
                $"{entry.TimeSeconds:0.000}  [{entry.Kind}]  plugin={entry.PluginId}  {entry.Detail}";
            lines.Add(line);
            while (lines.Count > MaxLines)
                lines.RemoveAt(0);
            dirty = true;
        }

        private void OnUpdate()
        {
            VstHostActivity.PumpMainThread();

            if (!dirty)
                return;
            dirty = false;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                lines.Clear();
            paused = GUILayout.Toggle(paused, paused ? "Resume" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(60));
            VstHostActivity.Enabled = GUILayout.Toggle(
                VstHostActivity.Enabled, "Capture", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{lines.Count} lines", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var i = 0; i < lines.Count; i++)
                EditorGUILayout.LabelField(lines[i], EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Shows VstHostManager MIDI / SetParameter / SetProgram traffic. " +
                "MIDI lines are deferred to the main thread (safe with device callbacks). " +
                "For device-level MIDI I/O use Window → MIDI → Monitor when the MIDI Plugin is present.",
                MessageType.Info);
        }
    }
}
