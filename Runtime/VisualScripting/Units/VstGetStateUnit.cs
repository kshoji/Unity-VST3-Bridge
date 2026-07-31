#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using System;
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Get State")]
    [UnitCategory("VST3 Host\\State")]
    [IncludeInSettings(true)]
    public sealed class VstGetStateUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput pluginId { get; private set; }
        [DoNotSerialize] public ValueOutput stateBase64 { get; private set; }
        [DoNotSerialize] public ValueOutput byteCount { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            pluginId = ValueInput<int>(nameof(pluginId), -1);
            stateBase64 = ValueOutput<string>(nameof(stateBase64));
            byteCount = ValueOutput<int>(nameof(byteCount));
        }

        ControlOutput Enter(Flow flow)
        {
            var id = flow.GetValue<int>(pluginId);
            byte[] state = null;
            if (id >= 1 && VstHostManager.Instance != null)
                state = VstHostManager.Instance.GetState(id);

            if (state == null || state.Length == 0)
            {
                flow.SetValue(stateBase64, string.Empty);
                flow.SetValue(byteCount, 0);
            }
            else
            {
                flow.SetValue(stateBase64, Convert.ToBase64String(state));
                flow.SetValue(byteCount, state.Length);
            }

            return exit;
        }
    }
}
#endif
