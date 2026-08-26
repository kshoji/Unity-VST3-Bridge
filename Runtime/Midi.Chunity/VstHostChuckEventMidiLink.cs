#if FEATURE_MIDI_PLUGIN && FEATURE_CHUNITY
using jp.kshoji.unity.midi.integrations.chunity;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Routes <see cref="MidiChuckEventToMidi"/> MIDI output into <see cref="VstHostMidiAdapter"/>
    /// so ChucK Events can drive a VST instrument.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostChuckEventMidiLink : MonoBehaviour
    {
        [SerializeField] private VstHostMidiAdapter adapter;
        [SerializeField] private MidiChuckEventToMidi eventToMidi;
        [Tooltip("When empty, uses the first binding's outputDeviceId or MidiChuckUtility default.")]
        [SerializeField] private string virtualDeviceIdOverride = string.Empty;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool filterAdapterToEventDevice = true;

        private void Reset()
        {
            adapter = GetComponent<VstHostMidiAdapter>();
            eventToMidi = GetComponent<MidiChuckEventToMidi>();
        }

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        /// <summary>MCP / runtime wiring for Chuck event MIDI → adapter.</summary>
        public void Configure(VstHostMidiAdapter? adapterRef = null, string? deviceIdOverride = null)
        {
            if (adapterRef != null)
                adapter = adapterRef;
            if (deviceIdOverride != null)
                virtualDeviceIdOverride = deviceIdOverride;
        }

        public string ResolveDeviceId()
        {
            if (!string.IsNullOrEmpty(virtualDeviceIdOverride))
                return virtualDeviceIdOverride;

            if (eventToMidi != null && eventToMidi.bindings != null)
            {
                for (var i = 0; i < eventToMidi.bindings.Length; i++)
                {
                    var binding = eventToMidi.bindings[i];
                    if (binding != null && !string.IsNullOrEmpty(binding.outputDeviceId))
                        return binding.outputDeviceId;
                }
            }

            return MidiChuckUtility.DefaultEventMidiOutDeviceId;
        }

        public void Apply()
        {
            if (adapter == null)
                adapter = GetComponent<VstHostMidiAdapter>();
            if (eventToMidi == null)
                eventToMidi = GetComponent<MidiChuckEventToMidi>();

            if (adapter == null)
            {
                Debug.LogWarning("[VstHostChuckEventMidiLink] VstHostMidiAdapter is required.", this);
                return;
            }

            var deviceId = ResolveDeviceId();
            if (string.IsNullOrEmpty(deviceId))
            {
                Debug.LogWarning("[VstHostChuckEventMidiLink] Device id is empty.", this);
                return;
            }

            if (filterAdapterToEventDevice)
                adapter.SetAllowedDeviceIds(deviceId);

            if (!adapter.IsRegistered)
                adapter.Register();
        }
    }
}
#endif
