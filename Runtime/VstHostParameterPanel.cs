using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Minimal host-side parameter / program UI (IMGUI).
    /// Plugin-native GUI (IPlugView) is not supported and not planned.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class VstHostParameterPanel : MonoBehaviour
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] private bool showHidden;
        [SerializeField] private bool showPrograms = true;
        [SerializeField] private Rect windowRect = new Rect(20, 20, 380, 480);
        [SerializeField] private float panelWidth = 380f;
        [SerializeField] private float panelHeight = 480f;
        [SerializeField] [Range(0.15f, 0.4f)] private float programAreaFraction = 0.28f;

        private static int nextGuiWindowId = 20001;
        private const float MinWindowWidth = 300f;
        private const float MinWindowHeight = 280f;
        private const float MaxHeightScreenFraction = 0.92f;
        private const float ResizeGrip = 28f;
        private const float TitleDragHeight = 24f;
        private const float SizeStep = 56f;

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
            if (panelWidth < MinWindowWidth)
                panelWidth = windowRect.width > MinWindowWidth ? windowRect.width : 380f;
            if (panelHeight < MinWindowHeight)
                panelHeight = windowRect.height > MinWindowHeight ? windowRect.height : 480f;
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

            var e = Event.current;

            // Delta works in root OnGUI even when the cursor leaves the window.
            if (resizing && e.rawType == EventType.MouseDrag)
            {
                panelWidth += e.delta.x;
                panelHeight += e.delta.y;
                ClampPanelSize();
                e.Use();
            }

            if (e.rawType == EventType.MouseUp)
                resizing = false;

            ClampPanelSize();
            windowRect.width = panelWidth;
            windowRect.height = panelHeight;

            windowRect = GUILayout.Window(
                guiWindowId,
                windowRect,
                DrawWindow,
                "VST Host Parameters",
                GUILayout.Width(panelWidth),
                GUILayout.Height(panelHeight));

            // Position from Window; size stays owned by panelWidth/Height.
            windowRect.width = panelWidth;
            windowRect.height = panelHeight;
            ClampWindowPosition();

            // Empty client areas do not Use() the event by themselves; without this,
            // later OnGUI (sample panels) still see MouseDown and fire covered buttons.
            ConsumePointerEventsOverWindow();
        }

        private void ConsumePointerEventsOverWindow()
        {
            var e = Event.current;
            if (e.type == EventType.Used)
                return;

            var block = e.isMouse || e.type == EventType.ScrollWheel;
            if (!block)
                return;

            if (!windowRect.Contains(e.mousePosition))
                return;

            if (e.type == EventType.MouseDown && e.button == 0)
                GUI.BringWindowToFront(guiWindowId);

            e.Use();
        }

        private void ClampPanelSize()
        {
            var maxW = Mathf.Max(MinWindowWidth, Screen.width - 24f);
            var maxH = Mathf.Max(MinWindowHeight, Screen.height * MaxHeightScreenFraction);
            panelWidth = Mathf.Clamp(panelWidth, MinWindowWidth, maxW);
            panelHeight = Mathf.Clamp(panelHeight, MinWindowHeight, maxH);
        }

        private void ClampWindowPosition()
        {
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - 48f));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - 48f));
        }

        private void DrawWindow(int id)
        {
            const float headerReserve = 52f;
            const float footerReserve = 92f;
            const float sectionLabel = 20f;
            var showProgramList = showPrograms && programs.Count > 0;
            var labelBudget = sectionLabel + (showProgramList ? sectionLabel : 0f);
            var scrollBudget = Mathf.Max(
                80f, panelHeight - headerReserve - footerReserve - labelBudget);

            var programAreaHeight = 0f;
            if (showProgramList)
            {
                programAreaHeight = Mathf.Clamp(
                    scrollBudget * programAreaFraction,
                    56f,
                    Mathf.Min(120f, scrollBudget * 0.35f));
            }

            var parameterAreaHeight = Mathf.Max(64f, scrollBudget - programAreaHeight);

            GUILayout.Label($"Plugin id={pluginId}");

            if (GUILayout.Button("Refresh", GUILayout.Height(22f)))
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
            if (GUILayout.Button("Save State", GUILayout.Height(22f)))
            {
                cachedState = VstHostManager.Instance.GetState(pluginId);
                Debug.Log(cachedState == null
                    ? "[VstHostParameterPanel] GetState failed"
                    : $"[VstHostParameterPanel] Saved state ({cachedState.Length} bytes)");
            }
            GUI.enabled = cachedState != null && cachedState.Length > 0;
            if (GUILayout.Button("Restore State", GUILayout.Height(22f)))
            {
                if (VstHostManager.Instance.SetState(pluginId, cachedState))
                    Refresh();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−", GUILayout.Width(28f), GUILayout.Height(ResizeGrip)))
            {
                panelWidth -= SizeStep;
                panelHeight -= SizeStep;
                ClampPanelSize();
            }

            if (GUILayout.Button("+", GUILayout.Width(28f), GUILayout.Height(ResizeGrip)))
            {
                panelWidth += SizeStep;
                panelHeight += SizeStep;
                ClampPanelSize();
            }

            GUILayout.Label("Size", GUILayout.Width(36f));
            GUILayout.FlexibleSpace();

            var gripRect = GUILayoutUtility.GetRect(
                ResizeGrip, ResizeGrip, GUILayout.Width(ResizeGrip), GUILayout.Height(ResizeGrip));
            GUI.Box(gripRect, "◢");

            // Start drag-resize on press; movement is applied in OnGUI via Event.delta.
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && gripRect.Contains(ev.mousePosition))
            {
                resizing = true;
                ev.Use();
            }

            GUILayout.EndHorizontal();

            if (!resizing)
                GUI.DragWindow(new Rect(0, 0, 10000, TitleDragHeight));
        }
    }
}
