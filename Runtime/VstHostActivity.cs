using System;
using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>Kind of host activity for editor / debug monitors.</summary>
    public enum VstHostActivityKind
    {
        Midi1 = 0,
        Parameter = 1,
        Program = 2,
        State = 3,
    }

    /// <summary>One host activity record (MIDI send, parameter change, etc.).</summary>
    public readonly struct VstHostActivityEntry
    {
        public readonly double TimeSeconds;
        public readonly VstHostActivityKind Kind;
        public readonly int PluginId;
        public readonly string Detail;

        public VstHostActivityEntry(double timeSeconds, VstHostActivityKind kind, int pluginId, string detail)
        {
            TimeSeconds = timeSeconds;
            Kind = kind;
            PluginId = pluginId;
            Detail = detail ?? string.Empty;
        }
    }

    /// <summary>
    /// Lightweight activity bus for editor monitors and diagnostics.
    /// <see cref="Raise"/> is intended for main-thread callers (parameter / program).
    /// MIDI uses <see cref="EnqueueMidi1"/> / <see cref="RecordMidi1Fail"/> from any thread;
    /// <see cref="PumpMainThread"/> delivers them on the main thread (no Unity API on MIDI/audio threads).
    /// Subscribers must not allocate heavily; MIDI events may arrive at device rate after pump.
    /// </summary>
    public static class VstHostActivity
    {
        private const int MidiRingCapacity = 512;

        private static readonly object midiLock = new object();
        private static readonly MidiPending[] midiRing = new MidiPending[MidiRingCapacity];
        private static int midiHead;
        private static int midiTail;
        private static int midiCount;

        private static int midiFailCount;
        private static int lastMidiFailPluginId;
        private static int lastMidiFailResult;
        private static int midiFailSinceWarn;
        private static float lastMidiFailWarnRealtime = -999f;

        public static event Action<VstHostActivityEntry> Raised;

        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Main-thread preferred. Uses a thread-safe clock (not <see cref="Time.realtimeSinceStartup"/>).
        /// </summary>
        public static void Raise(VstHostActivityKind kind, int pluginId, string detail)
        {
            if (!Enabled)
                return;
            var handler = Raised;
            if (handler == null)
                return;
            handler(new VstHostActivityEntry(
                NowSeconds(),
                kind,
                pluginId,
                detail));
        }

        /// <summary>
        /// Any thread. Queues a successful MIDI send for <see cref="PumpMainThread"/>.
        /// No-ops when capture is disabled or there are no subscribers.
        /// </summary>
        public static void EnqueueMidi1(int pluginId, byte status, byte data1, byte data2)
        {
            if (!Enabled || Raised == null)
                return;

            lock (midiLock)
            {
                if (midiCount >= MidiRingCapacity)
                {
                    midiHead = (midiHead + 1) & (MidiRingCapacity - 1);
                    midiCount--;
                }

                midiRing[midiTail] = new MidiPending
                {
                    PluginId = pluginId,
                    Status = status,
                    Data1 = data1,
                    Data2 = data2,
                    TickMs = Environment.TickCount64,
                };
                midiTail = (midiTail + 1) & (MidiRingCapacity - 1);
                midiCount++;
            }
        }

        /// <summary>Any thread. Records a failed <c>SendMidi1</c> for main-thread warning.</summary>
        public static void RecordMidi1Fail(int pluginId, VstHostResult result)
        {
            Interlocked.Increment(ref midiFailCount);
            Interlocked.Exchange(ref lastMidiFailPluginId, pluginId);
            Interlocked.Exchange(ref lastMidiFailResult, (int)result);
        }

        /// <summary>
        /// Main thread only. Drains deferred MIDI activity and emits rate-limited fail warnings.
        /// Safe to call from multiple LateUpdates / Editor update.
        /// </summary>
        public static void PumpMainThread()
        {
            DrainMidiToRaised();
            PumpMidiFailWarnings();
        }

        private static void DrainMidiToRaised()
        {
            if (!Enabled)
            {
                ClearMidiRing();
                return;
            }

            var handler = Raised;
            if (handler == null)
            {
                ClearMidiRing();
                return;
            }

            while (true)
            {
                MidiPending pending;
                lock (midiLock)
                {
                    if (midiCount <= 0)
                        break;
                    pending = midiRing[midiHead];
                    midiHead = (midiHead + 1) & (MidiRingCapacity - 1);
                    midiCount--;
                }

                handler(new VstHostActivityEntry(
                    pending.TickMs / 1000.0,
                    VstHostActivityKind.Midi1,
                    pending.PluginId,
                    $"status=0x{pending.Status:X2} d1={pending.Data1} d2={pending.Data2}"));
            }
        }

        private static void PumpMidiFailWarnings()
        {
            var dropped = Interlocked.Exchange(ref midiFailCount, 0);
            if (dropped > 0)
                midiFailSinceWarn += dropped;
            if (midiFailSinceWarn <= 0)
                return;

            var now = Time.realtimeSinceStartup;
            if (now - lastMidiFailWarnRealtime < 1f)
                return;

            lastMidiFailWarnRealtime = now;
            var n = midiFailSinceWarn;
            midiFailSinceWarn = 0;
            var pluginId = Volatile.Read(ref lastMidiFailPluginId);
            var result = (VstHostResult)Volatile.Read(ref lastMidiFailResult);
            Debug.LogWarning(
                $"[VstHost] SendMidi1 failed {n} time(s); last id={pluginId}: {result}");
        }

        private static void ClearMidiRing()
        {
            lock (midiLock)
            {
                midiHead = 0;
                midiTail = 0;
                midiCount = 0;
            }
        }

        private static double NowSeconds() => Environment.TickCount64 / 1000.0;

        private struct MidiPending
        {
            public int PluginId;
            public byte Status;
            public byte Data1;
            public byte Data2;
            public long TickMs;
        }
    }
}
