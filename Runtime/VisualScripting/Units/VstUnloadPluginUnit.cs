#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Unload Plugin")]
    [UnitCategory("VST3 Host\\Host")]
    [IncludeInSettings(true)]
    public sealed class VstUnloadPluginUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput pluginId { get; private set; }
        [DoNotSerialize] public ValueOutput success { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            pluginId = ValueInput<int>(nameof(pluginId), -1);
            success = ValueOutput<bool>(nameof(success));
        }

        ControlOutput Enter(Flow flow)
        {
            var id = flow.GetValue<int>(pluginId);
            var ok = id >= 1 && VstHostManager.Instance != null
                && VstHostManager.Instance.DestroyInstance(id);
            flow.SetValue(success, ok);
            return exit;
        }
    }
}
#endif
