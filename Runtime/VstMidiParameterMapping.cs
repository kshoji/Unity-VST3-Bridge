using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// ScriptableObject that maps MIDI CC / pitch bend to VST3 parameter IDs.
    /// Used by <c>VstHostMidiParameterMapper</c> (MIDI optional assembly).
    /// </summary>
    [CreateAssetMenu(
        fileName = "VstMidiParameterMapping",
        menuName = "VST3 Host/MIDI Parameter Mapping")]
    public sealed class VstMidiParameterMapping : ScriptableObject
    {
        public enum SourceType
        {
            ControlChange = 0,
            FourteenBitControlChange = 1,
            PitchBend = 2,
        }

        [Serializable]
        public struct Binding
        {
            [Tooltip("MIDI source kind.")]
            public SourceType source;

            [Tooltip("MIDI channel 0–15. Use -1 for all channels.")]
            public int channel;

            [Tooltip("CC number (0–127). For 14-bit, this is the MSB (0–31). Ignored for PitchBend.")]
            [Range(0, 127)]
            public int controller;

            [Tooltip("LSB CC for 14-bit (typically MSB + 32). Ignored unless source is FourteenBitControlChange.")]
            [Range(0, 127)]
            public int controllerLsb;

            [Tooltip("VST3 parameter id from VstHostManager.GetParameters.")]
            public uint parameterId;

            [Tooltip("When true, mapped value is inverted (1 - t).")]
            public bool invert;

            [Tooltip("Normalized output range minimum (0–1).")]
            [Range(0f, 1f)]
            public float minNormalized;

            [Tooltip("Normalized output range maximum (0–1).")]
            [Range(0f, 1f)]
            public float maxNormalized;

            public float MapNormalized(float t01)
            {
                var t = Mathf.Clamp01(t01);
                if (invert)
                    t = 1f - t;
                var min = Mathf.Clamp01(minNormalized);
                var max = Mathf.Clamp01(maxNormalized);
                if (max < min)
                    (min, max) = (max, min);
                return Mathf.Lerp(min, max, t);
            }

            public static Binding CreateDefault(SourceType source, int controller, uint parameterId)
            {
                return new Binding
                {
                    source = source,
                    channel = -1,
                    controller = controller,
                    controllerLsb = controller <= 31 ? controller + 32 : controller,
                    parameterId = parameterId,
                    invert = false,
                    minNormalized = 0f,
                    maxNormalized = 1f,
                };
            }
        }

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        /// <summary>Binding list (editable in Inspector / MIDI Learn).</summary>
        public Binding[] Bindings
        {
            get => bindings;
            set => bindings = value ?? Array.Empty<Binding>();
        }

        /// <summary>Adds or replaces a CC binding for the given controller / channel.</summary>
        public void UpsertControlChangeBinding(int channel, int controller, uint parameterId, bool fourteenBit = false)
        {
            var list = bindings != null ? new System.Collections.Generic.List<Binding>(bindings) : new System.Collections.Generic.List<Binding>();
            var source = fourteenBit ? SourceType.FourteenBitControlChange : SourceType.ControlChange;
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.source != source)
                    continue;
                if (b.controller != controller)
                    continue;
                if (b.channel != channel && b.channel != -1 && channel != -1)
                    continue;

                b.parameterId = parameterId;
                b.channel = channel;
                if (fourteenBit && b.controllerLsb == 0 && controller <= 31)
                    b.controllerLsb = controller + 32;
                list[i] = b;
                bindings = list.ToArray();
                return;
            }

            var created = Binding.CreateDefault(source, controller, parameterId);
            created.channel = channel;
            list.Add(created);
            bindings = list.ToArray();
        }

        /// <summary>Adds or replaces a pitch-bend binding.</summary>
        public void UpsertPitchBendBinding(int channel, uint parameterId)
        {
            var list = bindings != null ? new System.Collections.Generic.List<Binding>(bindings) : new System.Collections.Generic.List<Binding>();
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.source != SourceType.PitchBend)
                    continue;
                if (b.channel != channel && b.channel != -1 && channel != -1)
                    continue;

                b.parameterId = parameterId;
                b.channel = channel;
                list[i] = b;
                bindings = list.ToArray();
                return;
            }

            var created = Binding.CreateDefault(SourceType.PitchBend, 0, parameterId);
            created.channel = channel;
            list.Add(created);
            bindings = list.ToArray();
        }
    }
}
