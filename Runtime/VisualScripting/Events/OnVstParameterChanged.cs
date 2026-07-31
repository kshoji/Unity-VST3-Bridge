#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("On VST Parameter Changed")]
    [UnitCategory("VST3 Host\\Events")]
    [IncludeInSettings(true)]
    public sealed class OnVstParameterChanged : EventUnit<VstHostActivityEntry>
    {
        [DoNotSerialize] public ValueOutput pluginId { get; private set; }
        [DoNotSerialize] public ValueOutput detail { get; private set; }

        protected override bool register => true;

        protected override void Definition()
        {
            base.Definition();
            pluginId = ValueOutput<int>(nameof(pluginId));
            detail = ValueOutput<string>(nameof(detail));
        }

        protected override void AssignArguments(Flow flow, VstHostActivityEntry args)
        {
            flow.SetValue(pluginId, args.PluginId);
            flow.SetValue(detail, args.Detail);
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook(VstVisualScriptingHooks.ParameterChanged, reference.gameObject);
        }
    }
}
#endif
