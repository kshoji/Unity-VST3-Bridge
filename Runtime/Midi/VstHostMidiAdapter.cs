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
        [SerializeField] private int targetPluginId = -1;
        [SerializeField] private bool autoRegisterWithMidiManager = true;
        [SerializeField] private bool filterByDeviceId;
        [SerializeField] private List<string> allowedDeviceIds = new List<string>();

        private bool registered;

        /// <summary>Plugin instance id receiving MIDI (from <see cref="VstHostManager.CreateInstance"/>).</summary>
        public int TargetPluginId
        {
            get => targetPluginId;
            set => targetPluginId = value;
        }

        public bool IsRegistered => registered;

        private void OnEnable()
        {
            if (autoRegisterWithMidiManager)
                Register();
        }

        private void OnDisable()
        {
            Unregister();
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

        private bool Accept(string deviceId)
        {
            if (targetPluginId < 1)
                return false;
            if (!filterByDeviceId || allowedDeviceIds == null || allowedDeviceIds.Count == 0)
                return true;
            return allowedDeviceIds.Contains(deviceId);
        }

        private void Send(byte status, byte data1, byte data2)
        {
            VstHostManager.Instance.SendMidi1(targetPluginId, status, data1, data2);
        }

        // --- MIDI 1.0 (UMP message type 2 / classic handlers) ---

        public void OnMidi1NoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.NoteOn(channel, note, velocity, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1NoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.NoteOff(channel, note, velocity, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1ControlChange(string deviceId, int group, int channel, int function, int value)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.ControlChange(channel, function, value, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1ProgramChange(string deviceId, int group, int channel, int program)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1ChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.ChannelAftertouch(channel, pressure, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1PitchWheel(string deviceId, int group, int channel, int amount)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.PitchBend(channel, amount, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1PolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.PolyphonicAftertouch(channel, note, pressure, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi1SystemExclusive(string deviceId, int group, byte[] systemExclusive)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedSysex();
        }

        // --- MIDI 2.0 channel voice (down-convert) ---

        public void OnMidi2NoteOn(string deviceId, int group, int channel, int note, int velocity, int attributeType, int attributeData)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.NoteOn(channel, note, Midi1Util.DownconvertVelocity16(velocity), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2NoteOff(string deviceId, int group, int channel, int note, int velocity, int attributeType, int attributeData)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.NoteOff(channel, note, Midi1Util.DownconvertVelocity16(velocity), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2ControlChange(string deviceId, int group, int channel, int index, uint value)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.ControlChange(channel, index, Midi1Util.DownconvertU32To7(value), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2ProgramChange(string deviceId, int group, int channel, int optionFlags, int program, int bank)
        {
            if (!Accept(deviceId)) return;
            // Bank select (optionalFlags / bank) deferred; send program only.
            Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2ChannelAftertouch(string deviceId, int group, int channel, uint pressure)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.ChannelAftertouch(channel, Midi1Util.DownconvertU32To7(pressure), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2PitchWheel(string deviceId, int group, int channel, uint amount)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.PitchBend(channel, Midi1Util.DownconvertPitchBend32(amount), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2PolyphonicAftertouch(string deviceId, int group, int channel, int note, uint pressure)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.PolyphonicAftertouch(channel, note, Midi1Util.DownconvertU32To7(pressure), out var s, out var d1, out var d2);
            Send(s, d1, d2);
        }

        public void OnMidi2PerNotePitchWheel(string deviceId, int group, int channel, int note, uint amount)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2PerNoteManagement(string deviceId, int group, int channel, int note, int optionFlags)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2RegisteredPerNoteController(string deviceId, int group, int channel, int note, int index, uint data)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2AssignablePerNoteController(string deviceId, int group, int channel, int note, int index, uint data)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedPerNote();
        }

        public void OnMidi2SystemExclusive(string deviceId, int group, int streamId, byte[] systemExclusive)
        {
            if (!Accept(deviceId)) return;
            Midi1Util.LogSkippedSysex();
        }
    }
}
