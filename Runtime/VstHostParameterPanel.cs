using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Minimal host-side parameter / program UI (IMGUI).
    /// Plugin-native GUI (IPlugView) is not supported.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostParameterPanel : MonoBehaviour
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] private bool showHidden;
        [SerializeField] private bool showPrograms = true;
        [SerializeField] private Rect windowRect = new Rect(20, 20, 360, 480);

        private readonly List<VstParamInfo> parameters = new List<VstParamInfo>();
        private readonly List<float> values = new List<float>();
        private readonly List<string> programs = new List<string>();
        private int programIndex;
        private Vector2 scroll;
        private bool windowVisible = true;
        private byte[] cachedState;

        public int PluginId
        {
            get => pluginId;
            set
            {
                if (pluginId == value) return;
                pluginId = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            if (pluginId >= 1)
                Refresh();
        }

        public void Refresh()
        {
            parameters.Clear();
            values.Clear();
            programs.Clear();
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;

            var list = VstHostManager.Instance.GetParameters(pluginId);
            foreach (var p in list)
            {
                if (!showHidden && (p.ParamFlags & VstParamFlags.IsHidden) != 0)
                    continue;
                parameters.Add(p);
                VstHostManager.Instance.TryGetParameterNormalized(pluginId, p.Id, out var v);
                values.Add((float)v);
            }

            programs.AddRange(VstHostManager.Instance.GetPrograms(pluginId));
            programIndex = Mathf.Clamp(programIndex, 0, Mathf.Max(0, programs.Count - 1));
        }

        private void OnGUI()
        {
            if (!windowVisible || pluginId < 1)
                return;

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "VST Host Parameters");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Plugin id={pluginId}");

            if (GUILayout.Button("Refresh"))
                Refresh();

            if (showPrograms && programs.Count > 0)
            {
                GUILayout.Label($"Program ({programs.Count})");
                var labels = programs.ToArray();
                var next = GUILayout.SelectionGrid(programIndex, labels, 1);
                if (next != programIndex)
                {
                    programIndex = next;
                    VstHostManager.Instance.SetProgram(pluginId, programIndex);
                    Refresh();
                }
            }

            scroll = GUILayout.BeginScrollView(scroll);
            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                var title = string.IsNullOrEmpty(p.Title) ? $"Param {p.Id}" : p.Title;
                if (!string.IsNullOrEmpty(p.Units))
                    title = $"{title} ({p.Units})";

                GUI.enabled = !p.IsReadOnly;
                if (p.StepCount == 1)
                {
                    var on = values[i] >= 0.5f;
                    var next = GUILayout.Toggle(on, title);
                    if (next != on)
                    {
                        values[i] = next ? 1f : 0f;
                        VstHostManager.Instance.SetParameterNormalized(pluginId, p.Id, values[i]);
                    }
                }
                else
                {
                    GUILayout.Label($"{title}: {values[i]:0.###}");
                    var next = GUILayout.HorizontalSlider(values[i], 0f, 1f);
                    if (!Mathf.Approximately(next, values[i]))
                    {
                        values[i] = next;
                        VstHostManager.Instance.SetParameterNormalized(pluginId, p.Id, values[i]);
                    }
                }
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save State"))
            {
                cachedState = VstHostManager.Instance.GetState(pluginId);
                Debug.Log(cachedState == null
                    ? "[VstHostParameterPanel] GetState failed"
                    : $"[VstHostParameterPanel] Saved state ({cachedState.Length} bytes)");
            }
            GUI.enabled = cachedState != null && cachedState.Length > 0;
            if (GUILayout.Button("Restore State"))
            {
                if (VstHostManager.Instance.SetState(pluginId, cachedState))
                    Refresh();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("Note: plugin-native GUI is not hosted.");
            GUI.DragWindow();
        }
    }
}
