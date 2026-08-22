#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-note-on", Title = "VST3 / Note On")]
        [Description(
            "Send Note On to a loaded plugin. Play Mode required for audible result " +
            "(with vst3-setup-audio-filter). Channel 0–15.")]
        public string NoteOn
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("MIDI note number (e.g. 60 = C4).")]
            int note = 60,
            [Description("Velocity 1–127.")]
            int velocity = 100
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-note-on", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-note-on", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.NoteOn(pluginId, channel, note, velocity))
                    return $"[Error] vst3-note-on failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-note-on id={pluginId} ch={channel} note={note} vel={velocity}";
            });
        }

        [AiTool("vst3-note-off", Title = "VST3 / Note Off")]
        [Description("Send Note Off. Play Mode required.")]
        public string NoteOff
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("MIDI note number.")]
            int note = 60,
            [Description("Release velocity.")]
            int velocity = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-note-off", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-note-off", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.NoteOff(pluginId, channel, note, velocity))
                    return $"[Error] vst3-note-off failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-note-off id={pluginId} ch={channel} note={note}";
            });
        }

        [AiTool("vst3-note-off-all", Title = "VST3 / Note Off All")]
        [Description(
            "Hang-note safety: CC 123 All Notes Off on the channel; optional CC 120 All Sound Off. " +
            "Play Mode required.")]
        public string NoteOffAll
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("Also send CC 120 All Sound Off.")]
            bool allSoundOff = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-note-off-all", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-note-off-all", pluginId, out var loadErr))
                    return loadErr!;

                var ok = Host.ControlChange(pluginId, channel, 123, 0);
                if (allSoundOff)
                    ok = Host.ControlChange(pluginId, channel, 120, 0) && ok;

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return ok
                    ? $"[Success] vst3-note-off-all id={pluginId} ch={channel} allSoundOff={allSoundOff}"
                    : $"[Error] vst3-note-off-all failed id={pluginId}";
            });
        }

        [AiTool("vst3-send-cc", Title = "VST3 / Send CC")]
        [Description("Send MIDI Control Change. Play Mode required for audible/automation effect.")]
        public string SendCc
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("Controller number 0–127.")]
            int controller = 1,
            [Description("Value 0–127.")]
            int value = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-send-cc", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-send-cc", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.ControlChange(pluginId, channel, controller, value))
                    return $"[Error] vst3-send-cc failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-send-cc id={pluginId} ch={channel} cc={controller} value={value}";
            });
        }

        [AiTool("vst3-send-pc", Title = "VST3 / Send Program Change")]
        [Description(
            "Send MIDI Program Change (not host SetProgram). Play Mode required. " +
            "For host program lists use Phase 2 vst3-set-program.")]
        public string SendPc
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("Program 0–127.")]
            int program = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-send-pc", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-send-pc", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.ProgramChange(pluginId, channel, program))
                    return $"[Error] vst3-send-pc failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-send-pc id={pluginId} ch={channel} program={program}";
            });
        }

        [AiTool("vst3-send-pitch", Title = "VST3 / Send Pitch Bend")]
        [Description("Send Pitch Bend (14-bit amount, center 8192). Play Mode required.")]
        public string SendPitch
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("14-bit pitch bend amount (0–16383, center 8192).")]
            int amount14 = 8192
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-send-pitch", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-send-pitch", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.PitchBend(pluginId, channel, amount14))
                    return $"[Error] vst3-send-pitch failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-send-pitch id={pluginId} ch={channel} amount14={amount14}";
            });
        }

        [AiTool("vst3-send-aftertouch", Title = "VST3 / Send Aftertouch")]
        [Description("Send Channel Aftertouch. Play Mode required.")]
        public string SendAftertouch
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("Pressure 0–127.")]
            int pressure = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-send-aftertouch", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-send-aftertouch", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.ChannelAftertouch(pluginId, channel, pressure))
                    return $"[Error] vst3-send-aftertouch failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return $"[Success] vst3-send-aftertouch id={pluginId} ch={channel} pressure={pressure}";
            });
        }

        [AiTool("vst3-send-midi1", Title = "VST3 / Send MIDI1")]
        [Description(
            "Send a raw MIDI 1.0 short message (status/data1/data2). " +
            "Use for poly aftertouch etc. Play Mode required.")]
        public string SendMidi1
        (
            [Description("Plugin instance id.")]
            int pluginId,
            [Description("Status byte (e.g. 0x90 note on ch0).")]
            int status,
            [Description("Data byte 1.")]
            int data1 = 0,
            [Description("Data byte 2.")]
            int data2 = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequirePlayMode("vst3-send-midi1", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-send-midi1", pluginId, out var loadErr))
                    return loadErr!;

                if (!Host.SendMidi1(pluginId, (byte)status, (byte)data1, (byte)data2))
                    return $"[Error] vst3-send-midi1 failed id={pluginId}";

                jp.kshoji.unity.vst3nativehost.VstHostActivity.PumpMainThread();
                return
                    $"[Success] vst3-send-midi1 id={pluginId} status=0x{status:X2} " +
                    $"d1={data1} d2={data2}";
            });
        }
    }
}
