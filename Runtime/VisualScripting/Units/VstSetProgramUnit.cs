#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Set Program")]
    [UnitCategory("VST3 Host\\Parameters")]
    [IncludeInSettings(true)]
    public sealed class VstSetProgramUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput pluginId { get; private set; }
        [DoNotSerialize] public ValueInput programIndex { get; private set; }
        [DoNotSerialize] public ValueOutput success { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            pluginId = ValueInput<int>(nameof(pluginId), -1);
            programIndex = ValueInput<int>(nameof(programIndex), 0);
            success = ValueOutput<bool>(nameof(success));
        }

        ControlOutput Enter(Flow flow)
        {
            var id = flow.GetValue<int>(pluginId);
            var ok = id >= 1 && VstHostManager.Instance != null
                && VstHostManager.Instance.SetProgram(id, flow.GetValue<int>(programIndex));
            flow.SetValue(success, ok);
            return exit;
        }
    }
}
#endif
