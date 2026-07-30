using jp.kshoji.unity.midi;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Wires <see cref="SmfPlayer"/> output to a virtual MIDI device that
    /// <see cref="VstHostMidiAdapter"/> (and optional parameter mapper) can receive.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class VstHostSmfLink : MonoBehaviour
    {
        public const string DefaultVirtualDeviceId = "vst3:smf";

        [Tooltip("Virtual device ID used as SmfPlayer output and Adapter device filter.")]
        [SerializeField] private string virtualDeviceId = DefaultVirtualDeviceId;

        [SerializeField] private SmfPlayer smfPlayer;
        [SerializeField] private VstHostMidiAdapter midiAdapter;
        [SerializeField] private VstHostMidiParameterMapper parameterMapper;

        [SerializeField] private bool registerOnAwake = true;
        [SerializeField] private bool applyToSmfPlayer = true;
        [SerializeField] private bool applyAdapterDeviceFilter = true;
        [SerializeField] private bool applyMapperDeviceFilter = true;
        [SerializeField] private bool unregisterOnDestroy = true;

        private bool registered;

        public string VirtualDeviceId
        {
            get => virtualDeviceId;
            set => virtualDeviceId = value;
        }

        public SmfPlayer SmfPlayer
        {
            get => smfPlayer;
            set => smfPlayer = value;
        }

        public VstHostMidiAdapter MidiAdapter
        {
            get => midiAdapter;
            set => midiAdapter = value;
        }

        public bool IsRegistered => registered;

        private void Awake()
        {
            if (registerOnAwake)
                Connect();
        }

        private void OnDestroy()
        {
            if (unregisterOnDestroy)
                Disconnect();
        }

        /// <summary>
        /// Registers the virtual device and applies it to <see cref="SmfPlayer"/> / adapters.
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(virtualDeviceId))
            {
                Debug.LogWarning("[VstHostSmfLink] virtualDeviceId is empty.", this);
                return;
            }

            if (MidiManager.Instance == null)
            {
                Debug.LogWarning("[VstHostSmfLink] MidiManager.Instance is null.", this);
                return;
            }

            MidiManager.Instance.RegisterVirtualMidiDevice(virtualDeviceId, input: true, output: true);
            registered = true;

            if (applyToSmfPlayer)
            {
                if (smfPlayer == null)
                    smfPlayer = GetComponent<SmfPlayer>();
                if (smfPlayer != null)
                    smfPlayer.outputDeviceId = virtualDeviceId;
            }

            if (applyAdapterDeviceFilter)
            {
                if (midiAdapter == null)
                    midiAdapter = GetComponent<VstHostMidiAdapter>();
                if (midiAdapter != null)
                    midiAdapter.SetAllowedDeviceIds(virtualDeviceId);
            }

            if (applyMapperDeviceFilter)
            {
                if (parameterMapper == null)
                    parameterMapper = GetComponent<VstHostMidiParameterMapper>();
                if (parameterMapper != null)
                    parameterMapper.SetAllowedDeviceIds(virtualDeviceId);
            }
        }

        /// <summary>Unregisters the virtual device if this component registered it.</summary>
        public void Disconnect()
        {
            if (!registered || string.IsNullOrEmpty(virtualDeviceId) || MidiManager.Instance == null)
                return;

            MidiManager.Instance.UnregisterVirtualMidiDevice(virtualDeviceId);
            registered = false;
        }
    }
}
