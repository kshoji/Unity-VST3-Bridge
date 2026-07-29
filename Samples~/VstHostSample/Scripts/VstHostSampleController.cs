using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.sample
{
    /// <summary>
    /// Sample controller: VST-only (scan / load / manual notes / audio).
    /// When Unity MIDI Plugin + <c>FEATURE_MIDI_PLUGIN</c> are available,
    /// optionally attaches <c>VstHostMidiAdapter</c> via reflection (no hard asmdef dependency).
    /// </summary>
    public sealed class VstHostSampleController : MonoBehaviour
    {
        private const string MidiAdapterTypeName =
            "jp.kshoji.unity.vst3nativehost.VstHostMidiAdapter, jp.kshoji.unity.vst3nativehost.Midi";

        [SerializeField] private string preferredPluginNameContains = "mda DX10";
        [SerializeField] private string fallbackPluginNameContains = "NoiseMaker";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool showGui = true;
        [SerializeField] private bool enableMidiAdapterIfPresent = true;
        [SerializeField] private int note = 60;
        [SerializeField] private int velocity = 100;
        [SerializeField] private int channel;

        private readonly List<VstHostManager.ScannedPlugin> scanned = new List<VstHostManager.ScannedPlugin>();
        private VstHostAudioFilter audioFilter;
        private VstHostParameterPanel parameterPanel;
        private Component midiAdapter;
        private Type midiAdapterType;
        private int pluginId = -1;
        private string status = "Idle";
        private Vector2 scroll;
        private int selectedIndex;
        private float guiScale = 1f;
        private bool midiAvailable;

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

            parameterPanel = GetComponent<VstHostParameterPanel>();
            if (parameterPanel == null)
                parameterPanel = gameObject.AddComponent<VstHostParameterPanel>();

            TryEnsureMidiAdapter();
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
                selectedIndex = Mathf.Clamp(selectedIndex, 0, scanned.Count - 1);
        }

        private void TryAutoLoad()
        {
            if (scanned.Count == 0)
                return;

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

            if (index < 0)
                index = 0;

            selectedIndex = index;
            LoadSelected();
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

        private bool isLoading;

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
                var p = scanned[selectedIndex];
                pluginId = VstHostManager.Instance.CreateInstance(p.FilePath, p.Uid);
                if (pluginId < 1)
                {
                    status = $"Load failed: {p.Name}";
                    return;
                }

                // Fx → Effect (needs input buses / side-chain binding); else Instrument.
                var cat = p.Category ?? string.Empty;
                audioFilter.Mode = cat.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0
                    ? VstHostAudioFilter.ProcessMode.Instrument
                    : VstHostAudioFilter.ProcessMode.Effect;

                // Refresh params before arming audio (Attach arms on next LateUpdate).
                parameterPanel.PluginId = pluginId;
                parameterPanel.Refresh();
                SetMidiTarget(pluginId);
                audioFilter.AttachPlugin(pluginId);

                status = midiAvailable && enableMidiAdapterIfPresent
                    ? $"Loaded id={pluginId} {p.Name} (MIDI adapter available)"
                    : $"Loaded id={pluginId} {p.Name} (manual notes)";
            }
            finally
            {
                isLoading = false;
            }
        }

        private void UnloadCurrent()
        {
            if (pluginId < 1)
                return;

            SetMidiTarget(-1);
            if (audioFilter != null)
                audioFilter.DetachPlugin();
            if (parameterPanel != null)
                parameterPanel.PluginId = -1;

            VstHostManager.Instance.DestroyInstance(pluginId);
            pluginId = -1;
            status = "Unloaded";
        }

        private void NoteOn()
        {
            if (pluginId < 1) return;
            VstHostManager.Instance.NoteOn(pluginId, channel, note, velocity);
            status = $"NoteOn ch={channel} note={note} vel={velocity}";
        }

        private void NoteOff()
        {
            if (pluginId < 1) return;
            VstHostManager.Instance.NoteOff(pluginId, channel, note, 0);
            status = $"NoteOff ch={channel} note={note}";
        }

        private void OnGUI()
        {
            if (!showGui) return;

            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
            GUILayout.BeginArea(new Rect(12, 12, 420f, Screen.height / guiScale - 24));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Unity Plugin Host for VST3 — Sample");
            GUILayout.Label(status);
            GUILayout.Label(midiAvailable
                ? "MIDI assembly detected (MIDI + VST ready)"
                : "MIDI assembly not present (VST only)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rescan"))
                Rescan();
            if (GUILayout.Button("Load"))
                LoadSelected();
            if (GUILayout.Button("Unload"))
                UnloadCurrent();
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            for (var i = 0; i < scanned.Count; i++)
            {
                var label = $"{scanned[i].Name} [{scanned[i].Category}]";
                if (GUILayout.Toggle(selectedIndex == i, label, "Button"))
                    selectedIndex = i;
            }
            GUILayout.EndScrollView();

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

            GUILayout.Label("See Documentation~/verification.md for A/B/IL2CPP steps.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.matrix = prev;
        }
    }
}
