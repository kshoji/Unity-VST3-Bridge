#if UNITY_6000_3_OR_NEWER && !UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
using jp.kshoji.unity.vst3nativehost;
using Unity.IntegerTime;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Audio.ProcessorInstance;

namespace jp.kshoji.unity.vst3nativehost.scriptableaudio
{
    /// <summary>
    /// Scriptable Audio <see cref="IAudioGenerator"/> that renders a VST3 instrument via
    /// <see cref="VstHostNative.VstHost_Process"/>. Timed MIDI is drained from
    /// <see cref="VstHostDspMidiQueue.Shared"/> at DSP block boundaries.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class VstHostGenerator : MonoBehaviour, IAudioGenerator
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] [Range(0f, 2f)] private float gain = 1f;
        [SerializeField] private bool autoSetupOnAwake = true;
        [SerializeField] private bool autoPlayOnAwake = true;
        [SerializeField] private bool flushDspMidiQueue = true;

        private AudioSource audioSource;

        public bool isFinite => false;
        public bool isRealtime => true;
        public DiscreteTime? length => null;

        public int PluginId
        {
            get => pluginId;
            set => pluginId = value;
        }

        public float Gain
        {
            get => gain;
            set => gain = value;
        }

        private void Awake()
        {
            if (autoSetupOnAwake)
                SetupAudioSource();
        }

        public AudioSource SetupAudioSource()
        {
            if (!TryGetComponent(out audioSource))
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.generator = this;

            if (autoPlayOnAwake && Application.isPlaying && !audioSource.isPlaying)
                audioSource.Play();

            return audioSource;
        }

        public GeneratorInstance CreateInstance(
            ControlContext context,
            AudioFormat? nestedConfiguration,
            ProcessorInstance.CreationParameters creationParameters)
        {
            var control = new VstHostGeneratorControl
            {
                pluginId = pluginId,
                gain = gain,
                flushMidi = flushDspMidiQueue ? (byte)1 : (byte)0,
            };
            return context.AllocateGenerator(new VstHostGeneratorRealtime(), control, nestedConfiguration, creationParameters);
        }
    }

    internal struct VstHostGeneratorControl : GeneratorInstance.IControl<VstHostGeneratorRealtime>
    {
        internal int pluginId;
        internal float gain;
        internal byte flushMidi;

        public Response OnMessage(ControlContext context, Pipe pipe, Message message)
        {
            return Response.Unhandled;
        }

        public void Configure(
            ControlContext context,
            ref VstHostGeneratorRealtime realtime,
            in AudioFormat format,
            out GeneratorInstance.Setup setup,
            ref GeneratorInstance.Properties properties)
        {
            realtime.DisposeBuffers();
            realtime.pluginId = pluginId;
            realtime.gain = gain;
            realtime.flushMidi = flushMidi;
            realtime.sampleRate = format.sampleRate > 0 ? format.sampleRate : 48000;
            realtime.AllocateBuffers(Mathf.Max(2048, realtime.sampleRate / 10));
            setup = new GeneratorInstance.Setup(AudioSpeakerMode.Stereo, realtime.sampleRate);
        }

        public void Update(ControlContext context, Pipe pipe)
        {
        }

        public void Dispose(ControlContext context, ref VstHostGeneratorRealtime realtime)
        {
            realtime.DisposeBuffers();
        }
    }

    internal struct VstHostGeneratorRealtime : GeneratorInstance.IRealtime
    {
        internal int pluginId;
        internal float gain;
        internal byte flushMidi;
        internal int sampleRate;
        internal int bufferCapacity;
        internal IntPtr bufferL;
        internal IntPtr bufferR;

        public bool isFinite => false;
        public bool isRealtime => true;
        public DiscreteTime? length => null;

        public void AllocateBuffers(int capacity)
        {
            bufferCapacity = capacity;
            var bytes = capacity * sizeof(float);
            bufferL = Marshal.AllocHGlobal(bytes);
            bufferR = Marshal.AllocHGlobal(bytes);
        }

        public void DisposeBuffers()
        {
            if (bufferL != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(bufferL);
                bufferL = IntPtr.Zero;
            }

            if (bufferR != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(bufferR);
                bufferR = IntPtr.Zero;
            }

            bufferCapacity = 0;
        }

        public void Update(UpdatedDataContext context, Pipe pipe)
        {
        }

        public unsafe GeneratorInstance.Result Process(
            in RealtimeContext context,
            Pipe pipe,
            ChannelBuffer buffer,
            GeneratorInstance.Arguments args)
        {
            var frames = buffer.frameCount;
            if (frames <= 0 || pluginId < 1 || bufferL == IntPtr.Zero || bufferR == IntPtr.Zero)
                return 0;

            if (frames > bufferCapacity)
                frames = bufferCapacity;

            if (flushMidi != 0)
            {
                var blockEnd = (long)context.dspTime + frames;
                VstHostDspMidiQueue.Shared.FlushDue(blockEnd);
            }

            var ptrL = (float*)bufferL.ToPointer();
            var ptrR = (float*)bufferR.ToPointer();
            for (var i = 0; i < frames; i++)
            {
                ptrL[i] = 0f;
                ptrR[i] = 0f;
            }

            var result = VstHostNative.VstHost_Process(pluginId, null, null, ptrL, ptrR, frames);
            if (result != VstHostResult.Ok)
                return 0;

            var g = gain;
            var channels = buffer.channelCount;
            for (var frame = 0; frame < frames; frame++)
            {
                var l = ptrL[frame] * g;
                var r = ptrR[frame] * g;
                if (channels <= 1)
                {
                    buffer[0, frame] = 0.5f * (l + r);
                }
                else
                {
                    buffer[0, frame] = l;
                    buffer[1, frame] = r;
                    for (var ch = 2; ch < channels; ch++)
                        buffer[ch, frame] = 0f;
                }
            }

            return frames;
        }
    }
}
#endif
