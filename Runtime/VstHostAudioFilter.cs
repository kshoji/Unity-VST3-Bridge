using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Path B: call native <c>VstHost_Process</c> from Unity's audio thread
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

        private float[] planarL = Array.Empty<float>();
        private float[] planarR = Array.Empty<float>();
        private AudioSource audioSource;
        private bool warnedNotReady;

        public int PluginId
        {
            get => pluginId;
            set => pluginId = value;
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

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (pluginId < 1 || data == null || data.Length == 0 || channels <= 0)
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
                return;
            }

            EnsurePlanarCapacity(frames);

            if (mode == ProcessMode.Effect)
            {
                Deinterleave(data, channels, frames, planarL, planarR);
                if (!host.Process(pluginId, planarL, planarR, planarL, planarR, frames))
                    return;
            }
            else
            {
                Array.Clear(planarL, 0, frames);
                Array.Clear(planarR, 0, frames);
                if (!host.Process(pluginId, null, null, planarL, planarR, frames))
                    return;
            }

            InterleaveReplace(planarL, planarR, data, channels, frames, outputGain);
        }

        private void LateUpdate()
        {
            if (!warnedNotReady)
                return;
            warnedNotReady = false;
            Debug.LogWarning("[VstHostAudioFilter] Host not initialized; audio skipped.");
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
