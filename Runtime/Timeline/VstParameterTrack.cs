#if FEATURE_VST_HOST_TIMELINE
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>
    /// Timeline track that drives a VST3 parameter via <see cref="VstParameterTarget"/>.
    /// Place alongside MIDI playback tracks on the same Timeline for synced automation.
    /// </summary>
    [TrackColor(0.35f, 0.55f, 0.95f)]
    [TrackClipType(typeof(VstParameterClip))]
    [TrackBindingType(typeof(VstParameterTarget))]
    public sealed class VstParameterTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<VstParameterMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
#endif
