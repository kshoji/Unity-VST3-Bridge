using System;
using System.Threading;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>One timed MIDI 1.0 short message for DSP-clock scheduling.</summary>
    public struct VstHostDspMidiEvent
    {
        public long DspSample;
        public int PluginId;
        public byte Status;
        public byte Data1;
        public byte Data2;
    }

    /// <summary>
    /// Lock-free SPSC-friendly ring of timed MIDI events (multiple producers OK with lock).
    /// Audio thread consumers call <see cref="FlushDue"/> / <see cref="PeekNextDspSample"/>.
    /// </summary>
    public sealed class VstHostDspMidiQueue
    {
        public static readonly VstHostDspMidiQueue Shared = new VstHostDspMidiQueue(4096);

        private readonly VstHostDspMidiEvent[] buffer;
        private readonly object writeLock = new object();
        private int head; // consumer index
        private int tail; // producer index
        private int count;

        public VstHostDspMidiQueue(int capacity)
        {
            if (capacity < 16)
                capacity = 16;
            // power of two
            var pow = 1;
            while (pow < capacity)
                pow <<= 1;
            buffer = new VstHostDspMidiEvent[pow];
        }

        public int Count => Volatile.Read(ref count);

        public bool TryEnqueue(in VstHostDspMidiEvent evt)
        {
            lock (writeLock)
            {
                if (count >= buffer.Length)
                    return false;
                buffer[tail] = evt;
                tail = (tail + 1) & (buffer.Length - 1);
                count++;
                return true;
            }
        }

        public void ScheduleMidi1(int pluginId, long dspSample, byte status, byte data1, byte data2)
        {
            TryEnqueue(new VstHostDspMidiEvent
            {
                DspSample = dspSample,
                PluginId = pluginId,
                Status = status,
                Data1 = data1,
                Data2 = data2,
            });
        }

        public void ScheduleNoteOn(int pluginId, long dspSample, int channel, int note, int velocity)
        {
            Midi1Util.NoteOn(channel, note, velocity, out var s, out var d1, out var d2);
            ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void ScheduleNoteOff(int pluginId, long dspSample, int channel, int note, int velocity = 0)
        {
            Midi1Util.NoteOff(channel, note, velocity, out var s, out var d1, out var d2);
            ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void ScheduleControlChange(int pluginId, long dspSample, int channel, int controller, int value)
        {
            Midi1Util.ControlChange(channel, controller, value, out var s, out var d1, out var d2);
            ScheduleMidi1(pluginId, dspSample, s, d1, d2);
        }

        public void Clear()
        {
            lock (writeLock)
            {
                head = 0;
                tail = 0;
                count = 0;
            }
        }

        /// <summary>
        /// Returns the next event DSP sample, or <see cref="long.MaxValue"/> when empty.
        /// </summary>
        public long PeekNextDspSample()
        {
            if (Volatile.Read(ref count) <= 0)
                return long.MaxValue;
            return buffer[head].DspSample;
        }

        /// <summary>
        /// Sends all due events with <c>DspSample &lt;= untilDspSampleInclusive</c>.
        /// Calls native SendMidi1 directly (audio-thread safe; skips managed activity bus).
        /// </summary>
        public int FlushDue(long untilDspSampleInclusive)
        {
            var sent = 0;
            var host = VstHostManager.Instance;
            if (host == null || !host.IsInitialized)
                return 0;

            while (true)
            {
                VstHostDspMidiEvent evt;
                lock (writeLock)
                {
                    if (count <= 0)
                        break;
                    evt = buffer[head];
                    if (evt.DspSample > untilDspSampleInclusive)
                        break;
                    head = (head + 1) & (buffer.Length - 1);
                    count--;
                }

                if (evt.PluginId >= 1)
                {
                    VstHostNative.VstHost_SendMidi1(evt.PluginId, evt.Status, evt.Data1, evt.Data2);
                    sent++;
                }
            }

            return sent;
        }
    }
}
