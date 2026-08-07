using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.sample
{
    /// <summary>
    /// Extra Play Mode demos that work with the VST package alone
    /// (Presets A/B, CC→parameter simulation, multi-timbral channel routes, EventSink).
    /// Attach next to <see cref="VstHostSampleController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostSampleFeatureDemos : MonoBehaviour
    {
        private enum DemoTab
        {
            Guide = 0,
            Presets = 1,
            Mapping = 2,
            Routes = 3,
        }

        [SerializeField] private VstHostSampleController sample;
        [SerializeField] private bool showGui = true;
        [SerializeField] private VstMidiParameterMapping mappingAsset;

        private DemoTab tab = DemoTab.Guide;
        private VstPresetBrowser presetBrowser;
        private VstHostEventSink eventSink;
        private VstAudioGraph audioGraph;
        private VstHostParameterPanel parameterPanel;
        private float guiScale = 1f;
        private Vector2 scroll;
        private int demoCc = 1;
        private float demoCcValue = 0.5f;
        private float demoPitchBend = 0.5f;
        private uint mappedParameterId;
        private int routeChannel;
        private int routeNodeId;
        private string status = string.Empty;
        private readonly List<VstParamInfo> parameters = new List<VstParamInfo>();

        private void Awake()
        {
            guiScale = Screen.width > Screen.height ? Screen.width / 1024f : Screen.height / 1024f;
            if (sample == null)
                sample = GetComponent<VstHostSampleController>();

            presetBrowser = GetComponent<VstPresetBrowser>();
            if (presetBrowser == null)
                presetBrowser = gameObject.AddComponent<VstPresetBrowser>();
            // Main sample already draws status; keep preset IMGUI off and drive via API.
            presetBrowser.ShowGui = false;

            eventSink = GetComponent<VstHostEventSink>();
            if (eventSink == null)
                eventSink = gameObject.AddComponent<VstHostEventSink>();

            audioGraph = GetComponent<VstAudioGraph>();
            parameterPanel = GetComponent<VstHostParameterPanel>();
        }

        private void Update()
        {
            var id = sample != null ? sample.CurrentPluginId : -1;
            if (presetBrowser != null && presetBrowser.PluginId != id)
                presetBrowser.PluginId = id;
            if (eventSink != null)
                eventSink.PluginId = id;
        }

        private void OnGUI()
        {
            if (!showGui) return;

            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
            var x = 510f;
            GUILayout.BeginArea(new Rect(x, 12, 420f, Screen.height / guiScale - 24));
            GUILayout.BeginVertical("box");
            GUILayout.Label("Feature demos (standalone)");
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tab == DemoTab.Guide, "Guide", "Button"))
                tab = DemoTab.Guide;
            if (GUILayout.Toggle(tab == DemoTab.Presets, "Presets", "Button"))
                tab = DemoTab.Presets;
            if (GUILayout.Toggle(tab == DemoTab.Mapping, "Mapping", "Button"))
                tab = DemoTab.Mapping;
            if (GUILayout.Toggle(tab == DemoTab.Routes, "Routes", "Button"))
                tab = DemoTab.Routes;
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case DemoTab.Guide:
                    DrawGuide();
                    break;
                case DemoTab.Presets:
                    DrawPresets();
                    break;
                case DemoTab.Mapping:
                    DrawMapping();
                    break;
                case DemoTab.Routes:
                    DrawRoutes();
                    break;
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.matrix = prev;
        }

        private void DrawGuide()
        {
            var midi = sample != null && sample.MidiAssemblyAvailable;
            GUILayout.Label("This panel needs only the VST package.");
            GUILayout.Label(midi
                ? "MIDI assembly detected — Adapter / SMF / Network / Chunity helpers can compile."
                : "MIDI assembly not present — use Note On on the left, Virtual Controller, or EventSink.");

            GUILayout.Space(6);
            GUILayout.Label("Also try Editor menus (Window → VST3 Host):");
            GUILayout.Label("• Plugin Browser (category + vendor/tag)");
            GUILayout.Label("• Activity Monitor (host MIDI / params)");
            GUILayout.Label("• Audio Graph (read-only topology)");
            GUILayout.Label("• Virtual Controller / Preset Browser");
            GUILayout.Label("• Project Settings → VST3 Host");

            GUILayout.Space(6);
            GUILayout.Label("Optional packages (auto via versionDefines):");
            GUILayout.Label("• Timeline → FEATURE_USE_TIMELINE (VstParameterTrack)");
            GUILayout.Label("• Input System → FEATURE_INPUT_SYSTEM (InputSystemToVstBridge)");
            GUILayout.Label("• Visual Scripting → define FEATURE_USE_VISUALSCRIPTING");

            GUILayout.Space(6);
            GUILayout.Label("Left panel paths: Single | Audio Graph.");
            GUILayout.Label("Graph demos: Parallel→Serial, Send/Return, Sidechain (AGain SideChain).");
            GUILayout.Label("DSP MIDI flush is on by default for Audio Graph.");

            GUILayout.Space(6);
            GUILayout.Label("EventSink quick test");
            var id = sample != null ? sample.CurrentPluginId : -1;
            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = id >= 1;
                if (GUILayout.Button("Sink NoteOn 60"))
                    eventSink.NoteOn(60);
                if (GUILayout.Button("Sink NoteOff 60"))
                    eventSink.NoteOff(60);
                GUI.enabled = true;
            }
        }

        private void DrawPresets()
        {
            var id = sample != null ? sample.CurrentPluginId : -1;
            if (id < 1)
            {
                GUILayout.Label("Load an instrument on the left panel first.");
                return;
            }

            GUILayout.Label($"Plugin id={id}");
            if (GUILayout.Button("Refresh host programs"))
            {
                presetBrowser.RefreshPrograms();
                status = "Programs refreshed";
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture A"))
                {
                    presetBrowser.CaptureToSlotA();
                    status = "Captured A";
                }

                if (GUILayout.Button("Capture B"))
                {
                    presetBrowser.CaptureToSlotB();
                    status = "Captured B";
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply A"))
                {
                    presetBrowser.ApplySlotA();
                    status = "Applied A";
                }

                if (GUILayout.Button("Apply B"))
                {
                    presetBrowser.ApplySlotB();
                    status = "Applied B";
                }

                if (GUILayout.Button("Toggle A/B"))
                {
                    presetBrowser.ToggleAb();
                    status = "Toggled A/B";
                }
            }

            GUILayout.Label("Host programs (SetProgram)");
            var programs = VstHostManager.Instance.GetPrograms(id);
            for (var i = 0; i < programs.Count; i++)
            {
                if (GUILayout.Button($"{i}: {programs[i]}"))
                {
                    VstHostManager.Instance.SetProgram(id, i);
                    status = $"SetProgram {i}";
                }
            }
        }

        private void DrawMapping()
        {
            var id = sample != null ? sample.CurrentPluginId : -1;
            if (id < 1)
            {
                GUILayout.Label("Load an instrument on the left panel first.");
                return;
            }

            EnsureParameterList(id);
            GUILayout.Label("Simulate CC / Pitch Bend → VST parameter (no MIDI required).");
            GUILayout.Label("With MIDI Plugin, use VstHostMidiParameterMapper + Learn instead.");

            if (parameters.Count == 0)
            {
                GUILayout.Label("No parameters on this plugin.");
                return;
            }

            // Pick first automatable parameter by default
            if (mappedParameterId == 0 && parameters.Count > 0)
                mappedParameterId = parameters[0].Id;

            GUILayout.Label($"Target parameter id={mappedParameterId}");
            for (var i = 0; i < Mathf.Min(parameters.Count, 12); i++)
            {
                var p = parameters[i];
                if (GUILayout.Toggle(mappedParameterId == p.Id, $"{p.Id}: {p.Title}", "Button"))
                    mappedParameterId = p.Id;
            }

            demoCc = Mathf.RoundToInt(GUILayout.HorizontalSlider(demoCc, 0, 127));
            GUILayout.Label($"CC number {demoCc}");
            var newCc = GUILayout.HorizontalSlider(demoCcValue, 0f, 1f);
            if (!Mathf.Approximately(newCc, demoCcValue))
            {
                demoCcValue = newCc;
                ApplySimulatedCc(id);
            }

            var newPb = GUILayout.HorizontalSlider(demoPitchBend, 0f, 1f);
            if (!Mathf.Approximately(newPb, demoPitchBend))
            {
                demoPitchBend = newPb;
                var amount = Mathf.RoundToInt(demoPitchBend * 16383f);
                VstHostManager.Instance.PitchBend(id, 0, amount);
                // Also map pitch bend to selected parameter for demo visibility
                VstHostManager.Instance.SetParameterNormalized(id, mappedParameterId, demoPitchBend);
                status = $"PitchBend {amount} + param {mappedParameterId}={demoPitchBend:0.00}";
            }

            if (GUILayout.Button("Bind CC→param into Mapping asset (optional)"))
            {
                if (mappingAsset == null)
                {
                    mappingAsset = ScriptableObject.CreateInstance<VstMidiParameterMapping>();
                    mappingAsset.name = "RuntimeMapping";
                }

                mappingAsset.UpsertControlChangeBinding(-1, demoCc, mappedParameterId);
                mappingAsset.UpsertPitchBendBinding(-1, mappedParameterId);
                status = $"Upserted CC {demoCc} / PitchBend → {mappedParameterId}";
            }

            if (parameterPanel != null)
                parameterPanel.Refresh();
        }

        private void ApplySimulatedCc(int pluginId)
        {
            var midiValue = Mathf.RoundToInt(demoCcValue * 127f);
            VstHostManager.Instance.ControlChange(pluginId, 0, demoCc, midiValue);

            if (mappingAsset != null && mappingAsset.Bindings != null)
            {
                foreach (var b in mappingAsset.Bindings)
                {
                    if (b.source != VstMidiParameterMapping.SourceType.ControlChange)
                        continue;
                    if (b.controller != demoCc)
                        continue;
                    var n = b.MapNormalized(demoCcValue);
                    VstHostManager.Instance.SetParameterNormalized(pluginId, b.parameterId, n);
                    status = $"CC {demoCc}={midiValue} → param {b.parameterId}={n:0.00}";
                    return;
                }
            }

            VstHostManager.Instance.SetParameterNormalized(pluginId, mappedParameterId, demoCcValue);
            status = $"CC {demoCc}={midiValue} → param {mappedParameterId}={demoCcValue:0.00}";
        }

        private void DrawRoutes()
        {
            if (audioGraph == null)
                audioGraph = GetComponent<VstAudioGraph>();

            var useGraph = sample != null && sample.IsGraphMode && audioGraph != null && audioGraph.enabled;
            if (!useGraph)
            {
                GUILayout.Label("Graph channel → instrument node id (multi-timbral helper).");
                GUILayout.Label("With MIDI: add VstHostChannelRouteSync to copy routes to Adapter.");
                GUILayout.Label("Switch left panel to Audio Graph and press Build Graph first.");
                return;
            }

            DrawGraphRoutes();
        }

        private void DrawGraphRoutes()
        {
            GUILayout.Label("Graph channel → instrument node id (multi-timbral helper).");
            GUILayout.Label("Note On on the left uses ResolveInstrumentPluginId(channel).");

            var instrumentNodeIds = new List<int>();
            foreach (var n in audioGraph.Nodes)
            {
                if (n.kind == VstGraphNodeKind.Instrument && n.pluginId >= 1)
                {
                    instrumentNodeIds.Add(n.id);
                    GUILayout.Label($"  node {n.id} Instrument pluginId={n.pluginId} bypass={n.bypass}");
                }
            }

            if (instrumentNodeIds.Count == 0)
            {
                GUILayout.Label("Build a Graph with at least one Instrument first.");
                return;
            }

            if (routeNodeId < 1 || !instrumentNodeIds.Contains(routeNodeId))
                routeNodeId = instrumentNodeIds[0];

            routeChannel = Mathf.RoundToInt(GUILayout.HorizontalSlider(routeChannel, 0, 15));
            GUILayout.Label($"Channel {routeChannel}");

            GUILayout.Label("Target instrument node:");
            for (var i = 0; i < instrumentNodeIds.Count; i++)
            {
                var nid = instrumentNodeIds[i];
                if (GUILayout.Toggle(routeNodeId == nid, $"node {nid}", "Button"))
                    routeNodeId = nid;
            }

            if (GUILayout.Button("Add / replace channel route"))
            {
                var list = new List<VstAudioGraph.ChannelRoute>();
                foreach (var r in audioGraph.ChannelRoutes)
                {
                    if (r.channel != routeChannel)
                        list.Add(r);
                }

                list.Add(new VstAudioGraph.ChannelRoute
                {
                    channel = routeChannel,
                    nodeId = routeNodeId,
                });
                audioGraph.SetChannelRoutes(list);
                status = $"Route ch{routeChannel} → node {routeNodeId}";
            }

            if (GUILayout.Button("Clear channel routes"))
            {
                audioGraph.SetChannelRoutes(System.Array.Empty<VstAudioGraph.ChannelRoute>());
                status = "Cleared graph channel routes";
            }

            GUILayout.Label("Current routes:");
            foreach (var r in audioGraph.ChannelRoutes)
                GUILayout.Label($"  ch {r.channel} → node {r.nodeId}");
        }

        private void EnsureParameterList(int pluginId)
        {
            parameters.Clear();
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            parameters.AddRange(VstHostManager.Instance.GetParameters(pluginId));
        }
    }
}
