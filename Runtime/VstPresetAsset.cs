using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// ScriptableObject that stores an opaque VST3 plugin state blob from
    /// <see cref="VstHostManager.GetState"/> / <see cref="VstHostManager.SetState"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "VstPreset", menuName = "VST3 Host/Preset")]
    public sealed class VstPresetAsset : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string pluginName;
        [SerializeField] private string pluginUid;
        [SerializeField] private string pluginFilePath;
        [SerializeField] private int programIndex = -1;
        [SerializeField] private string programName;
        [SerializeField] [TextArea] private string notes;
        [SerializeField] private byte[] state = Array.Empty<byte>();

        public string DisplayName
        {
            get => string.IsNullOrEmpty(displayName) ? name : displayName;
            set => displayName = value;
        }

        public string PluginName
        {
            get => pluginName;
            set => pluginName = value;
        }

        public string PluginUid
        {
            get => pluginUid;
            set => pluginUid = value;
        }

        public string PluginFilePath
        {
            get => pluginFilePath;
            set => pluginFilePath = value;
        }

        public int ProgramIndex
        {
            get => programIndex;
            set => programIndex = value;
        }

        public string ProgramName
        {
            get => programName;
            set => programName = value;
        }

        public string Notes
        {
            get => notes;
            set => notes = value;
        }

        public byte[] State
        {
            get => state;
            set => state = value ?? Array.Empty<byte>();
        }

        public bool HasState => state != null && state.Length > 0;

        /// <summary>
        /// Captures the current plugin state into this asset.
        /// Optionally stamps metadata from <see cref="VstHostManager.LoadedPlugins"/>.
        /// </summary>
        public bool CaptureFrom(int pluginId, string overrideDisplayName = null)
        {
            var host = VstHostManager.Instance;
            if (!host.IsInitialized || pluginId < 1)
                return false;

            var blob = host.GetState(pluginId);
            if (blob == null)
                return false;

            state = blob;
            if (!string.IsNullOrEmpty(overrideDisplayName))
                displayName = overrideDisplayName;

            if (host.LoadedPlugins.TryGetValue(pluginId, out var info))
            {
                pluginFilePath = info.FilePath;
                pluginUid = info.Uid;
            }

            return true;
        }

        /// <summary>Applies stored state to a live plugin instance.</summary>
        public bool ApplyTo(int pluginId)
        {
            if (!HasState || pluginId < 1)
                return false;
            return VstHostManager.Instance.SetState(pluginId, state);
        }
    }
}
