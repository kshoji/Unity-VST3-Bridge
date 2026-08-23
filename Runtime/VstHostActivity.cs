using System;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

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
        private const int RecentCapacity = 256;

        private static readonly object midiLock = new object();
        private static readonly MidiPending[] midiRing = new MidiPending[MidiRingCapacity];
        private static int midiHead;
        private static int midiTail;
        private static int midiCount;

        private static readonly object recentLock = new object();
        private static readonly VstHostActivityEntry[] recentRing = new VstHostActivityEntry[RecentCapacity];
        private static int recentHead;
        private static int recentCount;

        private static int midiFailCount;
        private static int lastMidiFailPluginId;
        private static int lastMidiFailResult;
        private static int midiFailSinceWarn;
        private static float lastMidiFailWarnRealtime = -999f;

        public static event Action<VstHostActivityEntry> Raised;

        public static bool Enabled { get; set; } = true;

        /// <summary>Number of entries currently retained in the recent ring (0–256).</summary>
        public static int RecentCount
        {
            get
            {
                lock (recentLock)
                    return recentCount;
            }
        }

        /// <summary>
        /// Copy the most recent activity entries (oldest → newest within the returned span).
        /// Call <see cref="PumpMainThread"/> first so deferred MIDI is flushed.
        /// </summary>
        public static VstHostActivityEntry[] GetRecent(int maxCount = 64)
        {
            if (maxCount < 1)
                return Array.Empty<VstHostActivityEntry>();

            lock (recentLock)
            {
                var take = Math.Min(maxCount, recentCount);
                if (take <= 0)
                    return Array.Empty<VstHostActivityEntry>();

                var result = new VstHostActivityEntry[take];
                var start = (recentHead + recentCount - take + RecentCapacity) % RecentCapacity;
                for (var i = 0; i < take; i++)
                    result[i] = recentRing[(start + i) % RecentCapacity];
                return result;
            }
        }

        /// <summary>Clear the recent ring used by monitors / MCP (does not disable capture).</summary>
        public static void ClearRecent()
        {
            lock (recentLock)
            {
                recentHead = 0;
                recentCount = 0;
                Array.Clear(recentRing, 0, recentRing.Length);
            }
        }

        /// <summary>
        /// Main-thread preferred. Uses a thread-safe clock (not <see cref="Time.realtimeSinceStartup"/>).
        /// </summary>
        public static void Raise(VstHostActivityKind kind, int pluginId, string detail)
        {
            if (!Enabled)
                return;
            var entry = new VstHostActivityEntry(
                NowSeconds(),
                kind,
                pluginId,
                detail);
            AppendRecent(entry);
            Raised?.Invoke(entry);
        }

        /// <summary>
        /// Any thread. Queues a successful MIDI send for <see cref="PumpMainThread"/>.
        /// No-ops when capture is disabled. Entries are retained in the recent ring even
        /// when there are no <see cref="Raised"/> subscribers (MCP / headless).
        /// </summary>
        public static void EnqueueMidi1(int pluginId, byte status, byte data1, byte data2)
        {
            if (!Enabled)
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
                    TimeSeconds = NowSeconds(),
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

                var entry = new VstHostActivityEntry(
                    pending.TimeSeconds,
                    VstHostActivityKind.Midi1,
                    pending.PluginId,
                    $"status=0x{pending.Status:X2} d1={pending.Data1} d2={pending.Data2}");
                AppendRecent(entry);
                Raised?.Invoke(entry);
            }
        }

        private static void AppendRecent(in VstHostActivityEntry entry)
        {
            lock (recentLock)
            {
                if (recentCount > 0)
                {
                    var lastIndex = (recentHead + recentCount - 1 + RecentCapacity) % RecentCapacity;
                    var last = recentRing[lastIndex];
                    if (last.Kind == entry.Kind
                        && last.PluginId == entry.PluginId
                        && last.Detail == entry.Detail
                        && entry.TimeSeconds - last.TimeSeconds < 0.002)
                    {
                        return;
                    }
                }

                if (recentCount >= RecentCapacity)
                {
                    recentHead = (recentHead + 1) % RecentCapacity;
                    recentCount--;
                }

                var index = (recentHead + recentCount) % RecentCapacity;
                recentRing[index] = entry;
                recentCount++;
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

        /// <summary>Thread-safe monotonic-ish clock (.NET Standard 2.0 / Unity compatible).</summary>
        private static double NowSeconds() =>
            (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private struct MidiPending
        {
            public int PluginId;
            public byte Status;
            public byte Data1;
            public byte Data2;
            public double TimeSeconds;
        }
    }
}
