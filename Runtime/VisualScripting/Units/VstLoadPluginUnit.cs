#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Load Plugin")]
    [UnitCategory("VST3 Host\\Host")]
    [IncludeInSettings(true)]
    public sealed class VstLoadPluginUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput filePath { get; private set; }
        [DoNotSerialize] public ValueInput uid { get; private set; }
        [DoNotSerialize] public ValueOutput pluginId { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            filePath = ValueInput<string>(nameof(filePath), string.Empty);
            uid = ValueInput<string>(nameof(uid), string.Empty);
            pluginId = ValueOutput<int>(nameof(pluginId));
        }

        ControlOutput Enter(Flow flow)
        {
            var path = flow.GetValue<string>(filePath);
            var idUid = flow.GetValue<string>(uid);
            var id = VstHostManager.Instance != null
                ? VstHostManager.Instance.CreateInstance(path, string.IsNullOrEmpty(idUid) ? null : idUid)
                : -1;
            flow.SetValue(pluginId, id);
            return exit;
        }
    }
}
#endif
