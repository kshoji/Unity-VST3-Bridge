using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Play Mode keyboard / CC panel that sends MIDI directly to a loaded VST instance
    /// via <see cref="VstHostManager"/> (no MIDI Plugin required).
    /// Select a plugin loaded by Plugin Browser, sample scene, or your own CreateInstance.
    /// </summary>
    internal sealed class VstVirtualControllerWindow : EditorWindow
    {
        private int pluginId = -1;
        private int channel;
        private int velocity = 100;
        private readonly HashSet<int> pressed = new HashSet<int>();
        private int ccNumber = 74;
        private float ccNormalized = 0.5f;
        private string status = string.Empty;

        private static readonly string[] WhiteKeys =
        {
            "C", "D", "E", "F", "G", "A", "B"
        };

        [MenuItem("Window/VST3 Host/Virtual Controller")]
        private static void Open()
        {
            var window = GetWindow<VstVirtualControllerWindow>();
            window.titleContent = new GUIContent("VST Controller");
            window.minSize = new Vector2(420, 260);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ReleaseAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                pressed.Clear();
                pluginId = -1;
                VstHostEditorPreview.Clear();
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Available in Play Mode only. Load a plugin first (Window → VST3 Host → Plugin Browser → Load Selected), " +
                    "or CreateInstance in your scene, then select that Plugin Id below.",
                    MessageType.Info);
                return;
            }

            if (!VstHostManager.Instance.IsInitialized)
            {
                EditorGUILayout.HelpBox("VstHostManager is not initialized.", MessageType.Warning);
                return;
            }

            DrawTargetPicker();
            channel = EditorGUILayout.IntSlider("Channel", channel, 0, 15);
            velocity = EditorGUILayout.IntSlider("Velocity", velocity, 1, 127);

            var ready = pluginId >= 1 && VstHostManager.Instance.LoadedPlugins.ContainsKey(pluginId);
            using (new EditorGUI.DisabledScope(!ready))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Keyboard (hold to sustain)", EditorStyles.boldLabel);
                DrawOctave(48);
                DrawOctave(60);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Control Change", EditorStyles.boldLabel);
                ccNumber = EditorGUILayout.IntSlider("CC Number", ccNumber, 0, 127);
                var next = EditorGUILayout.Slider("CC Value", ccNormalized, 0f, 1f);
                if (!Mathf.Approximately(next, ccNormalized))
                {
                    ccNormalized = next;
                    VstHostManager.Instance.ControlChange(
                        pluginId, channel, ccNumber, Mathf.RoundToInt(ccNormalized * 127f));
                    status = $"CC {ccNumber} → plugin {pluginId}";
                }

                if (GUILayout.Button("All Notes Off (panic)"))
                    ReleaseAll();
            }

            if (!ready)
            {
                EditorGUILayout.HelpBox(
                    "No loaded plugins. Use Window → VST3 Host → Plugin Browser → Load Selected, " +
                    "then choose the target in this window.",
                    MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }

        private void DrawTargetPicker()
        {
            var loaded = VstHostManager.Instance.LoadedPlugins;
            var ids = loaded.Keys.OrderBy(id => id).ToList();

            if (ids.Count == 0)
            {
                pluginId = -1;
                EditorGUILayout.LabelField("Target Plugin", "(none loaded)");
                return;
            }

            // Prefer remembered browser preview, else keep current, else first.
            if (pluginId < 1 || !loaded.ContainsKey(pluginId))
            {
                if (VstHostEditorPreview.LastPluginId >= 1 &&
                    loaded.ContainsKey(VstHostEditorPreview.LastPluginId))
                    pluginId = VstHostEditorPreview.LastPluginId;
                else
                    pluginId = ids[0];
            }

            var labels = ids.Select(id =>
            {
                var info = loaded[id];
                var name = System.IO.Path.GetFileNameWithoutExtension(info.FilePath);
                if (string.IsNullOrEmpty(name))
                    name = info.FilePath;
                return $"id={id}  {name}";
            }).ToArray();

            var index = Mathf.Max(0, ids.IndexOf(pluginId));
            var next = EditorGUILayout.Popup("Target Plugin", index, labels);
            pluginId = ids[Mathf.Clamp(next, 0, ids.Count - 1)];

            if (GUILayout.Button("Ensure Audio Output (VstHostAudioFilter)"))
            {
                VstHostEditorPreview.EnsureAudio(pluginId);
                status = $"Audio filter attached to plugin {pluginId}.";
            }
            else
            {
                // Keep audio routed while interacting.
                VstHostEditorPreview.EnsureAudio(pluginId);
            }
        }

        private void DrawOctave(int baseNote)
        {
            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < 7; i++)
            {
                var note = baseNote + WhiteNoteOffset(i);
                // RepeatButton stays true while the mouse is held — avoids one-frame NoteOn/Off.
                if (GUILayout.RepeatButton(WhiteKeys[i]))
                {
                    if (pressed.Add(note))
                    {
                        VstHostEditorPreview.EnsureAudio(pluginId);
                        VstHostManager.Instance.NoteOn(pluginId, channel, note, velocity);
                        status = $"NoteOn {note} → plugin {pluginId}";
                    }
                }
                else if (pressed.Contains(note))
                {
                    NoteOff(note);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static int WhiteNoteOffset(int whiteIndex)
        {
            switch (whiteIndex)
            {
                case 0: return 0;
                case 1: return 2;
                case 2: return 4;
                case 3: return 5;
                case 4: return 7;
                case 5: return 9;
                default: return 11;
            }
        }

        private void NoteOff(int note)
        {
            if (!pressed.Remove(note))
                return;
            if (pluginId >= 1 && VstHostManager.Instance.IsInitialized)
                VstHostManager.Instance.NoteOff(pluginId, channel, note, 0);
        }

        private void ReleaseAll()
        {
            if (pluginId >= 1 && VstHostManager.Instance.IsInitialized)
            {
                foreach (var note in pressed)
                    VstHostManager.Instance.NoteOff(pluginId, channel, note, 0);
            }

            pressed.Clear();
            status = "All notes off.";
        }
    }
}
