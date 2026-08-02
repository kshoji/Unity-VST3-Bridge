using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Shared Play Mode preview AudioSource + <see cref="VstHostAudioFilter"/> used by
    /// Plugin Browser and Virtual Controller.
    /// </summary>
    internal static class VstHostEditorPreview
    {
        public const string PreviewObjectName = "__VstHostEditorPreview";

        /// <summary>Last plugin id loaded or selected by editor tools (Play Mode).</summary>
        public static int LastPluginId { get; private set; } = -1;

        public static void RememberPluginId(int pluginId)
        {
            LastPluginId = pluginId;
        }

        public static void Clear()
        {
            LastPluginId = -1;
            var go = GameObject.Find(PreviewObjectName);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Ensures a DontSave preview object routes audio for <paramref name="pluginId"/>.
        /// Only creates / updates while playing.
        /// </summary>
        public static VstHostAudioFilter EnsureAudio(int pluginId)
        {
            if (!Application.isPlaying || pluginId < 1)
                return null;

            RememberPluginId(pluginId);

            var go = GameObject.Find(PreviewObjectName);
            if (go == null)
            {
                go = new GameObject(PreviewObjectName);
                go.hideFlags = HideFlags.DontSave;
            }

            var filter = go.GetComponent<VstHostAudioFilter>();
            if (filter == null)
                filter = go.AddComponent<VstHostAudioFilter>();

            filter.Mode = VstHostAudioFilter.ProcessMode.Instrument;
            if (filter.PluginId != pluginId)
                filter.AttachPlugin(pluginId);
            filter.EnsureSilentSourcePlaying();
            return filter;
        }
    }
}
