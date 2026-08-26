#if FEATURE_MIDI_PLUGIN && FEATURE_MIDI_NETWORK
using jp.kshoji.unity.midi.net;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Wires <see cref="MidiNetworkClient"/> (or an explicit virtual device id) into
    /// <see cref="VstHostMidiAdapter"/> so remote MIDI plays a local VST instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostNetworkMidiLink : MonoBehaviour
    {
        [SerializeField] private VstHostMidiAdapter adapter;
        [SerializeField] private MidiNetworkClient networkClient;
        [Tooltip("Used when Network Client is unset. Default matches MidiNetworkClient.virtualDeviceId.")]
        [SerializeField] private string virtualDeviceId = "network:remote";
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool filterAdapterToNetworkDevice = true;

        public string VirtualDeviceId
        {
            get
            {
                if (networkClient != null && !string.IsNullOrEmpty(networkClient.virtualDeviceId))
                    return networkClient.virtualDeviceId;
                return virtualDeviceId;
            }
            set => virtualDeviceId = value;
        }

        private void Reset()
        {
            adapter = GetComponent<VstHostMidiAdapter>();
            networkClient = GetComponent<MidiNetworkClient>();
        }

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        /// <summary>MCP / runtime wiring for network virtual device → adapter.</summary>
        public void Configure(VstHostMidiAdapter? adapterRef = null)
        {
            if (adapterRef != null)
                adapter = adapterRef;
        }

        /// <summary>Filters the adapter to the network virtual device and registers it.</summary>
        public void Apply()
        {
            if (adapter == null)
                adapter = GetComponent<VstHostMidiAdapter>();
            if (adapter == null)
            {
                Debug.LogWarning("[VstHostNetworkMidiLink] VstHostMidiAdapter is required.", this);
                return;
            }

            if (networkClient == null)
                networkClient = GetComponent<MidiNetworkClient>();

            var deviceId = VirtualDeviceId;
            if (string.IsNullOrEmpty(deviceId))
            {
                Debug.LogWarning("[VstHostNetworkMidiLink] Virtual device id is empty.", this);
                return;
            }

            if (filterAdapterToNetworkDevice)
                adapter.SetAllowedDeviceIds(deviceId);

            if (!adapter.IsRegistered)
                adapter.Register();
        }
    }
}
#endif
