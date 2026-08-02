#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    [UnitTitle("VST Note Off")]
    [UnitCategory("VST3 Host\\MIDI")]
    [IncludeInSettings(true)]
    public sealed class VstNoteOffUnit : Unit
    {
        [DoNotSerialize] public ControlInput enter { get; private set; }
        [DoNotSerialize] public ControlOutput exit { get; private set; }
        [DoNotSerialize] public ValueInput pluginId { get; private set; }
        [DoNotSerialize] public ValueInput channel { get; private set; }
        [DoNotSerialize] public ValueInput note { get; private set; }
        [DoNotSerialize] public ValueInput velocity { get; private set; }

        protected override void Definition()
        {
            enter = ControlInput(nameof(enter), Enter);
            exit = ControlOutput(nameof(exit));
            Succession(enter, exit);
            pluginId = ValueInput<int>(nameof(pluginId), -1);
            channel = ValueInput<int>(nameof(channel), 0);
            note = ValueInput<int>(nameof(note), 60);
            velocity = ValueInput<int>(nameof(velocity), 0);
        }

        ControlOutput Enter(Flow flow)
        {
            var id = flow.GetValue<int>(pluginId);
            if (id >= 1 && VstHostManager.Instance != null)
            {
                VstHostManager.Instance.NoteOff(
                    id,
                    flow.GetValue<int>(channel),
                    flow.GetValue<int>(note),
                    flow.GetValue<int>(velocity));
            }

            return exit;
        }
    }
}
#endif
