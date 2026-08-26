#if FEATURE_SCRIPTABLE_AUDIO && UNITY_6000_3_OR_NEWER && !UNITY_WEBGL
using jp.kshoji.unity.midi.scriptableaudio;
using jp.kshoji.unity.vst3nativehost;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.scriptableaudio
{
    /// <summary>
    /// Mirrors <see cref="MidiDspSequenceScheduler"/> timed MIDI into
    /// <see cref="VstHostDspMidiQueue"/> for DSP-clocked VST playback
    /// (pair with <see cref="VstHostGenerator"/> or <see cref="VstHostAudioFilter"/> / <see cref="VstAudioGraph"/>).
    /// Assign this component to the scheduler's <c>extraTimedMidiOutput</c> field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostDspMidiOutBridge : MonoBehaviour, IMidiDspTimedMidiOutput
    {
        [SerializeField] private int targetPluginId = -1;
        [SerializeField] private VstParameterTarget parameterTarget;
        [SerializeField] private VstAudioGraph audioGraph;
        [SerializeField] private bool bridgeEnabled = true;
        [Tooltip("-1 keeps the source channel from the scheduled event.")]
        [SerializeField] private int outputChannelOverride = -1;

        public bool BridgeEnabled
        {
            get => bridgeEnabled;
            set => bridgeEnabled = value;
        }

        public int TargetPluginId
        {
            get => targetPluginId;
            set => targetPluginId = value;
        }

        /// <summary>MCP / runtime wiring for SA timed MIDI → VST DSP queue.</summary>
        public void Configure(VstParameterTarget? target = null, VstAudioGraph? graph = null)
        {
            if (target != null)
                parameterTarget = target;
            if (graph != null)
                audioGraph = graph;
        }

        int ResolvePluginId(byte channel)
        {
            if (audioGraph != null)
            {
                var routed = audioGraph.ResolveInstrumentPluginId(
                    outputChannelOverride >= 0 ? outputChannelOverride : channel);
                if (routed >= 1)
                    return routed;
            }

            if (parameterTarget != null)
            {
                var id = parameterTarget.PluginId;
                if (id >= 1)
                    return id;
            }

            return targetPluginId;
        }

        byte ResolveChannel(byte channel)
        {
            if (outputChannelOverride >= 0 && outputChannelOverride <= 15)
                return (byte)outputChannelOverride;
            return channel;
        }

        public void ScheduleNote(long dspSample, byte channel, byte note, byte velocity, bool isNoteOn)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            if (isNoteOn)
                VstHostDspMidiQueue.Shared.ScheduleNoteOn(pluginId, dspSample, channel, note, velocity);
            else
                VstHostDspMidiQueue.Shared.ScheduleNoteOff(pluginId, dspSample, channel, note, velocity);
        }

        public void ScheduleControlChange(long dspSample, byte channel, byte controller, byte value)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            VstHostDspMidiQueue.Shared.ScheduleControlChange(pluginId, dspSample, channel, controller, value);
        }

        public void ScheduleProgramChange(long dspSample, byte channel, byte program)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
            VstHostDspMidiQueue.Shared.ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void SchedulePolyPressure(long dspSample, byte channel, byte note, byte pressure)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            Midi1Util.PolyphonicAftertouch(channel, note, pressure, out var s, out var d1, out var d2);
            VstHostDspMidiQueue.Shared.ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void ScheduleChannelPressure(long dspSample, byte channel, byte pressure)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            Midi1Util.ChannelAftertouch(channel, pressure, out var s, out var d1, out var d2);
            VstHostDspMidiQueue.Shared.ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void SchedulePitchBend(long dspSample, byte channel, byte lsb, byte msb)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(channel);
            if (pluginId < 1)
                return;
            channel = ResolveChannel(channel);
            var amount = lsb | (msb << 7);
            Midi1Util.PitchBend(channel, amount, out var s, out var d1, out var d2);
            VstHostDspMidiQueue.Shared.ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void ScheduleSystemMessage(long dspSample, byte status, byte data1, byte data2)
        {
            if (!bridgeEnabled)
                return;
            var pluginId = ResolvePluginId(0);
            if (pluginId < 1)
                return;
            VstHostDspMidiQueue.Shared.ScheduleMidi1(pluginId, dspSample, status, data1, data2);
        }

        public void ScheduleSysex(long dspSample, byte[] payload)
        {
            // Native VST host skips SysEx; ignore.
        }

        public void ClearPending()
        {
            VstHostDspMidiQueue.Shared.Clear();
        }
    }
}
#endif
