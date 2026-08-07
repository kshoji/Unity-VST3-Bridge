using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Minimal host-side parameter / program UI (IMGUI).
    /// Plugin-native GUI (IPlugView) is not supported and not planned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostParameterPanel : MonoBehaviour
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] private bool showHidden;
        [SerializeField] private bool showPrograms = true;
        [SerializeField] private Rect windowRect = new Rect(20, 20, 400, 560);
        [SerializeField] [Range(0.2f, 0.45f)] private float programAreaFraction = 0.3f;

        private static int nextGuiWindowId = 20001;
        private const float MinWindowWidth = 320f;
        private const float MinWindowHeight = 420f;
        private const float ResizeGrip = 18f;

        private readonly List<VstParamInfo> parameters = new List<VstParamInfo>();
        private readonly List<float> values = new List<float>();
        private readonly List<string> programs = new List<string>();
        private int programIndex;
        private Vector2 programScroll;
        private Vector2 scroll;
        private bool windowVisible = true;
        private bool resizing;
        private byte[] cachedState;
        private MonoBehaviour midiParameterMapper;
        private int guiWindowId;

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

        private void Awake()
        {
            guiWindowId = nextGuiWindowId++;
        }

        private void OnEnable()
        {
            CacheMapper();
            if (pluginId >= 1)
                Refresh();
        }

        private void CacheMapper()
        {
            // Optional MIDI Learn target (compiled only with FEATURE_MIDI_PLUGIN).
            var type = System.Type.GetType(
                "jp.kshoji.unity.vst3nativehost.VstHostMidiParameterMapper, jp.kshoji.unity.vst3nativehost.Midi");
            midiParameterMapper = type != null ? GetComponent(type) as MonoBehaviour : null;
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

        private void NotifyMapperTouched(uint parameterId)
        {
            if (midiParameterMapper == null)
                CacheMapper();
            if (midiParameterMapper == null)
                return;

            var method = midiParameterMapper.GetType().GetMethod("NotifyParameterTouched");
            method?.Invoke(midiParameterMapper, new object[] { parameterId });
        }

        private void OnGUI()
        {
            if (!windowVisible || pluginId < 1)
                return;

            windowRect.width = Mathf.Max(windowRect.width, MinWindowWidth);
            windowRect.height = Mathf.Max(windowRect.height, MinWindowHeight);
            windowRect = GUILayout.Window(guiWindowId, windowRect, DrawWindow, "VST Host Parameters");
        }

        private void DrawWindow(int id)
        {
            // Keep programs capped so the parameter list remains the primary working area.
            const float headerReserve = 54f;
            const float footerReserve = 88f;
            const float sectionLabel = 22f;
            var showProgramList = showPrograms && programs.Count > 0;
            var labelBudget = sectionLabel + (showProgramList ? sectionLabel : 0f);
            var scrollBudget = Mathf.Max(
                160f, windowRect.height - headerReserve - footerReserve - labelBudget);

            var programAreaHeight = 0f;
            if (showProgramList)
            {
                programAreaHeight = Mathf.Clamp(
                    scrollBudget * programAreaFraction,
                    72f,
                    Mathf.Min(160f, scrollBudget * 0.4f));
            }

            // Prefer parameters: shrink program area before letting params go tiny.
            var parameterAreaHeight = scrollBudget - programAreaHeight;
            const float preferredParamMin = 200f;
            if (showProgramList && parameterAreaHeight < preferredParamMin)
            {
                var shrink = Mathf.Min(
                    preferredParamMin - parameterAreaHeight,
                    Mathf.Max(0f, programAreaHeight - 72f));
                programAreaHeight -= shrink;
                parameterAreaHeight = scrollBudget - programAreaHeight;
            }

            parameterAreaHeight = Mathf.Max(140f, parameterAreaHeight);

            GUILayout.Label($"Plugin id={pluginId}");

            if (GUILayout.Button("Refresh"))
                Refresh();

            if (showProgramList)
            {
                GUILayout.Label($"Program ({programs.Count})");
                programScroll = GUILayout.BeginScrollView(
                    programScroll, GUILayout.Height(programAreaHeight));
                var next = GUILayout.SelectionGrid(programIndex, programs.ToArray(), 1);
                if (next != programIndex)
                {
                    programIndex = next;
                    VstHostManager.Instance.SetProgram(pluginId, programIndex);
                    Refresh();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Label($"Parameters ({parameters.Count})");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(parameterAreaHeight));
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
                    var toggled = GUILayout.Toggle(on, title);
                    if (toggled != on)
                    {
                        values[i] = toggled ? 1f : 0f;
                        VstHostManager.Instance.SetParameterNormalized(pluginId, p.Id, values[i]);
                        NotifyMapperTouched(p.Id);
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
                        NotifyMapperTouched(p.Id);
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

            GUILayout.Label("Note: plugin-native GUI is not hosted (not planned). Drag corner to resize.");
            HandleWindowResize();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void HandleWindowResize()
        {
            var gripRect = new Rect(
                windowRect.width - ResizeGrip,
                windowRect.height - ResizeGrip,
                ResizeGrip,
                ResizeGrip);
            GUI.Box(gripRect, "◢");

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when gripRect.Contains(e.mousePosition):
                    resizing = true;
                    e.Use();
                    break;
                case EventType.MouseDrag when resizing:
                    windowRect.width = Mathf.Max(MinWindowWidth, e.mousePosition.x + ResizeGrip * 0.5f);
                    windowRect.height = Mathf.Max(MinWindowHeight, e.mousePosition.y + ResizeGrip * 0.5f);
                    e.Use();
                    break;
                case EventType.MouseUp when resizing:
                    resizing = false;
                    e.Use();
                    break;
            }
        }
    }
}
