#if FEATURE_USE_TIMELINE
using UnityEngine;
using UnityEngine.Playables;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>
    /// Receives <see cref="VstProgramChangeMarker"/> notifications.
    /// Attach to the same GameObject as <see cref="PlayableDirector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstTimelineNotificationReceiver : MonoBehaviour, INotificationReceiver
    {
        [SerializeField] private VstParameterTarget defaultTarget;

        public VstParameterTarget DefaultTarget
        {
            get => defaultTarget;
            set => defaultTarget = value;
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is VstProgramChangeMarker marker)
                marker.Apply(defaultTarget);
        }
    }
}
#endif
