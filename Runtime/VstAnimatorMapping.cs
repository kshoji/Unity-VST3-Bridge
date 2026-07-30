using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>Direction of a VST ↔ Animator binding.</summary>
    public enum VstAnimatorDirection
    {
        /// <summary>Read VST parameter → write Animator float.</summary>
        VstToAnimator = 0,
        /// <summary>Read Animator float → write VST parameter.</summary>
        AnimatorToVst = 1,
    }

    /// <summary>One VST parameter ↔ Animator float binding.</summary>
    [Serializable]
    public sealed class VstAnimatorBinding
    {
        public string label;
        public uint parameterId;
        public string animatorParameterName;
        public VstAnimatorDirection direction = VstAnimatorDirection.VstToAnimator;
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool invert;
        [Tooltip("Smoothing time in seconds. 0 = immediate.")]
        public float smoothingTime;
    }

    /// <summary>ScriptableObject mapping for <see cref="VstAnimatorDriver"/>.</summary>
    [CreateAssetMenu(fileName = "VstAnimatorMapping", menuName = "VST3 Host/Animator Mapping")]
    public sealed class VstAnimatorMapping : ScriptableObject
    {
        public VstAnimatorBinding[] bindings = Array.Empty<VstAnimatorBinding>();
    }
}
