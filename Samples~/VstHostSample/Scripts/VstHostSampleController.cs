using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.sample
{
    /// <summary>
    /// Sample controller: VST-only (scan / load / manual notes / audio / plugin chain).
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
            Chain = 1,
        }

        [SerializeField] private string preferredPluginNameContains = "mda DX10";
        [SerializeField] private string fallbackPluginNameContains = "NoiseMaker";
        [SerializeField] private string preferredEffectNameContains = "AGain";
        [SerializeField] private string fallbackEffectNameContains = "Delay";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool showGui = true;
        [SerializeField] private bool enableMidiAdapterIfPresent = true;
        [SerializeField] private int note = 60;
        [SerializeField] private int velocity = 100;
        [SerializeField] private int channel;

        private readonly List<VstHostManager.ScannedPlugin> scanned = new List<VstHostManager.ScannedPlugin>();
        private readonly List<int> loadedPluginIds = new List<int>();
        private VstHostAudioFilter audioFilter;
        private VstPluginChain pluginChain;
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
        private VstPluginChain.MixMode chainMixMode = VstPluginChain.MixMode.ParallelInstrumentsThenSerialEffects;
        private bool effectBypass;
        private bool isLoading;
        private bool editEffectParameters;

        /// <summary>Currently loaded instrument plugin id, or -1 when unloaded.</summary>
        public int CurrentPluginId => pluginId;

        /// <summary>Loaded effect plugin id in Chain mode, or -1.</summary>
        public int CurrentEffectPluginId => effectPluginId;

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
            audioFilter.EnsureSilentSourcePlaying();

            pluginChain = GetComponent<VstPluginChain>();
            if (pluginChain == null)
                pluginChain = gameObject.AddComponent<VstPluginChain>();
            pluginChain.enabled = false;

            parameterPanel = GetComponent<VstHostParameterPanel>();
            if (parameterPanel == null)
                parameterPanel = gameObject.AddComponent<VstHostParameterPanel>();

            TryEnsureMidiAdapter();
            ApplyAudioPathEnabledState();

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
            var useChain = playbackMode == PlaybackMode.Chain;
            if (audioFilter != null)
            {
                audioFilter.enabled = !useChain;
                if (!useChain)
                    audioFilter.EnsureSilentSourcePlaying();
            }

            if (pluginChain != null)
            {
                pluginChain.enabled = useChain;
                if (useChain)
                    pluginChain.EnsureSilentSourcePlaying();
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
            if (playbackMode == PlaybackMode.Chain)
                BuildChain();
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
            status = mode == PlaybackMode.Chain
                ? "Chain mode: select instrument + effect, then Build Chain"
                : "Single mode";
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

        private void BuildChain()
        {
            if (isLoading)
                return;

            if (selectedIndex < 0 || selectedIndex >= scanned.Count
                || selectedEffectIndex < 0 || selectedEffectIndex >= scanned.Count)
            {
                status = "Select instrument and effect plugins";
                return;
            }

            isLoading = true;
            try
            {
                UnloadCurrent();
                playbackMode = PlaybackMode.Chain;
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

                pluginChain.Mode = chainMixMode;
                pluginChain.SetSlots(new[]
                {
                    VstPluginChain.Slot.Instrument(pluginId),
                    VstPluginChain.Slot.Effect(effectPluginId),
                });
                pluginChain.SetBypass(effectPluginId, effectBypass);
                pluginChain.EnsureSilentSourcePlaying();
                if (audioFilter != null)
                    audioFilter.EnsureSilentSourcePlaying();

                editEffectParameters = false;
                BindParameterPanel(pluginId);
                SetMidiTarget(pluginId);

                var instrCat = instrument.Category ?? string.Empty;
                var warnFxAsInstr = instrCat.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) < 0
                    && (instrCat.IndexOf("Fx", StringComparison.OrdinalIgnoreCase) >= 0
                        || instrCat.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0);

                status =
                    $"Chain: [{pluginId}] {instrument.Name} → [{effectPluginId}] {effect.Name}"
                    + (effectBypass ? " (effect bypassed)" : string.Empty);
                if (warnFxAsInstr)
                    status += " — warning: top list looks like an Effect; pick a synth/Instrument or Note On stays silent";
            }
            finally
            {
                isLoading = false;
            }
        }

        private void UnloadCurrent()
        {
            SetMidiTarget(-1);
            if (audioFilter != null)
                audioFilter.DetachPlugin();
            if (pluginChain != null)
                pluginChain.ClearSlots();
            if (parameterPanel != null)
                parameterPanel.PluginId = -1;

            DestroyLoadedInstances();
            pluginId = -1;
            effectPluginId = -1;
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
            if (playbackMode != PlaybackMode.Chain)
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
            if (playbackMode == PlaybackMode.Chain && pluginChain != null)
            {
                var routed = pluginChain.ResolveInstrumentPluginId(channel);
                if (routed >= 1)
                    return routed;
            }

            return pluginId;
        }

        private void ApplyEffectBypass(bool bypass)
        {
            effectBypass = bypass;
            if (playbackMode == PlaybackMode.Chain && effectPluginId >= 1 && pluginChain != null)
            {
                pluginChain.SetBypass(effectPluginId, bypass);
                status = bypass ? "Effect bypassed" : "Effect engaged";
            }
        }

        private void ApplyChainMixMode(VstPluginChain.MixMode mode)
        {
            chainMixMode = mode;
            if (pluginChain != null)
                pluginChain.Mode = mode;
        }

        private void OnGUI()
        {
            if (!showGui) return;

            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
            GUILayout.BeginArea(new Rect(12, 12, 460f, Screen.height / guiScale - 24));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Unity Plugin Host for VST3 — Sample");
            GUILayout.Label(status);
            GUILayout.Label(midiAvailable
                ? "MIDI assembly detected (MIDI + VST ready)"
                : "MIDI assembly not present (VST only)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(playbackMode == PlaybackMode.Single, "Single", "Button"))
                SetPlaybackMode(PlaybackMode.Single);
            if (GUILayout.Toggle(playbackMode == PlaybackMode.Chain, "Plugin Chain", "Button"))
                SetPlaybackMode(PlaybackMode.Chain);
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
                if (GUILayout.Button("Build Chain"))
                    BuildChain();
            }

            if (GUILayout.Button("Unload"))
                UnloadCurrent();
            GUILayout.EndHorizontal();

            GUILayout.Label(playbackMode == PlaybackMode.Chain
                ? "Instrument (notes / MIDI target)"
                : "Plugin");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(playbackMode == PlaybackMode.Chain ? 120 : 180));
            for (var i = 0; i < scanned.Count; i++)
            {
                var label = $"{scanned[i].Name} [{scanned[i].Category}]";
                if (GUILayout.Toggle(selectedIndex == i, label, "Button"))
                    selectedIndex = i;
            }
            GUILayout.EndScrollView();

            if (playbackMode == PlaybackMode.Chain)
            {
                GUILayout.Label("Effect (serial after instrument)");
                effectScroll = GUILayout.BeginScrollView(effectScroll, GUILayout.Height(120));
                for (var i = 0; i < scanned.Count; i++)
                {
                    var label = $"{scanned[i].Name} [{scanned[i].Category}]";
                    if (GUILayout.Toggle(selectedEffectIndex == i, label, "Button"))
                        selectedEffectIndex = i;
                }
                GUILayout.EndScrollView();

                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(
                        chainMixMode == VstPluginChain.MixMode.ParallelInstrumentsThenSerialEffects,
                        "Parallel→Serial",
                        "Button"))
                    ApplyChainMixMode(VstPluginChain.MixMode.ParallelInstrumentsThenSerialEffects);
                if (GUILayout.Toggle(
                        chainMixMode == VstPluginChain.MixMode.StrictSerial,
                        "Strict Serial",
                        "Button"))
                    ApplyChainMixMode(VstPluginChain.MixMode.StrictSerial);
                GUILayout.EndHorizontal();

                // Button style so it matches other toolbar toggles (plain Toggle is easy to miss).
                var bypass = GUILayout.Toggle(
                    effectBypass,
                    effectBypass ? "Bypass effect: ON (dry)" : "Bypass effect: OFF (wet)",
                    "Button");
                if (bypass != effectBypass)
                    ApplyEffectBypass(bypass);

                if (pluginId >= 1 && effectPluginId >= 1)
                {
                    GUILayout.Label($"Active chain ids: instr={pluginId}  fx={effectPluginId}");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(!editEffectParameters, "Edit Instrument params", "Button"))
                        SetParameterEditTarget(false);
                    if (GUILayout.Toggle(editEffectParameters, "Edit Effect params", "Button"))
                        SetParameterEditTarget(true);
                    GUILayout.EndHorizontal();
                }
            }

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

            GUILayout.Label("See Documentation~/verification.md and plugin-chain.md.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.matrix = prev;
        }
    }
}
