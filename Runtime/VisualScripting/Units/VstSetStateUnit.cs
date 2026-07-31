#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using System;
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Set State")]
    [UnitCategory("VST3 Host\\State")]
    [IncludeInSettings(true)]
    public sealed class VstSetStateUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput pluginId { get; private set; }
        [DoNotSerialize] public ValueInput stateBase64 { get; private set; }
        [DoNotSerialize] public ValueOutput success { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            pluginId = ValueInput<int>(nameof(pluginId), -1);
            stateBase64 = ValueInput<string>(nameof(stateBase64), string.Empty);
            success = ValueOutput<bool>(nameof(success));
        }

        ControlOutput Enter(Flow flow)
        {
            var id = flow.GetValue<int>(pluginId);
            var b64 = flow.GetValue<string>(stateBase64);
            var ok = false;
            if (id >= 1 && VstHostManager.Instance != null && !string.IsNullOrEmpty(b64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(b64);
                    ok = VstHostManager.Instance.SetState(id, bytes);
                }
                catch (FormatException)
                {
                    ok = false;
                }
            }

            flow.SetValue(success, ok);
            return exit;
        }
    }
}
#endif
