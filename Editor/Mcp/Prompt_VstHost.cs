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

        [AiPrompt(Name = "vst3-params-preset-smoke", Role = Role.User)]
        [Description(
            "Phase 2 smoke: list/set a parameter, create/capture/apply a VstPresetAsset, optional A/B.")]
        public string ParamsPresetSmoke()
        {
            return
                "VST3 parameter + preset smoke (after a loaded instrument in Play Mode):\n" +
                "1. vst3-params-list for the loaded pluginId; pick a writable param (not readOnly).\n" +
                "2. vst3-param-get then vst3-param-set (by paramId or title) to a mid value; vst3-activity-read.\n" +
                "3. vst3-programs-list; if count>0 try vst3-set-program index=0.\n" +
                "4. vst3-preset-create-asset path=Assets/VstMcpPresets/Smoke.asset capturePluginId=<id>.\n" +
                "5. Change a param, then vst3-preset-apply the asset; confirm value restored via vst3-param-get.\n" +
                "6. Optional: vst3-preset-ab capture-a → change param → capture-b → toggle.\n" +
                "7. Optional: vst3-parameter-panel-setup for on-screen sliders.\n" +
                "Prefer presets over huge vst3-state-get Base64 when possible.";
        }

        [AiPrompt(Name = "vst3-midi-adapter-smoke", Role = Role.User)]
        [Description(
            "Phase 3: Sync MIDI define → Adapter → hardware/virtual Note → Activity (+ optional MIDI MCP).")]
        public string MidiAdapterSmoke()
        {
            return
                "VST3 + MIDI Plugin smoke (needs FEATURE_MIDI_PLUGIN / Phase 3 tools):\n" +
                "1. vst3-features-status — confirm FEATURE_MIDI_PLUGIN + mcp-midi; if missing, Edit Mode → " +
                "vst3-sync-midi-define then wait for domain reload.\n" +
                "2. Play Mode → vst3-host-init → scan/load Instrument → vst3-setup-audio-filter.\n" +
                "3. vst3-midi-adapter-setup targetPluginId=<id> allowedDeviceIds=virtual:vst3-smoke " +
                "(or clear filter if using hardware).\n" +
                "4. Exercise Adapter via MIDI MCP (do NOT use vst3-note-on for this step — it bypasses Adapter):\n" +
                "   - midi-virtual-device action=register deviceId=virtual:vst3-smoke input=true\n" +
                "   - midi-virtual-device action=inject-note-on deviceId=virtual:vst3-smoke note=60 value=100\n" +
                "     (note is int; for names use noteName=\"C4\". Never pass a bare number into a string note param.)\n" +
                "   - then inject-note-off with the same deviceId/note\n" +
                "5. vst3-activity-read — expect NoteOn/NoteOff; vst3-note-off-all if stuck.\n" +
                "Optional: vst3-smf-link then smf-player-control; vst3-channel-routes for multi-timbral.\n" +
                "Boundary: MIDI MCP owns devices/inject/SMF; vst3-* owns Adapter/SmfLink wiring only.";
        }

        [AiPrompt(Name = "vst3-midi-learn-smoke", Role = Role.User)]
        [Description(
            "Phase 3: CC mapping asset → MIDI Learn → Activity SetParameter confirmation.")]
        public string MidiLearnSmoke()
        {
            return
                "MIDI Learn → parameter smoke (Play Mode, loaded plugin):\n" +
                "1. vst3-params-list; pick a writable paramId.\n" +
                "2. vst3-cc-mapping-create Assets/VstMcpMappings/Learn.asset " +
                "assignToGameObject=__VstHostMcpMidi targetPluginId=<id>.\n" +
                "3. Either vst3-midi-learn bindNow=true controller=<cc> OR arm Learn (bindNow=false) " +
                "and send that CC from a controller / midi-send.\n" +
                "4. Move the CC; vst3-activity-read / vst3-param-get to confirm SetParameter.\n" +
                "5. Optional: vst3-cc-mapping-list / edit for PitchBend or 14-bit.";
        }
    }
}
