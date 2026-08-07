using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.sample
{
    /// <summary>
    /// Sample controller: VST-only (scan / load / manual notes / audio / audio graph).
    /// When Unity MIDI Plugin + <c>FEATURE_MIDI_PLUGIN</c> are available,
    /// optionally attaches <c>VstHostMidiAdapter</c> via reflection (no hard asmdef dependency).
    /// </summary>
    public sealed class VstHostSampleController : MonoBehaviour
    {
        private const string MidiAdapterTypeName =
            "jp.kshoji.unity.vst3nativehost.VstHostMidiAdapter, jp.kshoji.unity.vst3nativehost.Midi";

        private enum PlaybackMode
        {
            Single = 0,
            Graph = 1,
        }

        private enum GraphDemoKind
        {
            ParallelSerial = 0,
            SendReturn = 1,
            Sidechain = 2,
        }

        [SerializeField] private string preferredPluginNameContains = "mda DX10";
        [SerializeField] private string fallbackPluginNameContains = "NoiseMaker";
        [SerializeField] private string preferredEffectNameContains = "AGain";
        [SerializeField] private string fallbackEffectNameContains = "Delay";
        [SerializeField] private string preferredSidechainEffectNameContains = "SideChain";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool showGui = true;
        [SerializeField] private bool enableMidiAdapterIfPresent = true;
        [SerializeField] private int note = 60;
        [SerializeField] private int velocity = 100;
        [SerializeField] private int channel;

        private readonly List<VstHostManager.ScannedPlugin> scanned = new List<VstHostManager.ScannedPlugin>();
        private readonly List<int> loadedPluginIds = new List<int>();
        private VstHostAudioFilter audioFilter;
        private VstAudioGraph audioGraph;
        private VstHostParameterPanel parameterPanel;
        private Component midiAdapter;
        private Type midiAdapterType;
        private int pluginId = -1;
        private int effectPluginId = -1;
        private string status = "Idle";
        private Vector2 scroll;
        private Vector2 effectScroll;
        private int selectedIndex;
        private int selectedEffectIndex;
        private float guiScale = 1f;
        private bool midiAvailable;
        private PlaybackMode playbackMode = PlaybackMode.Single;
        private GraphDemoKind graphDemoKind = GraphDemoKind.ParallelSerial;
        private bool effectBypass;
        private bool graphMixExternalInput;
        private float graphSendGain = 0.3f;
        private int graphSplitNodeId = -1;
        private int graphSendEffectNodeId = -1;
        private bool isLoading;
        private bool editEffectParameters;

        /// <summary>Currently loaded instrument plugin id, or -1 when unloaded.</summary>
        public int CurrentPluginId => pluginId;

        /// <summary>Loaded effect plugin id in Graph mode, or -1.</summary>
        public int CurrentEffectPluginId => effectPluginId;

        /// <summary>True when left panel is in Audio Graph mode.</summary>
        public bool IsGraphMode => playbackMode == PlaybackMode.Graph;

        /// <summary>Active graph component (may be disabled).</summary>
        public VstAudioGraph AudioGraph => audioGraph;

        /// <summary>True when the optional MIDI adapter assembly is present.</summary>
        public bool MidiAssemblyAvailable => midiAvailable;

        private void Awake()
        {
            guiScale = Screen.width > Screen.height ? Screen.width / 1024f : Screen.height / 1024f;

            var host = VstHostManager.Instance;
            if (!host.IsInitialized)
                host.InitializeFromAudioSettings();

            audioFilter = GetComponent<VstHostAudioFilter>();
            if (audioFilter == null)
                audioFilter = gameObject.AddComponent<VstHostAudioFilter>();
            audioFilter.Mode = VstHostAudioFilter.ProcessMode.Instrument;

            // Disable siblings before AddComponent so Graph.OnEnable does not see an active peer path.
            audioFilter.enabled = false;
            audioGraph = GetComponent<VstAudioGraph>();
            if (audioGraph == null)
                audioGraph = gameObject.AddComponent<VstAudioGraph>();
            audioGraph.enabled = false;

            parameterPanel = GetComponent<VstHostParameterPanel>();
            if (parameterPanel == null)
                parameterPanel = gameObject.AddComponent<VstHostParameterPanel>();

            TryEnsureMidiAdapter();
            ApplyAudioPathEnabledState();
            if (audioFilter != null && audioFilter.enabled)
                audioFilter.EnsureSilentSourcePlaying();

            if (GetComponent<VstHostSampleFeatureDemos>() == null)
                gameObject.AddComponent<VstHostSampleFeatureDemos>();
        }

        private void Start()
        {
            Rescan();
            if (loadOnStart)
                TryAutoLoad();
        }

        private void OnDestroy()
        {
            UnloadCurrent();
        }

        private void TryEnsureMidiAdapter()
        {
            midiAdapterType = Type.GetType(MidiAdapterTypeName);
            midiAvailable = midiAdapterType != null;
            if (!enableMidiAdapterIfPresent || !midiAvailable)
                return;

            midiAdapter = GetComponent(midiAdapterType);
            if (midiAdapter == null)
                midiAdapter = gameObject.AddComponent(midiAdapterType);
        }

        private void SetMidiTarget(int id)
        {
            if (midiAdapter == null || midiAdapterType == null)
                return;

            var prop = midiAdapterType.GetProperty("TargetPluginId", BindingFlags.Instance | BindingFlags.Public);
            prop?.SetValue(midiAdapter, id);

            if (id >= 1)
                midiAdapterType.GetMethod("Register", BindingFlags.Instance | BindingFlags.Public)?.Invoke(midiAdapter, null);
            else
                midiAdapterType.GetMethod("Unregister", BindingFlags.Instance | BindingFlags.Public)?.Invoke(midiAdapter, null);
        }

        private void ApplyAudioPathEnabledState()
        {
            var useGraph = playbackMode == PlaybackMode.Graph;

            // Disable every path first so two OnAudioFilterRead handlers never run in one callback.
            if (audioFilter != null)
                audioFilter.enabled = false;
            if (audioGraph != null)
                audioGraph.enabled = false;

            if (useGraph)
            {
                if (audioGraph != null)
                {
                    audioGraph.enabled = true;
                    audioGraph.EnsureSilentSourcePlaying();
                }
            }
            else if (audioFilter != null)
            {
                audioFilter.enabled = true;
                audioFilter.EnsureSilentSourcePlaying();
            }

            // Restart the shared AudioSource so Unity rebinds the active OnAudioFilterRead chain.
            RestartSharedAudioSource();
        }

        private void RestartSharedAudioSource()
        {
            var src = GetComponent<AudioSource>();
            if (src == null)
                return;
            if (src.clip == null)
            {
                const int len = 256;
                var clip = AudioClip.Create("VstHostSampleSilence", len, 1, AudioSettings.outputSampleRate, false);
                clip.SetData(new float[len], 0);
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = false;
            }

            src.Stop();
            src.Play();
        }

        private static void ApplyPluginDefaultParameters(int pluginId)
        {
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            var parameters = VstHostManager.Instance.GetParameters(pluginId);
            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                VstHostManager.Instance.SetParameterNormalized(pluginId, p.Id, p.DefaultNormalized);
            }
        }

        /// <summary>
        /// AGain (and similar) default to ~unity, so Bypass A/B is inaudible. Pull Gain down for demos.
        /// </summary>
        private static void ApplyAudibleDemoEffectTone(int effectPluginId)
        {
            if (effectPluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;

            const double wetNormalized = 0.25;
            var parameters = VstHostManager.Instance.GetParameters(effectPluginId);
            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                if (p.IsReadOnly)
                    continue;
                var title = p.Title ?? string.Empty;
                if (title.IndexOf("Gain", StringComparison.OrdinalIgnoreCase) < 0
                    && title.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) < 0
                    && title.IndexOf("Level", StringComparison.OrdinalIgnoreCase) < 0
                    && i != 0)
                    continue;

                VstHostManager.Instance.SetParameterNormalized(effectPluginId, p.Id, wetNormalized);
                return;
            }
        }

        private void Rescan()
        {
            scanned.Clear();
            var host = VstHostManager.Instance;
            if (!host.IsInitialized)
            {
                status = "Host not initialized";
                return;
            }

            scanned.AddRange(host.Scan());
            status = $"Scan found {scanned.Count} class(es)";
            if (scanned.Count > 0)
            {
                selectedIndex = Mathf.Clamp(selectedIndex, 0, scanned.Count - 1);
                selectedEffectIndex = Mathf.Clamp(selectedEffectIndex, 0, scanned.Count - 1);
            }
        }

        private void TryAutoLoad()
        {
            if (scanned.Count == 0)
                return;

            selectedIndex = ResolvePreferredInstrumentIndex();
            selectedEffectIndex = ResolvePreferredEffectIndex();
            if (playbackMode == PlaybackMode.Graph)
                BuildGraph();
            else
                LoadSelected();
        }

        private int ResolvePreferredInstrumentIndex()
        {
            var index = FindPreferredIndex(preferredPluginNameContains);
            if (index < 0)
                index = FindPreferredIndex(fallbackPluginNameContains);
            if (index < 0)
            {
                for (var i = 0; i < scanned.Count; i++)
                {
                    if (!string.IsNullOrEmpty(scanned[i].Category)
                        && scanned[i].Category.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        index = i;
                        break;
                    }
                }
            }

            return index < 0 ? 0 : index;
        }

        private int ResolvePreferredEffectIndex()
        {
            var index = FindPreferredIndex(preferredEffectNameContains);
            if (index < 0)
                index = FindPreferredIndex(fallbackEffectNameContains);
            if (index < 0)
            {
                for (var i = 0; i < scanned.Count; i++)
                {
                    if (i == selectedIndex)
                        continue;
                    var cat = scanned[i].Category ?? string.Empty;
                    if (cat.IndexOf("Fx", StringComparison.OrdinalIgnoreCase) >= 0
                        || cat.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
                index = scanned.Count > 1 ? (selectedIndex == 0 ? 1 : 0) : selectedIndex;
            return index;
        }

        private int FindPreferredIndex(string contains)
        {
            if (string.IsNullOrEmpty(contains))
                return -1;
            for (var i = 0; i < scanned.Count; i++)
            {
                if (!string.IsNullOrEmpty(scanned[i].Name)
                    && scanned[i].Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return -1;
        }

        private void SetPlaybackMode(PlaybackMode mode)
        {
            if (playbackMode == mode)
                return;

            UnloadCurrent();
            playbackMode = mode;
            ApplyAudioPathEnabledState();
            if (mode == PlaybackMode.Graph && graphDemoKind == GraphDemoKind.Sidechain)
                selectedEffectIndex = ResolvePreferredSidechainEffectIndex();
            if (mode == PlaybackMode.Graph)
                status = "Graph mode: pick demo, select plugins, then Build Graph";
            else
                status = "Single mode";
        }

        private int ResolvePreferredSidechainEffectIndex()
        {
            var index = FindPreferredIndex(preferredSidechainEffectNameContains);
            return index >= 0 ? index : ResolvePreferredEffectIndex();
        }

        private void LoadSelected()
        {
            if (isLoading)
                return;

            if (selectedIndex < 0 || selectedIndex >= scanned.Count)
            {
                status = "No plugin selected";
                return;
            }

            isLoading = true;
            try
            {
                UnloadCurrent();
                playbackMode = PlaybackMode.Single;
                ApplyAudioPathEnabledState();

                var p = scanned[selectedIndex];
                pluginId = VstHostManager.Instance.CreateInstance(p.FilePath, p.Uid);
                if (pluginId < 1)
                {
                    status = $"Load failed: {p.Name}";
                    return;
                }

                loadedPluginIds.Add(pluginId);

                var cat = p.Category ?? string.Empty;
                audioFilter.Mode = cat.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0
                    ? VstHostAudioFilter.ProcessMode.Instrument
                    : VstHostAudioFilter.ProcessMode.Effect;

                parameterPanel.PluginId = pluginId;
                parameterPanel.Refresh();
                SetMidiTarget(pluginId);
                audioFilter.AttachPlugin(pluginId);

                editEffectParameters = false;

                status = midiAvailable && enableMidiAdapterIfPresent
                    ? $"Loaded id={pluginId} {p.Name} (MIDI adapter available)"
                    : $"Loaded id={pluginId} {p.Name} (manual notes)";
            }
            finally
            {
                isLoading = false;
            }
        }

        private void BuildGraph()
        {
            if (isLoading)
                return;

            if (selectedIndex < 0 || selectedIndex >= scanned.Count
                || selectedEffectIndex < 0 || selectedEffectIndex >= scanned.Count)
            {
                status = "Select instrument and effect plugins";
                return;
            }

            if (audioGraph == null)
            {
                status = "VstAudioGraph missing";
                return;
            }

            isLoading = true;
            try
            {
                UnloadCurrent();
                playbackMode = PlaybackMode.Graph;
                ApplyAudioPathEnabledState();

                var instrument = scanned[selectedIndex];
                var effect = scanned[selectedEffectIndex];

                pluginId = VstHostManager.Instance.CreateInstance(instrument.FilePath, instrument.Uid);
                if (pluginId < 1)
                {
                    status = $"Instrument load failed: {instrument.Name}";
                    return;
                }

                loadedPluginIds.Add(pluginId);

                effectPluginId = VstHostManager.Instance.CreateInstance(effect.FilePath, effect.Uid);
                if (effectPluginId < 1)
                {
                    status = $"Effect load failed: {effect.Name}";
                    DestroyLoadedInstances();
                    pluginId = -1;
                    return;
                }

                loadedPluginIds.Add(effectPluginId);

                ApplyPluginDefaultParameters(pluginId);
                ApplyPluginDefaultParameters(effectPluginId);
                ApplyAudibleDemoEffectTone(effectPluginId);

                graphSplitNodeId = -1;
                graphSendEffectNodeId = -1;
                var ok = false;
                switch (graphDemoKind)
                {
                    case GraphDemoKind.SendReturn:
                        ok = audioGraph.BuildSendReturn(pluginId, effectPluginId, graphSendGain, dryGain: 1f);
                        CacheSendReturnNodeIds();
                        break;
                    case GraphDemoKind.Sidechain:
                        ok = audioGraph.BuildSidechainFromSingleSource(pluginId, effectPluginId);
                        break;
                    default:
                        ok = audioGraph.BuildParallelInstrumentsThenSerialEffects(
                            new[] { pluginId },
                            new[] { effectPluginId },
                            mixExternalInput: graphMixExternalInput);
                        break;
                }

                if (!ok || !audioGraph.HasArmedGraph)
                {
                    status = "Graph build/arm failed (see Console)";
                    DestroyLoadedInstances();
                    pluginId = -1;
                    effectPluginId = -1;
                    return;
                }

                if (effectBypass)
                    audioGraph.SetBypassByPluginId(effectPluginId, true);

                // Re-assert path + restart so OnAudioFilterRead is bound after arm.
                ApplyAudioPathEnabledState();

                editEffectParameters = false;
                BindParameterPanel(pluginId);
                SetMidiTarget(pluginId);

                var demoLabel = "Parallel→Serial";
                if (graphDemoKind == GraphDemoKind.SendReturn)
                    demoLabel = "Send/Return";
                else if (graphDemoKind == GraphDemoKind.Sidechain)
                    demoLabel = "Sidechain";
                status =
                    $"Graph [{demoLabel}]: [{pluginId}] {instrument.Name} → [{effectPluginId}] {effect.Name}"
                    + (effectBypass ? " (effect bypassed)" : string.Empty)
                    + (graphDemoKind == GraphDemoKind.SendReturn ? $" send={graphSendGain:0.00}" : string.Empty)
                    + (graphMixExternalInput && graphDemoKind == GraphDemoKind.ParallelSerial
                        ? " +ExternalIn"
                        : string.Empty)
                    + (audioGraph.enabled ? " [path=Graph]" : " [WARN: Graph disabled]");
            }
            finally
            {
                isLoading = false;
            }
        }

        private void CacheSendReturnNodeIds()
        {
            graphSplitNodeId = -1;
            graphSendEffectNodeId = -1;
            if (audioGraph == null)
                return;
            foreach (var n in audioGraph.Nodes)
            {
                if (n.kind == VstGraphNodeKind.Split)
                    graphSplitNodeId = n.id;
                if (n.kind == VstGraphNodeKind.Effect)
                    graphSendEffectNodeId = n.id;
            }
        }

        private void UnloadCurrent()
        {
            SetMidiTarget(-1);
            if (audioFilter != null)
                audioFilter.DetachPlugin();
            if (audioGraph != null)
                audioGraph.ClearGraph();
            if (parameterPanel != null)
                parameterPanel.PluginId = -1;

            DestroyLoadedInstances();
            pluginId = -1;
            effectPluginId = -1;
            graphSplitNodeId = -1;
            graphSendEffectNodeId = -1;
            editEffectParameters = false;
            status = "Unloaded";
        }

        private void BindParameterPanel(int id)
        {
            if (parameterPanel == null)
                return;
            parameterPanel.PluginId = id;
            parameterPanel.Refresh();
        }

        private void SetParameterEditTarget(bool effect)
        {
            editEffectParameters = effect;
            if (playbackMode != PlaybackMode.Graph)
                return;

            var id = effect ? effectPluginId : pluginId;
            if (id < 1)
                return;
            BindParameterPanel(id);
            status = effect
                ? $"Editing effect parameters (id={id})"
                : $"Editing instrument parameters (id={id})";
        }

        private void DestroyLoadedInstances()
        {
            for (var i = 0; i < loadedPluginIds.Count; i++)
            {
                var id = loadedPluginIds[i];
                if (id >= 1)
                    VstHostManager.Instance.DestroyInstance(id);
            }

            loadedPluginIds.Clear();
        }

        private void NoteOn()
        {
            var target = ResolveNoteTargetPluginId();
            if (target < 1) return;
            VstHostManager.Instance.NoteOn(target, channel, note, velocity);
            status = $"NoteOn id={target} ch={channel} note={note} vel={velocity}";
        }

        private void NoteOff()
        {
            var target = ResolveNoteTargetPluginId();
            if (target < 1) return;
            VstHostManager.Instance.NoteOff(target, channel, note, 0);
            status = $"NoteOff id={target} ch={channel} note={note}";
        }

        private int ResolveNoteTargetPluginId()
        {
            if (playbackMode == PlaybackMode.Graph && audioGraph != null)
            {
                var routed = audioGraph.ResolveInstrumentPluginId(channel);
                if (routed >= 1)
                    return routed;
            }

            return pluginId;
        }

        private void ApplyEffectBypass(bool bypass)
        {
            effectBypass = bypass;
            if (effectPluginId < 1)
                return;

            if (playbackMode == PlaybackMode.Graph && audioGraph != null)
            {
                audioGraph.SetBypassByPluginId(effectPluginId, bypass);
                status = bypass
                    ? "Effect bypassed (dry / full level)"
                    : "Effect engaged (demo Gain≈0.25 — should be quieter than bypass)";
            }
        }

        private void SetGraphDemoKind(GraphDemoKind kind)
        {
            if (graphDemoKind == kind)
                return;
            graphDemoKind = kind;
            if (kind == GraphDemoKind.Sidechain)
                selectedEffectIndex = ResolvePreferredSidechainEffectIndex();
            if (kind == GraphDemoKind.SendReturn)
                status = "Graph demo: Send/Return (Build Graph)";
            else if (kind == GraphDemoKind.Sidechain)
                status = "Graph demo: Sidechain — prefer AGain SideChain effect";
            else
                status = "Graph demo: Parallel instruments → serial effects";
        }

        private void ApplyGraphSendGain(float gain)
        {
            graphSendGain = Mathf.Clamp(gain, 0f, 2f);
            if (playbackMode != PlaybackMode.Graph || audioGraph == null)
                return;
            if (graphSplitNodeId < 1 || graphSendEffectNodeId < 1)
                return;
            if (audioGraph.SetEdgeGain(graphSplitNodeId, graphSendEffectNodeId, VstGraphPort.Main, graphSendGain))
                status = $"Send gain = {graphSendGain:0.00}";
        }

        private void OnGUI()
        {
            if (!showGui) return;

            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
            GUILayout.BeginArea(new Rect(12, 12, 480f, Screen.height / guiScale - 24));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Unity Plugin Host for VST3 — Sample");
            GUILayout.Label(status);
            GUILayout.Label(midiAvailable
                ? "MIDI assembly detected (MIDI + VST ready)"
                : "MIDI assembly not present (VST only)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(playbackMode == PlaybackMode.Single, "Single", "Button"))
                SetPlaybackMode(PlaybackMode.Single);
            if (GUILayout.Toggle(playbackMode == PlaybackMode.Graph, "Audio Graph", "Button"))
                SetPlaybackMode(PlaybackMode.Graph);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rescan"))
                Rescan();
            if (playbackMode == PlaybackMode.Single)
            {
                if (GUILayout.Button("Load"))
                    LoadSelected();
            }
            else
            {
                if (GUILayout.Button("Build Graph"))
                    BuildGraph();
            }

            if (GUILayout.Button("Unload"))
                UnloadCurrent();
            GUILayout.EndHorizontal();

            var multiPlugin = playbackMode == PlaybackMode.Graph;
            GUILayout.Label(multiPlugin
                ? "Instrument (notes / MIDI target)"
                : "Plugin");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(multiPlugin ? 100 : 180));
            for (var i = 0; i < scanned.Count; i++)
            {
                var label = $"{scanned[i].Name} [{scanned[i].Category}]";
                if (GUILayout.Toggle(selectedIndex == i, label, "Button"))
                    selectedIndex = i;
            }
            GUILayout.EndScrollView();

            if (playbackMode == PlaybackMode.Graph)
                DrawGraphControls();

            GUILayout.Label($"Note {note}  Velocity {velocity}  Channel {channel}");
            note = Mathf.RoundToInt(GUILayout.HorizontalSlider(note, 0, 127));
            velocity = Mathf.RoundToInt(GUILayout.HorizontalSlider(velocity, 1, 127));
            channel = Mathf.RoundToInt(GUILayout.HorizontalSlider(channel, 0, 15));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Note On"))
                NoteOn();
            if (GUILayout.Button("Note Off"))
                NoteOff();
            GUILayout.EndHorizontal();

            GUILayout.Label("See Documentation~/verification.md, audio-graph.md.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.matrix = prev;
        }

        private void DrawGraphControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(graphDemoKind == GraphDemoKind.ParallelSerial, "Parallel→Serial", "Button"))
                SetGraphDemoKind(GraphDemoKind.ParallelSerial);
            if (GUILayout.Toggle(graphDemoKind == GraphDemoKind.SendReturn, "Send/Return", "Button"))
                SetGraphDemoKind(GraphDemoKind.SendReturn);
            if (GUILayout.Toggle(graphDemoKind == GraphDemoKind.Sidechain, "Sidechain", "Button"))
                SetGraphDemoKind(GraphDemoKind.Sidechain);
            GUILayout.EndHorizontal();

            GUILayout.Label(graphDemoKind == GraphDemoKind.Sidechain
                ? "Effect (prefer AGain SideChain)"
                : "Effect");
            effectScroll = GUILayout.BeginScrollView(effectScroll, GUILayout.Height(100));
            for (var i = 0; i < scanned.Count; i++)
            {
                var label = $"{scanned[i].Name} [{scanned[i].Category}]";
                if (GUILayout.Toggle(selectedEffectIndex == i, label, "Button"))
                    selectedEffectIndex = i;
            }
            GUILayout.EndScrollView();

            if (graphDemoKind == GraphDemoKind.ParallelSerial)
            {
                var ext = GUILayout.Toggle(graphMixExternalInput, "Mix ExternalIn (upstream filter)", "Button");
                if (ext != graphMixExternalInput)
                    graphMixExternalInput = ext;
            }

            if (graphDemoKind == GraphDemoKind.SendReturn)
            {
                GUILayout.Label($"Send gain {graphSendGain:0.00}");
                var g = GUILayout.HorizontalSlider(graphSendGain, 0f, 1f);
                if (!Mathf.Approximately(g, graphSendGain))
                    ApplyGraphSendGain(g);
            }

            var bypass = GUILayout.Toggle(
                effectBypass,
                effectBypass
                    ? "Bypass effect: ON (dry / full)"
                    : "Bypass effect: OFF (wet / quieter demo Gain)",
                "Button");
            if (bypass != effectBypass)
                ApplyEffectBypass(bypass);

            if (pluginId >= 1 && effectPluginId >= 1)
            {
                GUILayout.Label($"Active graph ids: instr={pluginId}  fx={effectPluginId}");
                GUILayout.Label("Tip: AGain default≈unity — demo sets Gain≈0.25 so Bypass A/B is audible.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(!editEffectParameters, "Edit Instrument params", "Button"))
                    SetParameterEditTarget(false);
                if (GUILayout.Toggle(editEffectParameters, "Edit Effect params", "Button"))
                    SetParameterEditTarget(true);
                GUILayout.EndHorizontal();
            }
        }
    }
}
