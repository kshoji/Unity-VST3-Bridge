#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-dsp-midi-schedule", Title = "VST3 / DSP MIDI Schedule")]
        [Description(
            "Enqueue NoteOn/NoteOff/CC into VstHostDspMidiQueue.Shared at DSP clock. " +
            "offsetMs from AudioSettings.dspTime. Pair with VstHostAudioFilter / VstAudioGraph / VstHostGenerator " +
            "that flush the queue. Play Mode / runtime required.")]
        public string DspMidiSchedule
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("noteon | noteoff | cc")]
            string messageType = "noteon",
            [Description("Note or CC controller number.")]
            int number = 60,
            [Description("Velocity or CC value.")]
            int value = 100,
            [Description("MIDI channel 0–15.")]
            int channel = 0,
            [Description("Offset from now in milliseconds.")]
            float offsetMs = 0f,
            [Description("When true and messageType=noteon, also schedule noteoff after noteOffMs.")]
            bool scheduleNoteOff = true,
            [Description("NoteOff delay ms when scheduleNoteOff.")]
            float noteOffMs = 200f
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireAudioSession("vst3-dsp-midi-schedule", out var playErr))
                    return playErr!;
                if (!TryRequireLoaded("vst3-dsp-midi-schedule", pluginId, out var loadErr))
                    return loadErr!;

                var sampleRate = AudioSettings.outputSampleRate;
                if (sampleRate < 1)
                    return "[Error] vst3-dsp-midi-schedule: AudioSettings.outputSampleRate invalid.";

                var now = (long)(AudioSettings.dspTime * sampleRate);
                var at = now + (long)(offsetMs * 0.001 * sampleRate);
                var type = (messageType ?? "noteon").Trim().ToLowerInvariant();
                var queue = VstHostDspMidiQueue.Shared;
                bool ok;

                switch (type)
                {
                    case "noteon":
                    case "note-on":
                        ok = queue.ScheduleNoteOn(pluginId, at, channel, number, value);
                        if (ok && scheduleNoteOff)
                        {
                            var offAt = at + (long)(System.Math.Max(0f, noteOffMs) * 0.001 * sampleRate);
                            queue.ScheduleNoteOff(pluginId, offAt, channel, number, 0);
                        }

                        break;
                    case "noteoff":
                    case "note-off":
                        ok = queue.ScheduleNoteOff(pluginId, at, channel, number, value);
                        break;
                    case "cc":
                    case "controlchange":
                        ok = queue.ScheduleControlChange(pluginId, at, channel, number, value);
                        break;
                    default:
                        return "[Error] messageType must be noteon|noteoff|cc.";
                }

                return ok
                    ? $"[Success] vst3-dsp-midi-schedule type={type} pluginId={pluginId} " +
                      $"dspSample={at} queueCount={queue.Count}"
                    : "[Error] vst3-dsp-midi-schedule: queue full (overflow).";
            });
        }
    }
}
