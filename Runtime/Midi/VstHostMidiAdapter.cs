using System;
using System.Collections.Generic;
using jp.kshoji.unity.midi;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Optional bridge: Unity MIDI Plugin events → <see cref="VstHostManager"/> MIDI 1.0 queue.
    /// Compiles only when <c>FEATURE_MIDI_PLUGIN</c> is defined (see Editor define sync).
    /// Routing stays in this VST package; do not register into MIDI core <c>midi2Plugins</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostMidiAdapter : MonoBehaviour,
        IMidi1NoteOnEventHandler,
        IMidi1NoteOffEventHandler,
        IMidi1ControlChangeEventHandler,
        IMidi1ProgramChangeEventHandler,
        IMidi1ChannelAftertouchEventHandler,
        IMidi1PitchWheelEventHandler,
        IMidi1PolyphonicAftertouchEventHandler,
        IMidi1SystemExclusiveEventHandler,
        IMidi2NoteOnEventHandler,
        IMidi2NoteOffEventHandler,
        IMidi2ControlChangeEventHandler,
        IMidi2ProgramChangeEventHandler,
        IMidi2ChannelAftertouchEventHandler,
        IMidi2PitchWheelEventHandler,
        IMidi2PolyphonicAftertouchEventHandler,
        IMidi2PerNotePitchWheelEventHandler,
        IMidi2PerNoteManagementEventHandler,
        IMidi2RegisteredPerNoteControllerEventHandler,
        IMidi2AssignablePerNoteControllerEventHandler,
        IMidi2SystemExclusiveEventHandler
    {
        [Serializable]
        public struct ChannelRoute
        {
            [Tooltip("MIDI channel 0–15.")]
            [Range(0, 15)]
            public int channel;

            [Tooltip("VST plugin instance id from VstHostManager.CreateInstance.")]
            public int pluginId;
        }

        [SerializeField] private int targetPluginId = -1;
        [SerializeField] private bool autoRegisterWithMidiManager = true;
        [SerializeField] private bool filterByDeviceId;
        [SerializeField] private List<string> allowedDeviceIds = new List<string>();

        [Header("Channel routing (multi-timbral)")]
        [Tooltip("When non-empty, routes each MIDI channel to a plugin id. Unlisted channels use TargetPluginId.")]
        [SerializeField] private List<ChannelRoute> channelRoutes = new List<ChannelRoute>();

        [Header("Forward filters")]
        [SerializeField] private bool forwardControlChange = true;
        [SerializeField] private bool forwardPitchBend = true;
        [SerializeField] private bool forwardProgramChange = true;

        [Tooltip("When true, MIDI Program Change also calls VstHostManager.SetProgram (host program list).")]
        [SerializeField] private bool mapProgramChangeToHostProgram;

        private bool registered;
        private Dictionary<int, int> channelRouteLookup;

        /// <summary>Default plugin instance id receiving MIDI (from <see cref="VstHostManager.CreateInstance"/>).</summary>
        public int TargetPluginId
        {
            get => targetPluginId;
            set => targetPluginId = value;
        }

        public bool ForwardControlChange
        {
            get => forwardControlChange;
            set => forwardControlChange = value;
        }

        public bool ForwardPitchBend
        {
            get => forwardPitchBend;
            set => forwardPitchBend = value;
        }

        public bool ForwardProgramChange
        {
            get => forwardProgramChange;
            set => forwardProgramChange = value;
        }

        public bool MapProgramChangeToHostProgram
        {
            get => mapProgramChangeToHostProgram;
            set => mapProgramChangeToHostProgram = value;
        }

        public bool IsRegistered => registered;

        /// <summary>Channel → plugin id routes (multi-timbral). Empty uses <see cref="TargetPluginId"/> only.</summary>
        public IList<ChannelRoute> ChannelRoutes => channelRoutes;

        /// <summary>Replaces channel→plugin routes used for multi-timbral SMF / controller setups.</summary>
        public void SetChannelRoutes(IEnumerable<ChannelRoute> routes)
        {
            channelRoutes = routes != null ? new List<ChannelRoute>(routes) : new List<ChannelRoute>();
            RebuildChannelRouteLookup();
        }

        /// <summary>Enables device filtering and allows only the given device ids.</summary>
        public void SetAllowedDeviceIds(params string[] deviceIds)
        {
            filterByDeviceId = true;
            allowedDeviceIds = deviceIds != null
                ? new List<string>(deviceIds)
                : new List<string>();
        }

        private void OnEnable()
        {
            RebuildChannelRouteLookup();
            if (autoRegisterWithMidiManager)
                Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnValidate()
        {
            RebuildChannelRouteLookup();
        }

        /// <summary>Subscribe to MIDI Plugin public events via <see cref="MidiManager.RegisterEventHandleObject"/>.</summary>
        public void Register()
        {
            if (registered) return;
            if (MidiManager.Instance == null)
            {
                Debug.LogWarning("[VstHostMidiAdapter] MidiManager.Instance is null; cannot register.");
                return;
            }

            MidiManager.Instance.RegisterEventHandleObject(this);
            registered = true;
        }

        public void Unregister()
        {
            if (!registered) return;
            if (MidiManager.Instance != null)
                MidiManager.Instance.UnregisterEventHandleObject(this);
            registered = false;
        }

        private void RebuildChannelRouteLookup()
        {
            channelRouteLookup = new Dictionary<int, int>();
            if (channelRoutes == null)
                return;
            foreach (var route in channelRoutes)
            {
                if (route.channel < 0 || route.channel > 15 || route.pluginId < 1)
                    continue;
                channelRouteLookup[route.channel] = route.pluginId;
            }
        }

        private bool AcceptDevice(string deviceId)
        {
            if (!filterByDeviceId || allowedDeviceIds == null || allowedDeviceIds.Count == 0)
                return true;
            return allowedDeviceIds.Contains(deviceId);
        }

        /// <summary>Resolves the plugin id for a MIDI channel (channel route, else <see cref="TargetPluginId"/>).</summary>
        public int ResolvePluginId(int channel)
        {
            if (channelRouteLookup != null && channelRouteLookup.TryGetValue(channel, out var routed) && routed >= 1)
                return routed;
            return targetPluginId;
        }

        private bool TryResolve(string deviceId, int channel, out int pluginId)
        {
            pluginId = -1;
            if (!AcceptDevice(deviceId))
                return false;
            pluginId = ResolvePluginId(channel);
            return pluginId >= 1;
        }

        private static void Send(int pluginId, byte status, byte data1, byte data2)
        {
            VstHostManager.Instance.SendMidi1(pluginId, status, data1, data2);
        }

        // --- MIDI 1.0 (UMP message type 2 / classic handlers) ---

        public void OnMidi1NoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.NoteOn(channel, note, velocity, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1NoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.NoteOff(channel, note, velocity, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1ControlChange(string deviceId, int group, int channel, int function, int value)
        {
            if (!forwardControlChange) return;
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.ControlChange(channel, function, value, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1ProgramChange(string deviceId, int group, int channel, int program)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            if (forwardProgramChange)
            {
                Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
                Send(pluginId, s, d1, d2);
            }

            if (mapProgramChangeToHostProgram)
                VstHostManager.Instance.SetProgram(pluginId, program);
        }

        public void OnMidi1ChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.ChannelAftertouch(channel, pressure, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1PitchWheel(string deviceId, int group, int channel, int amount)
        {
            if (!forwardPitchBend) return;
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.PitchBend(channel, amount, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1PolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.PolyphonicAftertouch(channel, note, pressure, out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi1SystemExclusive(string deviceId, int group, byte[] systemExclusive)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedSysex();
        }

        // --- MIDI 2.0 channel voice (down-convert) ---

        public void OnMidi2NoteOn(string deviceId, int group, int channel, int note, int velocity, int attributeType, int attributeData)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.NoteOn(channel, note, Midi1Util.DownconvertVelocity16(velocity), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2NoteOff(string deviceId, int group, int channel, int note, int velocity, int attributeType, int attributeData)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.NoteOff(channel, note, Midi1Util.DownconvertVelocity16(velocity), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2ControlChange(string deviceId, int group, int channel, int index, uint value)
        {
            if (!forwardControlChange) return;
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.ControlChange(channel, index, Midi1Util.DownconvertU32To7(value), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2ProgramChange(string deviceId, int group, int channel, int optionFlags, int program, int bank)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            if (forwardProgramChange)
            {
                // Bank select (optionalFlags / bank) deferred; send program only.
                Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
                Send(pluginId, s, d1, d2);
            }

            if (mapProgramChangeToHostProgram)
                VstHostManager.Instance.SetProgram(pluginId, program);
        }

        public void OnMidi2ChannelAftertouch(string deviceId, int group, int channel, uint pressure)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.ChannelAftertouch(channel, Midi1Util.DownconvertU32To7(pressure), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2PitchWheel(string deviceId, int group, int channel, uint amount)
        {
            if (!forwardPitchBend) return;
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.PitchBend(channel, Midi1Util.DownconvertPitchBend32(amount), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2PolyphonicAftertouch(string deviceId, int group, int channel, int note, uint pressure)
        {
            if (!TryResolve(deviceId, channel, out var pluginId)) return;
            Midi1Util.PolyphonicAftertouch(channel, note, Midi1Util.DownconvertU32To7(pressure), out var s, out var d1, out var d2);
            Send(pluginId, s, d1, d2);
        }

        public void OnMidi2PerNotePitchWheel(string deviceId, int group, int channel, int note, uint amount)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2PerNoteManagement(string deviceId, int group, int channel, int note, int optionFlags)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2RegisteredPerNoteController(string deviceId, int group, int channel, int note, int index, uint data)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2AssignablePerNoteController(string deviceId, int group, int channel, int note, int index, uint data)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2SystemExclusive(string deviceId, int group, int streamId, byte[] systemExclusive)
        {
            if (!AcceptDevice(deviceId)) return;
            Midi1Util.LogSkippedSysex();
        }
    }
}
