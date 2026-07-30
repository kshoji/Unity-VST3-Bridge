#if FEATURE_VST_HOST_TIMELINE
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>
    /// Timeline clip that evaluates a normalized VST parameter curve over the clip duration.
    /// Curve X is normalized clip time (0–1); Y is normalized parameter value (0–1).
    /// </summary>
    public sealed class VstParameterClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("VST3 parameter id from VstHostManager.GetParameters.")]
        public uint parameterId;

        [Tooltip("Normalized value curve. X = clip local time 0–1, Y = parameter 0–1.")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<VstParameterBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.parameterId = parameterId;
            behaviour.curve = curve;
            return playable;
        }
    }
}
#endif
