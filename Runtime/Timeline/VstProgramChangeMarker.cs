#if FEATURE_VST_HOST_TIMELINE
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>
    /// Timeline marker that switches a VST host program (or sends MIDI Program Change).
    /// Requires <see cref="VstTimelineNotificationReceiver"/> on the PlayableDirector object.
    /// </summary>
    [CustomStyle("SignalEmitter")]
    public sealed class VstProgramChangeMarker : Marker, INotification, INotificationOptionProvider
    {
        [Tooltip("When set, uses this target. Otherwise the notification receiver's default target is used.")]
        public VstParameterTarget target;

        [Tooltip("Host program index for VstHostManager.SetProgram.")]
        public int programIndex;

        [Tooltip("When true, also send MIDI Program Change on channel.")]
        public bool alsoSendMidiProgramChange;

        [Range(0, 15)]
        public int midiChannel;

        public PropertyName id => new PropertyName("VstProgramChangeMarker");

        public NotificationFlags flags =>
            NotificationFlags.TriggerInEditMode | NotificationFlags.Retroactive;

        public void Apply(VstParameterTarget fallbackTarget)
        {
            var resolved = target != null ? target : fallbackTarget;
            if (resolved == null)
                return;

            var pluginId = resolved.PluginId;
            if (pluginId < 1)
                return;

            VstHostManager.Instance.SetProgram(pluginId, programIndex);
            if (alsoSendMidiProgramChange)
                VstHostManager.Instance.ProgramChange(pluginId, midiChannel, programIndex);
        }
    }
}
#endif
