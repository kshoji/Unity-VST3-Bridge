#if FEATURE_VST_HOST_TIMELINE
using UnityEngine;
using UnityEngine.Playables;

namespace jp.kshoji.unity.vst3nativehost.timeline
{
    /// <summary>
    /// Mixes overlapping <see cref="VstParameterBehaviour"/> inputs by Timeline weight
    /// and writes the result to <see cref="VstHostManager.SetParameterNormalized"/>.
    /// </summary>
    public sealed class VstParameterMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as VstParameterTarget;
            if (target == null)
                return;

            var pluginId = target.PluginId;
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;

            var inputCount = playable.GetInputCount();
            // Accumulate per parameter id (clips on one track usually share one id).
            uint activeParam = 0;
            var weightedSum = 0.0;
            var weightSum = 0.0;
            var hasValue = false;

            for (var i = 0; i < inputCount; i++)
            {
                var weight = playable.GetInputWeight(i);
                if (weight <= 0.0001f)
                    continue;

                var input = (ScriptPlayable<VstParameterBehaviour>)playable.GetInput(i);
                var behaviour = input.GetBehaviour();
                if (behaviour == null)
                    continue;

                var value = behaviour.Evaluate(input);
                if (!hasValue)
                {
                    activeParam = behaviour.parameterId;
                    hasValue = true;
                }
                else if (behaviour.parameterId != activeParam)
                {
                    // Different params on one track: apply immediately then continue.
                    if (weightSum > 0.0001)
                    {
                        VstHostManager.Instance.SetParameterNormalized(
                            pluginId, activeParam, weightedSum / weightSum);
                    }

                    activeParam = behaviour.parameterId;
                    weightedSum = 0.0;
                    weightSum = 0.0;
                }

                weightedSum += value * weight;
                weightSum += weight;
            }

            if (hasValue && weightSum > 0.0001)
            {
                VstHostManager.Instance.SetParameterNormalized(
                    pluginId, activeParam, weightedSum / weightSum);
            }
        }
    }
}
#endif
