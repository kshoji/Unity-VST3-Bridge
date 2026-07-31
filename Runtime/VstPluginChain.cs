using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Processes multiple VST3 instances as a serial effect chain and/or parallel instruments.
    /// Replaces a single <see cref="VstHostAudioFilter"/> on the same GameObject (disable the filter when using a chain).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class VstPluginChain : MonoBehaviour
    {
        public enum SlotRole
        {
            Instrument = 0,
            Effect = 1,
        }

        public enum MixMode
        {
            /// <summary>All instruments in order → each effect in order (serial after mix).</summary>
            ParallelInstrumentsThenSerialEffects = 0,
            /// <summary>Slots processed strictly in list order (instrument generates / effect processes).</summary>
            StrictSerial = 1,
        }

        [Serializable]
        public struct Slot
        {
            public int pluginId;
            public SlotRole role;
            [Range(0f, 2f)] public float gain;
            public bool bypass;

            public static Slot Instrument(int pluginId, float gain = 1f) => new Slot
            {
                pluginId = pluginId,
                role = SlotRole.Instrument,
                gain = gain,
                bypass = false,
            };

            public static Slot Effect(int pluginId, float gain = 1f) => new Slot
            {
                pluginId = pluginId,
                role = SlotRole.Effect,
                gain = gain,
                bypass = false,
            };
        }

        [Serializable]
        public struct ChannelRoute
        {
            [Range(0, 15)] public int channel;
            [Tooltip("Index into Slots for the destination instrument (ignored for effects).")]
            public int slotIndex;
        }

        [SerializeField] private List<Slot> slots = new List<Slot>();
        [SerializeField] private MixMode mixMode = MixMode.ParallelInstrumentsThenSerialEffects;
        [SerializeField] private float outputGain = 1f;
        [SerializeField] private bool autoPlaySilentSource = true;
        [SerializeField] private bool flushDspMidiQueue = true;
        [Tooltip("When true, Unity filter input (e.g. upstream Chunity OnAudioFilterRead) is mixed in before instruments / as the effect bus seed.")]
        [SerializeField] private bool mixExternalInput;
        [SerializeField] private List<ChannelRoute> channelRoutes = new List<ChannelRoute>();

        private float[] planarL = Array.Empty<float>();
        private float[] planarR = Array.Empty<float>();
        private float[] mixL = Array.Empty<float>();
        private float[] mixR = Array.Empty<float>();
        private float[] tempL = Array.Empty<float>();
        private float[] tempR = Array.Empty<float>();
        private AudioSource audioSource;
        private bool warnedNotReady;
        private int armed = 1;

        // Main thread owns serialized lists; LateUpdate publishes immutable snapshots for audio.
        // Double-buffered so the audio thread can keep reading the previous array while main fills the next.
        private Slot[] slotSnapA = Array.Empty<Slot>();
        private Slot[] slotSnapB = Array.Empty<Slot>();
        private ChannelRoute[] routeSnapA = Array.Empty<ChannelRoute>();
        private ChannelRoute[] routeSnapB = Array.Empty<ChannelRoute>();
        private bool slotSnapWriteA = true;
        private bool routeSnapWriteA = true;
        private Slot[] armedSlots = Array.Empty<Slot>();
        private ChannelRoute[] armedChannelRoutes = Array.Empty<ChannelRoute>();

        /// <summary>
        /// Main-thread slot list (Inspector / <see cref="SetSlots"/>). Do not mutate from the audio thread;
        /// processing uses an armed snapshot. Prefer <see cref="SetSlots"/> / <see cref="ClearSlots"/> /
        /// <see cref="SetBypass"/> while playing (immediate arm). Direct list edits arm on LateUpdate.
        /// </summary>
        public IList<Slot> Slots => slots;

        /// <summary>
        /// Main-thread channel routes. Prefer <see cref="SetChannelRoutes"/> while playing;
        /// direct list edits are picked up on the next LateUpdate.
        /// </summary>
        public IList<ChannelRoute> ChannelRoutes => channelRoutes;

        public MixMode Mode
        {
            get => mixMode;
            set => mixMode = value;
        }

        public float OutputGain
        {
            get => outputGain;
            set => outputGain = value;
        }

        /// <summary>
        /// When true, deinterleaves the Unity filter buffer into the mix before processing
        /// (useful for Chunity / other upstream <c>OnAudioFilterRead</c> → VST effects).
        /// </summary>
        public bool MixExternalInput
        {
            get => mixExternalInput;
            set => mixExternalInput = value;
        }

        /// <summary>Clears all slots (does not unload native instances). Armed immediately for the audio thread.</summary>
        public void ClearSlots()
        {
            slots.Clear();
            PublishArmedSnapshots();
        }

        /// <summary>Replaces the slot list (does not unload native instances). Armed immediately for the audio thread.</summary>
        public void SetSlots(IEnumerable<Slot> newSlots)
        {
            slots.Clear();
            if (newSlots != null)
                slots.AddRange(newSlots);
            PublishArmedSnapshots();
        }

        /// <summary>Replaces channel routes. Armed immediately for the audio thread.</summary>
        public void SetChannelRoutes(IEnumerable<ChannelRoute> newRoutes)
        {
            channelRoutes.Clear();
            if (newRoutes != null)
                channelRoutes.AddRange(newRoutes);
            PublishArmedSnapshots();
        }

        /// <summary>Sets bypass on the first slot matching <paramref name="pluginId"/>. Armed immediately for the audio thread.</summary>
        public bool SetBypass(int pluginId, bool bypass)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.pluginId != pluginId)
                    continue;
                slot.bypass = bypass;
                slots[i] = slot;
                PublishArmedSnapshots();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves instrument plugin id for a MIDI channel (channel route → first instrument).
        /// Reads the armed snapshot (safe from DSP / MIDI scheduling threads).
        /// </summary>
        public int ResolveInstrumentPluginId(int channel)
        {
            var routes = Volatile.Read(ref armedChannelRoutes) ?? Array.Empty<ChannelRoute>();
            var slotSnap = Volatile.Read(ref armedSlots) ?? Array.Empty<Slot>();

            for (var i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route.channel != channel)
                    continue;
                if (route.slotIndex < 0 || route.slotIndex >= slotSnap.Length)
                    continue;
                var slot = slotSnap[route.slotIndex];
                if (!slot.bypass && slot.role == SlotRole.Instrument && slot.pluginId >= 1)
                    return slot.pluginId;
            }

            for (var i = 0; i < slotSnap.Length; i++)
            {
                var slot = slotSnap[i];
                if (!slot.bypass && slot.role == SlotRole.Instrument && slot.pluginId >= 1)
                    return slot.pluginId;
            }

            return -1;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            PublishArmedSnapshots();
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void OnEnable()
        {
            Volatile.Write(ref armed, 1);
            PublishArmedSnapshots();
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void OnDisable()
        {
            Volatile.Write(ref armed, 0);
        }

        public void EnsureSilentSourcePlaying()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                return;

            if (audioSource.clip == null)
            {
                const int len = 256;
                var clip = AudioClip.Create("VstHostChainSilence", len, 1, AudioSettings.outputSampleRate, false);
                var zeros = new float[len];
                clip.SetData(zeros, 0);
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        private void LateUpdate()
        {
            // Publish latest main-thread lists so Inspector / IList edits apply within one frame.
            PublishArmedSnapshots();

            if (warnedNotReady)
            {
                warnedNotReady = false;
                Debug.LogWarning("[VstPluginChain] Host not initialized; audio skipped.");
            }
        }

        /// <summary>
        /// Copies serialized lists into the inactive snap buffer and publishes the reference for the audio thread.
        /// Main thread only.
        /// </summary>
        private void PublishArmedSnapshots()
        {
            var nextSlots = CopySlotsToWriteBuffer();
            var nextRoutes = CopyRoutesToWriteBuffer();
            Volatile.Write(ref armedSlots, nextSlots);
            Volatile.Write(ref armedChannelRoutes, nextRoutes);
        }

        private Slot[] CopySlotsToWriteBuffer()
        {
            var count = slots != null ? slots.Count : 0;
            if (count == 0)
            {
                slotSnapWriteA = !slotSnapWriteA;
                return Array.Empty<Slot>();
            }

            var writeBuf = slotSnapWriteA ? slotSnapA : slotSnapB;
            if (writeBuf.Length != count)
            {
                writeBuf = new Slot[count];
                if (slotSnapWriteA)
                    slotSnapA = writeBuf;
                else
                    slotSnapB = writeBuf;
            }

            for (var i = 0; i < count; i++)
                writeBuf[i] = slots[i];

            slotSnapWriteA = !slotSnapWriteA;
            return writeBuf;
        }

        private ChannelRoute[] CopyRoutesToWriteBuffer()
        {
            var count = channelRoutes != null ? channelRoutes.Count : 0;
            if (count == 0)
            {
                routeSnapWriteA = !routeSnapWriteA;
                return Array.Empty<ChannelRoute>();
            }

            var writeBuf = routeSnapWriteA ? routeSnapA : routeSnapB;
            if (writeBuf.Length != count)
            {
                writeBuf = new ChannelRoute[count];
                if (routeSnapWriteA)
                    routeSnapA = writeBuf;
                else
                    routeSnapB = writeBuf;
            }

            for (var i = 0; i < count; i++)
                writeBuf[i] = channelRoutes[i];

            routeSnapWriteA = !routeSnapWriteA;
            return writeBuf;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref armed) == 0 || data == null || data.Length == 0 || channels <= 0)
                return;

            var host = VstHostManager.Instance;
            if (host == null || !host.IsInitialized)
            {
                warnedNotReady = true;
                return;
            }

            var frames = data.Length / channels;
            if (frames <= 0 || frames > host.BlockSize)
                return;

            EnsureCapacity(frames);

            var sampleRate = host.SampleRate > 0 ? host.SampleRate : AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
                sampleRate = 48000;
            var blockEnd = (long)(AudioSettings.dspTime * sampleRate) + frames;

            if (flushDspMidiQueue)
                VstHostDspMidiQueue.Shared.FlushDue(blockEnd);

            ProcessBlock(host, frames, data, channels);
        }

        private void ProcessBlock(VstHostManager host, int frames, float[] data, int channels)
        {
            if (mixExternalInput)
                Deinterleave(data, channels, frames, mixL, mixR);
            else
            {
                Array.Clear(mixL, 0, frames);
                Array.Clear(mixR, 0, frames);
            }

            ProcessBlockIntoPlanar(host, frames, seedFromExternal: mixExternalInput);
            InterleaveReplace(planarL, planarR, data, channels, frames, outputGain);
        }

        private void ProcessBlockIntoPlanar(VstHostManager host, int frames, bool seedFromExternal)
        {
            if (!seedFromExternal)
            {
                Array.Clear(mixL, 0, frames);
                Array.Clear(mixR, 0, frames);
            }

            var slotSnap = Volatile.Read(ref armedSlots) ?? Array.Empty<Slot>();

            if (mixMode == MixMode.StrictSerial)
            {
                if (seedFromExternal)
                    CopyBuffer(mixL, mixR, tempL, tempR, frames);
                else
                {
                    Array.Clear(tempL, 0, frames);
                    Array.Clear(tempR, 0, frames);
                }

                var hasSignal = seedFromExternal;

                for (var i = 0; i < slotSnap.Length; i++)
                {
                    var slot = slotSnap[i];
                    if (slot.bypass || slot.pluginId < 1)
                        continue;

                    if (slot.role == SlotRole.Instrument)
                    {
                        Array.Clear(planarL, 0, frames);
                        Array.Clear(planarR, 0, frames);
                        if (!host.Process(slot.pluginId, null, null, planarL, planarR, frames))
                            continue;
                        ScaleBuffer(planarL, planarR, frames, slot.gain);
                        if (!hasSignal)
                        {
                            CopyBuffer(planarL, planarR, tempL, tempR, frames);
                            hasSignal = true;
                        }
                        else
                        {
                            AddBuffer(planarL, planarR, tempL, tempR, frames);
                        }
                    }
                    else
                    {
                        if (!hasSignal)
                            continue;
                        if (!host.Process(slot.pluginId, tempL, tempR, planarL, planarR, frames))
                            continue;
                        ScaleBuffer(planarL, planarR, frames, slot.gain);
                        CopyBuffer(planarL, planarR, tempL, tempR, frames);
                    }
                }

                if (hasSignal)
                    CopyBuffer(tempL, tempR, mixL, mixR, frames);
            }
            else
            {
                for (var i = 0; i < slotSnap.Length; i++)
                {
                    var slot = slotSnap[i];
                    if (slot.bypass || slot.pluginId < 1 || slot.role != SlotRole.Instrument)
                        continue;

                    Array.Clear(planarL, 0, frames);
                    Array.Clear(planarR, 0, frames);
                    if (!host.Process(slot.pluginId, null, null, planarL, planarR, frames))
                        continue;
                    ScaleBuffer(planarL, planarR, frames, slot.gain);
                    AddBuffer(planarL, planarR, mixL, mixR, frames);
                }

                CopyBuffer(mixL, mixR, tempL, tempR, frames);
                for (var i = 0; i < slotSnap.Length; i++)
                {
                    var slot = slotSnap[i];
                    if (slot.bypass || slot.pluginId < 1 || slot.role != SlotRole.Effect)
                        continue;

                    if (!host.Process(slot.pluginId, tempL, tempR, planarL, planarR, frames))
                        continue;
                    ScaleBuffer(planarL, planarR, frames, slot.gain);
                    CopyBuffer(planarL, planarR, tempL, tempR, frames);
                }

                CopyBuffer(tempL, tempR, mixL, mixR, frames);
            }

            CopyBuffer(mixL, mixR, planarL, planarR, frames);
        }

        private void EnsureCapacity(int frames)
        {
            if (planarL.Length < frames) planarL = new float[frames];
            if (planarR.Length < frames) planarR = new float[frames];
            if (mixL.Length < frames) mixL = new float[frames];
            if (mixR.Length < frames) mixR = new float[frames];
            if (tempL.Length < frames) tempL = new float[frames];
            if (tempR.Length < frames) tempR = new float[frames];
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

        private static void ScaleBuffer(float[] l, float[] r, int frames, float gain)
        {
            if (Mathf.Approximately(gain, 1f))
                return;
            for (var i = 0; i < frames; i++)
            {
                l[i] *= gain;
                r[i] *= gain;
            }
        }

        private static void AddBuffer(float[] srcL, float[] srcR, float[] dstL, float[] dstR, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                dstL[i] += srcL[i];
                dstR[i] += srcR[i];
            }
        }

        private static void CopyBuffer(float[] srcL, float[] srcR, float[] dstL, float[] dstR, int frames)
        {
            Array.Copy(srcL, 0, dstL, 0, frames);
            Array.Copy(srcR, 0, dstR, 0, frames);
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
