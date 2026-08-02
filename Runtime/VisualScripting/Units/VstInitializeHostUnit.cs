#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Initialize Host")]
    [UnitCategory("VST3 Host\\Host")]
    [IncludeInSettings(true)]
    public sealed class VstInitializeHostUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueOutput success { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            success = ValueOutput<bool>(nameof(success));
        }

        ControlOutput Enter(Flow flow)
        {
            var host = VstHostManager.Instance;
            var ok = host != null && (host.IsInitialized || host.InitializeFromAudioSettings());
            flow.SetValue(success, ok);
            return exit;
        }
    }
}
#endif
