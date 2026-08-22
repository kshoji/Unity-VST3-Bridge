#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    /// <summary>MCP prompts for common VST3 host workflows (Phase 1).</summary>
    [AiPromptType]
    public partial class Prompt_VstHost
    {
        [AiPrompt(Name = "vst3-quick-start-note", Role = Role.User)]
        [Description(
            "MIDI-less quick start: Init → Scan → Load instrument → AudioFilter → NoteOn C4 → Activity.")]
        public string QuickStartNote()
        {
            return
                "Complete a MIDI-less VST3 host smoke test in Unity using vst3-* tools only:\n" +
                "1. Call vst3-ping and vst3-features-status.\n" +
                "2. Enter Play Mode.\n" +
                "3. vst3-host-init (fromAudioSettings=true).\n" +
                "4. vst3-scan; pick an Instrument-class plugin (e.g. mda DX10), not an Effect-only class.\n" +
                "5. vst3-load with that path/uid.\n" +
                "6. vst3-setup-audio-filter with mode=Instrument.\n" +
                "7. vst3-activity-clear then vst3-note-on note=60; wait briefly; vst3-note-off.\n" +
                "8. vst3-activity-read and vst3-validate.\n" +
                "If silent: check Instrument vs Effect, AudioSource playing, and vst3-diagnostics-read.\n" +
                "Always finish with vst3-note-off-all if notes may still be hanging.";
        }

        [AiPrompt(Name = "vst3-validate-troubleshoot", Role = Role.User)]
        [Description(
            "Troubleshoot silent/failed VST host using validate, platforms, diagnostics, and Play Mode checks.")]
        public string ValidateTroubleshoot()
        {
            return
                "Diagnose Unity VST3 host issues with vst3-* tools:\n" +
                "1. vst3-verify-platforms — fix missing/misconfigured VstHostNative binaries.\n" +
                "2. vst3-validate — review hostInitialized, loadedCount, audioFilterCount, Play Mode.\n" +
                "3. If not initialized: Play Mode → vst3-host-init.\n" +
                "4. If no instances: vst3-scan / vst3-load.\n" +
                "5. If no filter in Play Mode: vst3-setup-audio-filter.\n" +
                "6. vst3-diagnostics-read — Process fail / blockSize skip / buffer clip.\n" +
                "7. vst3-activity-read — confirm MIDI reached the host.\n" +
                "Do not assume commercial plugins are compatible; use Activity + diagnostics to isolate.";
        }
    }
}
