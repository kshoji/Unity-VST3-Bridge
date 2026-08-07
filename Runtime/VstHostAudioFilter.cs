using System;
using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Call native <c>VstHost_Process</c> from Unity's audio thread
    /// (<see cref="OnAudioFilterRead"/>) and write planar stereo into the filter buffer.
    /// Attach to a GameObject with an <see cref="AudioSource"/> (looping silence clip).
    /// Do not call Unity APIs from <see cref="OnAudioFilterRead"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class VstHostAudioFilter : MonoBehaviour
    {
        public enum ProcessMode
        {
            /// <summary>Instrument / generator: ignore input, replace buffer with VST output.</summary>
            Instrument,
            /// <summary>Effect: feed Unity input into VST, write processed output.</summary>
            Effect,
        }

        [SerializeField] private int pluginId = -1;
        [SerializeField] private ProcessMode mode = ProcessMode.Instrument;
        [SerializeField] private float outputGain = 1f;
        [SerializeField] private bool autoPlaySilentSource = true;
        [SerializeField] private bool flushDspMidiQueue = true;

        private float[] planarL = Array.Empty<float>();
        private float[] planarR = Array.Empty<float>();
        private AudioSource audioSource;
        private bool warnedNotReady;
        // Arm on main thread one frame after Attach so Load/Refresh cannot race first Process.
        private int pendingPluginId = -1;
        private int armedPluginId = -1;

        public int PluginId
        {
            get => Volatile.Read(ref armedPluginId);
            set
            {
                Volatile.Write(ref pendingPluginId, value);
                Volatile.Write(ref pluginId, value);
                if (value < 1)
                {
                    Volatile.Write(ref armedPluginId, -1);
                }
            }
        }

        public ProcessMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public float OutputGain
        {
            get => outputGain;
            set => outputGain = value;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void OnEnable()
        {
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        /// <summary>
        /// OnAudioFilterRead only runs while an AudioSource is playing.
        /// A short looping silent clip keeps the callback alive for instruments.
        /// </summary>
        public void EnsureSilentSourcePlaying()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                return;

            if (audioSource.clip == null)
            {
                const int len = 256;
                var clip = AudioClip.Create("VstHostSilence", len, 1, AudioSettings.outputSampleRate, false);
                var zeros = new float[len];
                clip.SetData(zeros, 0);
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        /// <summary>Stop routing audio to a plugin before native unload.</summary>
        public void DetachPlugin()
        {
            Volatile.Write(ref pendingPluginId, -1);
            Volatile.Write(ref armedPluginId, -1);
            Volatile.Write(ref pluginId, -1);
            // Do not Pause the AudioSource: the same GameObject may host VstAudioGraph,
            // which needs the silent clip to keep playing for OnAudioFilterRead.
        }

        /// <summary>
        /// Queue routing to <paramref name="id"/>. Process starts on the next LateUpdate
        /// so main-thread Load/parameter refresh can finish first.
        /// </summary>
        public void AttachPlugin(int id)
        {
            Volatile.Write(ref pendingPluginId, id);
            Volatile.Write(ref pluginId, id);
            // Keep armed cleared until LateUpdate — avoid Process during same-frame setup.
            Volatile.Write(ref armedPluginId, -1);
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void LateUpdate()
        {
            VstHostDspMidiQueue.Shared.PumpMainThreadDiagnostics();
            VstHostAudioDiagnostics.PumpMainThreadDiagnostics();
            VstHostActivity.PumpMainThread();

            if (warnedNotReady)
            {
                warnedNotReady = false;
                Debug.LogWarning("[VstHostAudioFilter] Host not initialized; audio skipped.");
            }

            var pending = Volatile.Read(ref pendingPluginId);
            if (Volatile.Read(ref armedPluginId) != pending)
                Volatile.Write(ref armedPluginId, pending);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var id = Volatile.Read(ref armedPluginId);
            if (id < 1 || data == null || data.Length == 0 || channels <= 0)
                return;

            var host = VstHostManager.Instance;
            if (host == null || !host.IsInitialized)
            {
                // Cannot log from audio thread.
                warnedNotReady = true;
                return;
            }

            var frames = data.Length / channels;
            if (frames <= 0)
                return;

            if (frames > host.BlockSize)
            {
                // Block larger than Initialize max — skip this callback.
                VstHostAudioDiagnostics.RecordBlockSizeSkip(frames, host.BlockSize);
                return;
            }

            EnsurePlanarCapacity(frames);

            if (flushDspMidiQueue)
            {
                var sampleRate = host.SampleRate > 0 ? host.SampleRate : AudioSettings.outputSampleRate;
                if (sampleRate <= 0)
                    sampleRate = 48000;
                var blockEnd = (long)(AudioSettings.dspTime * sampleRate) + frames;
                VstHostDspMidiQueue.Shared.FlushDue(blockEnd);
            }

            if (mode == ProcessMode.Effect)
            {
                Deinterleave(data, channels, frames, planarL, planarR);
                // On failure leave Unity input in `data` (bypass-equivalent).
                if (!host.Process(id, planarL, planarR, planarL, planarR, frames))
                {
                    VstHostAudioDiagnostics.RecordProcessFail();
                    return;
                }
            }
            else
            {
                Array.Clear(planarL, 0, frames);
                Array.Clear(planarR, 0, frames);
                if (!host.Process(id, null, null, planarL, planarR, frames))
                {
                    VstHostAudioDiagnostics.RecordProcessFail();
                    return;
                }
            }

            InterleaveReplace(planarL, planarR, data, channels, frames, outputGain);
        }

        private void EnsurePlanarCapacity(int frames)
        {
            if (planarL.Length < frames)
                planarL = new float[frames];
            if (planarR.Length < frames)
                planarR = new float[frames];
        }

        private static void Deinterleave(float[] interleaved, int channels, int frames, float[] left, float[] right)
        {
            if (channels == 1)
            {
                for (var i = 0; i < frames; i++)
                {
                    left[i] = interleaved[i];
                    right[i] = interleaved[i];
                }
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var baseIndex = i * channels;
                left[i] = interleaved[baseIndex];
                right[i] = interleaved[baseIndex + 1];
            }
        }

        private static void InterleaveReplace(float[] left, float[] right, float[] interleaved, int channels, int frames, float gain)
        {
            if (channels == 1)
            {
                for (var i = 0; i < frames; i++)
                    interleaved[i] = 0.5f * (left[i] + right[i]) * gain;
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var baseIndex = i * channels;
                interleaved[baseIndex] = left[i] * gain;
                interleaved[baseIndex + 1] = right[i] * gain;
                for (var c = 2; c < channels; c++)
                    interleaved[baseIndex + c] = 0f;
            }
        }
    }
}
