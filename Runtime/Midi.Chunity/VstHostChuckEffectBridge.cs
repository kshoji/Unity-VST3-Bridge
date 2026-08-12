#if FEATURE_MIDI_PLUGIN && FEATURE_CHUNITY
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Configures a <see cref="VstHostAudioFilter"/> (Effect) or <see cref="VstAudioGraph"/>
    /// to process upstream audio (typically Chunity <c>OnAudioFilterRead</c> on the same GameObject).
    /// Place this component / the VST filter <b>below</b> the Chuck instance in the Inspector
    /// so Unity runs Chuck first, then the VST effect.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class VstHostChuckEffectBridge : MonoBehaviour
    {
        public enum TargetKind
        {
            AudioFilterEffect = 0,
            AudioGraphExternalInput = 1,
        }

        [SerializeField] private TargetKind target = TargetKind.AudioFilterEffect;
        [SerializeField] private int effectPluginId = -1;
        [SerializeField] private VstHostAudioFilter audioFilter;
        [SerializeField] private VstAudioGraph audioGraph;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] [Range(0f, 2f)] private float outputGain = 1f;

        public int EffectPluginId
        {
            get => effectPluginId;
            set => effectPluginId = value;
        }

        private void Reset()
        {
            audioFilter = GetComponent<VstHostAudioFilter>();
            audioGraph = GetComponent<VstAudioGraph>();
        }

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        public void Apply()
        {
            if (target == TargetKind.AudioFilterEffect)
            {
                if (audioFilter == null)
                    audioFilter = GetComponent<VstHostAudioFilter>();
                if (audioFilter == null)
                    audioFilter = gameObject.AddComponent<VstHostAudioFilter>();

                if (audioGraph != null)
                    audioGraph.enabled = false;

                audioFilter.enabled = true;
                audioFilter.Mode = VstHostAudioFilter.ProcessMode.Effect;
                audioFilter.OutputGain = outputGain;
                if (effectPluginId >= 1)
                    audioFilter.AttachPlugin(effectPluginId);
                audioFilter.EnsureSilentSourcePlaying();
                return;
            }

            if (audioGraph == null)
                audioGraph = GetComponent<VstAudioGraph>();
            if (audioGraph == null)
                audioGraph = gameObject.AddComponent<VstAudioGraph>();

            if (audioFilter != null)
                audioFilter.enabled = false;

            audioGraph.enabled = true;
            audioGraph.OutputGain = outputGain;
            if (effectPluginId >= 1)
            {
                audioGraph.BuildParallelInstrumentsThenSerialEffects(
                    System.Array.Empty<int>(),
                    new[] { effectPluginId },
                    mixExternalInput: true);
            }

            audioGraph.EnsureSilentSourcePlaying();
        }
    }
}
#endif
