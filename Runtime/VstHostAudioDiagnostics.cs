using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Audio-thread-safe counters for Process / block-size issues.
    /// Audio paths only increment; main thread calls
    /// <see cref="PumpMainThreadDiagnostics"/> to emit rate-limited warnings.
    /// </summary>
    public static class VstHostAudioDiagnostics
    {
        private static int processFailCount;
        private static int blockSizeSkipCount;
        private static int bufferCapacityClipCount;
        private static int lastBlockSizeSkipFrames;
        private static int lastBlockSizeSkipLimit;
        private static int lastBufferCapacityClipFrames;
        private static int lastBufferCapacity;

        private static int lifetimeProcessFail;
        private static int lifetimeBlockSizeSkip;
        private static int lifetimeBufferCapacityClip;

        private static int processFailSinceWarn;
        private static int blockSizeSkipSinceWarn;
        private static int bufferCapacityClipSinceWarn;
        private static float lastWarnRealtime = -999f;

        /// <summary>Call from the audio thread when <c>Process</c> returns false / non-Ok.</summary>
        public static void RecordProcessFail()
        {
            Interlocked.Increment(ref processFailCount);
            Interlocked.Increment(ref lifetimeProcessFail);
        }

        /// <summary>
        /// Call from the audio thread when a DSP callback is skipped because
        /// <paramref name="frames"/> exceeds the host <paramref name="blockSize"/>.
        /// </summary>
        public static void RecordBlockSizeSkip(int frames, int blockSize)
        {
            Interlocked.Increment(ref blockSizeSkipCount);
            Interlocked.Increment(ref lifetimeBlockSizeSkip);
            Interlocked.Exchange(ref lastBlockSizeSkipFrames, frames);
            Interlocked.Exchange(ref lastBlockSizeSkipLimit, blockSize);
        }

        /// <summary>
        /// Call from the audio thread when a generator truncates
        /// <paramref name="requestedFrames"/> to <paramref name="capacity"/>.
        /// </summary>
        public static void RecordBufferCapacityClip(int requestedFrames, int capacity)
        {
            Interlocked.Increment(ref bufferCapacityClipCount);
            Interlocked.Increment(ref lifetimeBufferCapacityClip);
            Interlocked.Exchange(ref lastBufferCapacityClipFrames, requestedFrames);
            Interlocked.Exchange(ref lastBufferCapacity, capacity);
        }

        /// <summary>
        /// Lifetime counters since last <see cref="ClearLifetime"/> (not cleared by pump warnings).
        /// </summary>
        public static void Snapshot(
            out int processFail,
            out int blockSizeSkip,
            out int bufferCapacityClip,
            out int lastSkipFrames,
            out int lastSkipLimit,
            out int lastClipFrames,
            out int lastClipCapacity)
        {
            processFail = Volatile.Read(ref lifetimeProcessFail);
            blockSizeSkip = Volatile.Read(ref lifetimeBlockSizeSkip);
            bufferCapacityClip = Volatile.Read(ref lifetimeBufferCapacityClip);
            lastSkipFrames = Volatile.Read(ref lastBlockSizeSkipFrames);
            lastSkipLimit = Volatile.Read(ref lastBlockSizeSkipLimit);
            lastClipFrames = Volatile.Read(ref lastBufferCapacityClipFrames);
            lastClipCapacity = Volatile.Read(ref lastBufferCapacity);
        }

        /// <summary>Reset lifetime counters used by MCP / tooling.</summary>
        public static void ClearLifetime()
        {
            Interlocked.Exchange(ref lifetimeProcessFail, 0);
            Interlocked.Exchange(ref lifetimeBlockSizeSkip, 0);
            Interlocked.Exchange(ref lifetimeBufferCapacityClip, 0);
        }

        /// <summary>Main thread only. Safe to call from multiple LateUpdates.</summary>
        public static void PumpMainThreadDiagnostics()
        {
            Accumulate(Consume(ref processFailCount), ref processFailSinceWarn);
            Accumulate(Consume(ref blockSizeSkipCount), ref blockSizeSkipSinceWarn);
            Accumulate(Consume(ref bufferCapacityClipCount), ref bufferCapacityClipSinceWarn);

            if (processFailSinceWarn <= 0
                && blockSizeSkipSinceWarn <= 0
                && bufferCapacityClipSinceWarn <= 0)
                return;

            var now = Time.realtimeSinceStartup;
            if (now - lastWarnRealtime < 1f)
                return;

            lastWarnRealtime = now;

            if (processFailSinceWarn > 0)
            {
                var n = processFailSinceWarn;
                processFailSinceWarn = 0;
                Debug.LogWarning(
                    $"[VstHost] Process failed {n} time(s) (invalid id, not ready, or native ProcessFailed). " +
                    "Effect mode leaves the input buffer unchanged (bypass-equivalent).");
            }

            if (blockSizeSkipSinceWarn > 0)
            {
                var n = blockSizeSkipSinceWarn;
                blockSizeSkipSinceWarn = 0;
                var frames = Volatile.Read(ref lastBlockSizeSkipFrames);
                var limit = Volatile.Read(ref lastBlockSizeSkipLimit);
                Debug.LogWarning(
                    $"[VstHost] Skipped {n} audio callback(s): frames ({frames}) > BlockSize ({limit}). " +
                    "Call InitializeFromAudioSettings (or Initialize with a larger blockSize) so DSP buffer ≤ BlockSize.");
            }

            if (bufferCapacityClipSinceWarn > 0)
            {
                var n = bufferCapacityClipSinceWarn;
                bufferCapacityClipSinceWarn = 0;
                var frames = Volatile.Read(ref lastBufferCapacityClipFrames);
                var capacity = Volatile.Read(ref lastBufferCapacity);
                Debug.LogWarning(
                    $"[VstHost] Clipped {n} generator block(s): frames ({frames}) > bufferCapacity ({capacity}).");
            }
        }

        private static int Consume(ref int counter) => Interlocked.Exchange(ref counter, 0);

        private static void Accumulate(int delta, ref int sinceWarn)
        {
            if (delta > 0)
                sinceWarn += delta;
        }
    }
}
