using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Timeline / Animator / Input binding target that resolves a VST plugin instance id.
    /// Prefer assigning <see cref="audioFilter"/> so the id stays in sync with audio output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstParameterTarget : MonoBehaviour
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] private VstHostAudioFilter audioFilter;

        public int PluginId
        {
            get
            {
                if (audioFilter != null)
                {
                    var fromFilter = audioFilter.PluginId;
                    if (fromFilter >= 1)
                        return fromFilter;
                }

                return pluginId;
            }
            set
            {
                pluginId = value;
                if (audioFilter != null)
                    audioFilter.PluginId = value;
            }
        }

        public VstHostAudioFilter AudioFilter
        {
            get => audioFilter;
            set => audioFilter = value;
        }

        private void Reset()
        {
            audioFilter = GetComponent<VstHostAudioFilter>();
        }

        private void OnValidate()
        {
            if (audioFilter == null)
                audioFilter = GetComponent<VstHostAudioFilter>();
        }
    }
}
