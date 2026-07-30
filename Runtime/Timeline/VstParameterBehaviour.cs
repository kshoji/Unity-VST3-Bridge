#if FEATURE_VST_HOST_TIMELINE
using UnityEngine;
using UnityEngine.Playables;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>Per-clip evaluation state for <see cref="VstParameterClip"/>.</summary>
    public sealed class VstParameterBehaviour : PlayableBehaviour
    {
        public uint parameterId;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float Evaluate(Playable playable)
        {
            var duration = playable.GetDuration();
            if (duration <= 0.0001)
                return curve != null ? curve.Evaluate(0f) : 0f;

            var t = (float)(playable.GetTime() / duration);
            return curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : 0f;
        }
    }
}
#endif
