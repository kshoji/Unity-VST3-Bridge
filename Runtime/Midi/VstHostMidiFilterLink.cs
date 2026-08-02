#if FEATURE_MIDI_PLUGIN
using System.Collections.Generic;
using jp.kshoji.unity.midi;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Wires <see cref="MidiChannelFilter"/> (and optionally other filter chains) so that
    /// forwarded MIDI reaches <see cref="VstHostMidiAdapter"/> without double-registration
    /// on <see cref="MidiManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class VstHostMidiFilterLink : MonoBehaviour
    {
        [SerializeField] private MidiChannelFilter channelFilter;
        [SerializeField] private VstHostMidiAdapter adapter;
        [Tooltip("When true, Adapter stops registering directly with MidiManager and only receives filter forwards.")]
        [SerializeField] private bool unregisterAdapterFromMidiManager = true;
        [SerializeField] private bool applyOnEnable = true;

        private void Reset()
        {
            channelFilter = GetComponent<MidiChannelFilter>();
            adapter = GetComponent<VstHostMidiAdapter>();
        }

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        /// <summary>
        /// Adds the Adapter GameObject to the filter's <c>forwardTargets</c> and optionally
        /// unregisters the Adapter from MidiManager so messages flow Filter → Adapter only.
        /// </summary>
        public void Apply()
        {
            if (channelFilter == null)
                channelFilter = GetComponent<MidiChannelFilter>();
            if (adapter == null)
                adapter = GetComponent<VstHostMidiAdapter>();

            if (channelFilter == null || adapter == null)
            {
                Debug.LogWarning("[VstHostMidiFilterLink] MidiChannelFilter and VstHostMidiAdapter are required.", this);
                return;
            }

            EnsureForwardTarget(channelFilter, adapter.gameObject);

            if (unregisterAdapterFromMidiManager)
            {
                adapter.Unregister();
            }
            else if (!adapter.IsRegistered)
            {
                adapter.Register();
            }
        }

        private static void EnsureForwardTarget(MidiChannelFilter filter, GameObject target)
        {
            if (filter == null || target == null)
                return;

            var list = filter.forwardTargets != null
                ? new List<GameObject>(filter.forwardTargets)
                : new List<GameObject>();

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == target)
                    return;
            }

            list.Add(target);
            filter.forwardTargets = list.ToArray();
        }
    }
}
#endif
