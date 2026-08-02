#if FEATURE_MIDI_PLUGIN && FEATURE_CHUNITY
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Configures a <see cref="VstHostAudioFilter"/> (Effect) or <see cref="VstPluginChain"/>
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
            PluginChainExternalInput = 1,
        }

        [SerializeField] private TargetKind target = TargetKind.AudioFilterEffect;
        [SerializeField] private int effectPluginId = -1;
        [SerializeField] private VstHostAudioFilter audioFilter;
        [SerializeField] private VstPluginChain pluginChain;
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
            pluginChain = GetComponent<VstPluginChain>();
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

                if (pluginChain != null)
                    pluginChain.enabled = false;

                audioFilter.enabled = true;
                audioFilter.Mode = VstHostAudioFilter.ProcessMode.Effect;
                audioFilter.OutputGain = outputGain;
                if (effectPluginId >= 1)
                    audioFilter.AttachPlugin(effectPluginId);
                audioFilter.EnsureSilentSourcePlaying();
                return;
            }

            if (pluginChain == null)
                pluginChain = GetComponent<VstPluginChain>();
            if (pluginChain == null)
                pluginChain = gameObject.AddComponent<VstPluginChain>();

            if (audioFilter != null)
                audioFilter.enabled = false;

            pluginChain.enabled = true;
            pluginChain.MixExternalInput = true;
            pluginChain.OutputGain = outputGain;
            if (effectPluginId >= 1)
            {
                pluginChain.SetSlots(new[]
                {
                    VstPluginChain.Slot.Effect(effectPluginId),
                });
            }

            pluginChain.EnsureSilentSourcePlaying();
        }
    }
}
#endif
