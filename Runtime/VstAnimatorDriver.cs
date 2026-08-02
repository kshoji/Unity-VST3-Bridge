using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Bidirectional bridge between VST3 parameters and Animator float parameters.
    /// Does not modify MIDI <c>MidiAnimatorMapping</c>; keep VST bindings in this component / asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private VstParameterTarget target;
        [SerializeField] private Animator animator;
        [SerializeField] private VstAnimatorMapping mapping;
        [SerializeField] private VstAnimatorBinding[] bindings = System.Array.Empty<VstAnimatorBinding>();

        private readonly Dictionary<int, float> smoothed = new Dictionary<int, float>();

        public VstParameterTarget Target
        {
            get => target;
            set => target = value;
        }

        public Animator Animator
        {
            get => animator != null ? animator : animator = GetComponent<Animator>();
            set => animator = value;
        }

        private void Reset()
        {
            target = GetComponent<VstParameterTarget>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            var pluginId = target != null ? target.PluginId : -1;
            var anim = Animator;
            if (pluginId < 1 || anim == null || !VstHostManager.Instance.IsInitialized)
                return;

            var list = GetBindings();
            if (list == null)
                return;

            for (var i = 0; i < list.Length; i++)
            {
                var binding = list[i];
                if (binding == null || string.IsNullOrEmpty(binding.animatorParameterName))
                    continue;

                if (binding.direction == VstAnimatorDirection.VstToAnimator)
                    ApplyVstToAnimator(pluginId, anim, binding, i);
                else
                    ApplyAnimatorToVst(pluginId, anim, binding, i);
            }
        }

        private VstAnimatorBinding[] GetBindings()
        {
            if (mapping != null && mapping.bindings != null && mapping.bindings.Length > 0)
                return mapping.bindings;
            return bindings;
        }

        private void ApplyVstToAnimator(int pluginId, Animator anim, VstAnimatorBinding binding, int index)
        {
            if (!VstHostManager.Instance.TryGetParameterNormalized(pluginId, binding.parameterId, out var raw))
                return;

            var mapped = MapValue((float)raw, binding);
            mapped = Smooth(index, mapped, binding.smoothingTime);
            anim.SetFloat(binding.animatorParameterName, mapped);
        }

        private void ApplyAnimatorToVst(int pluginId, Animator anim, VstAnimatorBinding binding, int index)
        {
            var raw = anim.GetFloat(binding.animatorParameterName);
            var mapped = MapValue(raw, binding);
            mapped = Smooth(index, mapped, binding.smoothingTime);
            VstHostManager.Instance.SetParameterNormalized(pluginId, binding.parameterId, mapped);
        }

        private static float MapValue(float t01, VstAnimatorBinding binding)
        {
            var t = Mathf.Clamp01(t01);
            if (binding.invert)
                t = 1f - t;
            return binding.responseCurve != null ? binding.responseCurve.Evaluate(t) : t;
        }

        private float Smooth(int index, float targetValue, float smoothingTime)
        {
            if (smoothingTime <= 0f)
            {
                smoothed[index] = targetValue;
                return targetValue;
            }

            if (!smoothed.TryGetValue(index, out var current))
                current = targetValue;

            var factor = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothingTime));
            current = Mathf.Lerp(current, targetValue, factor);
            smoothed[index] = current;
            return current;
        }
    }
}
