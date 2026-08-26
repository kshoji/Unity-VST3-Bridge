#nullable enable
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.core
{
    /// <summary>
    /// Play Mode / running-build session detection shared by Editor and Runtime MCP tools.
    /// </summary>
    public static class McpExecutionContext
    {
        /// <summary>True when audio Process / note playback is allowed.</summary>
        public static bool IsAudioSessionActive => Application.isPlaying;

#if UNITY_EDITOR
        public static bool IsEditorPlayMode => UnityEditor.EditorApplication.isPlaying;
#else
        public static bool IsEditorPlayMode => false;
#endif

        public static string SessionKind =>
#if UNITY_EDITOR
            IsEditorPlayMode ? "playMode" : "editMode";
#else
            "runtime";
#endif
    }
}
