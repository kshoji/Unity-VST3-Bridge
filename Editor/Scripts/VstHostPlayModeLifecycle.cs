using UnityEditor;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Editor keeps native plugins loaded across Play Mode. Terminate on exit so the next
    /// session can call <see cref="VstHostManager.Initialize"/> cleanly.
    /// </summary>
    [InitializeOnLoad]
    static class VstHostPlayModeLifecycle
    {
        static VstHostPlayModeLifecycle()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
                return;

            VstHostManager.Instance.Terminate();
        }
    }
}
