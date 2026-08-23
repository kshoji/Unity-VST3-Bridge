#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    /// <summary>MCP prompts for common VST3 host workflows.</summary>
    [AiPromptType]
    public partial class Prompt_VstHost
    {
        // Optional unused arg: prefer prompts/get with Arguments:{} (null fails on some MCP hosts).
        const string ArgsHint =
            "Always pass Arguments as {}. Never omit Arguments and never send null.";

        [AiPrompt(Name = "vst3-quick-start-note", Role = Role.User)]
        [Description(
            "MIDI-less quick start: Init → Scan → Load instrument → AudioFilter → NoteOn C4 → Activity.")]
        public string QuickStartNote
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "Complete a MIDI-less VST3 host smoke test in Unity using these exact tool names:\n" +
                "1. vst3-ping then vst3-features-status.\n" +
                "2. Enter Play Mode.\n" +
                "3. vst3-host-init with fromAudioSettings=true.\n" +
                "4. vst3-scan; pick an Instrument-class plugin (e.g. mda DX10), not Effect-only.\n" +
                "5. vst3-load with that path/uid.\n" +
                "6. vst3-setup-audio-filter mode=Instrument (creates GameObject __VstHostMcpAudio).\n" +
                "7. vst3-activity-clear then vst3-note-on note=60; wait briefly; vst3-note-off note=60.\n" +
                "8. vst3-activity-read and vst3-validate (expect filter on __VstHostMcpAudio).\n" +
                "If silent: Instrument vs Effect, AudioSource playing, vst3-diagnostics-read.\n" +
                "Always finish with vst3-note-off-all if notes may still be hanging.\n" +
                "Use only names from tools/list; do not invent aliases.";
        }

        [AiPrompt(Name = "vst3-validate-troubleshoot", Role = Role.User)]
        [Description(
            "Troubleshoot silent/failed VST host using validate, platforms, diagnostics, and Play Mode checks.")]
        public string ValidateTroubleshoot
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "Diagnose Unity VST3 host issues with these exact tool names:\n" +
                "1. vst3-verify-platforms — fix missing/misconfigured VstHostNative binaries.\n" +
                "2. vst3-validate — hostInitialized, loadedCount, audioFilterCount, mcpAudioFilter=__VstHostMcpAudio, Play Mode.\n" +
                "3. If not initialized: Play Mode → vst3-host-init.\n" +
                "4. If no instances: vst3-scan then vst3-load.\n" +
                "5. If no filter in Play Mode: vst3-setup-audio-filter (look for __VstHostMcpAudio in validate).\n" +
                "6. vst3-diagnostics-read — Process fail / blockSize skip / buffer clip.\n" +
                "7. vst3-activity-read — confirm MIDI reached the host.\n" +
                "Do not assume commercial plugins are compatible; use Activity + diagnostics to isolate.";
        }

        [AiPrompt(Name = "vst3-params-preset-smoke", Role = Role.User)]
        [Description(
            "Parameter + preset smoke: list/set a parameter, create/capture/apply a VstPresetAsset, optional A/B.")]
        public string ParamsPresetSmoke
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "VST3 parameter + preset smoke (after a loaded instrument in Play Mode).\n" +
                "IMPORTANT: prompts/get must use Arguments:{} (never Arguments:null).\n" +
                "Exact tools/list names only:\n" +
                "1. vst3-params-list pluginId=<id> preferAutomate=true — pick the first suggested " +
                "CanAutomate / non-programChange / non-readOnly param (avoid Factory Presets lists).\n" +
                "2. vst3-param-get then vst3-param-set (paramId or title) to a mid continuous value; " +
                "vst3-activity-read.\n" +
                "3. vst3-programs-list; if count>0 optional vst3-set-program index=0.\n" +
                "4. vst3-preset-create-asset assetPath=Assets/VstMcpPresets/Smoke.asset " +
                "capturePluginId=<id> overwrite=true.\n" +
                "5. Change that param, then vst3-preset-apply assetPath=...; confirm via vst3-param-get.\n" +
                "6. Optional A/B (state chunk lags param-set until Process):\n" +
                "   - vst3-preset-ab action=capture-a (flushProcessBeforeCapture=true by default)\n" +
                "   - vst3-param-set to a clearly different value\n" +
                "   - wait ~300–500ms OR play a short note, then vst3-preset-ab action=capture-b\n" +
                "   - Confirm Success shows slotsEqual=false and different slotASha8/slotBSha8\n" +
                "   - vst3-preset-ab action=toggle twice; check applied=A|B and preferSlotB; " +
                "vst3-activity-read for State entries\n" +
                "7. Optional: vst3-parameter-panel-setup.\n" +
                "Prefer presets over huge vst3-state-get Base64.";
        }

        [AiPrompt(Name = "vst3-midi-adapter-smoke", Role = Role.User)]
        [Description(
            "MIDI: Sync define → Adapter → virtual inject → Activity (0 hardware devices OK).")]
        public string MidiAdapterSmoke
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "VST3 + MIDI Plugin smoke (FEATURE_MIDI_PLUGIN / mcp-midi). Exact tools/list names:\n" +
                "1. vst3-features-status — confirm FEATURE_MIDI_PLUGIN + mcp-midi; if missing, Edit Mode → " +
                "vst3-sync-midi-define then wait for domain reload.\n" +
                "2. Play Mode → vst3-host-init → vst3-scan → vst3-load Instrument → vst3-setup-audio-filter.\n" +
                "3. vst3-midi-adapter-setup targetPluginId=<id> allowedDeviceIds=virtual:vst3-smoke.\n" +
                "4. Do NOT use vst3-note-on here (bypasses Adapter). With zero hardware devices use:\n" +
                "   a) midi-virtual-device action=register deviceId=virtual:vst3-smoke input=true output=false\n" +
                "   b) vst3-activity-clear\n" +
                "   c) midi-virtual-device-inject messageType=NoteOn deviceId=virtual:vst3-smoke " +
                "channel=0 data1=60 data2=100\n" +
                "   d) midi-virtual-device-inject messageType=NoteOff deviceId=virtual:vst3-smoke " +
                "channel=0 data1=60 data2=0\n" +
                "   (Alternative to c/d: midi-virtual-device action=inject-note-on|inject-note-off " +
                "with note=60 as JSON integer, optional noteName for C4.)\n" +
                "5. vst3-activity-read — expect NoteOn/NoteOff (one each is enough); vst3-note-off-all if stuck.\n" +
                "Optional: vst3-smf-link; smf-player-control; vst3-channel-routes.\n" +
                "Boundary: MIDI MCP = devices/inject/SMF; vst3-* = Adapter/SmfLink wiring only.";
        }

        [AiPrompt(Name = "vst3-midi-learn-smoke", Role = Role.User)]
        [Description(
            "CC mapping asset → MIDI Learn → Activity SetParameter confirmation.")]
        public string MidiLearnSmoke
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "MIDI Learn → parameter smoke (Play Mode, loaded plugin). Exact tool names:\n" +
                "1. vst3-params-list preferAutomate=true; pick a writable continuous paramId.\n" +
                "2. vst3-cc-mapping-create assetPath=Assets/VstMcpMappings/Learn.asset " +
                "assignToGameObject=__VstHostMcpMidi targetPluginId=<id>.\n" +
                "3. Prefer vst3-midi-learn bindNow=true controller=<cc> parameterId=<id> " +
                "mappingAssetPath=... OR arm Learn (bindNow=false) then:\n" +
                "   midi-virtual-device action=register deviceId=virtual:vst3-smoke input=true\n" +
                "   midi-virtual-device-inject messageType=ControlChange deviceId=virtual:vst3-smoke " +
                "data1=<cc> data2=64\n" +
                "4. vst3-activity-read / vst3-param-get to confirm SetParameter.\n" +
                "5. Optional: vst3-cc-mapping-list / vst3-cc-mapping-edit.";
        }

        [AiPrompt(Name = "vst3-graph-parallel-smoke", Role = Role.User)]
        [Description(
            "Audio Graph: parallel instruments → serial FX → status / bypass / edge gain.")]
        public string GraphParallelSmoke
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "VST3 Audio Graph smoke (Play Mode). Exact tool names:\n" +
                "1. vst3-host-init → vst3-scan → vst3-load ≥1 Instrument (optional Effect).\n" +
                "2. vst3-graph-build-parallel instrumentIds=<id> effectIds=<fxId or empty>.\n" +
                "3. vst3-graph-status gameObjectName=__VstHostMcpGraph — confirm armed, nodes, edges.\n" +
                "   Optional resource: vst3://graph/__VstHostMcpGraph (same content as status).\n" +
                "4. vst3-note-on note=60 (or vst3-dsp-midi-schedule messageType=noteon); vst3-activity-read.\n" +
                "5. Optional: vst3-graph-bypass nodeId=<instrument node id> bypass=true; " +
                "vst3-graph-set-edge-gain for send graphs.\n" +
                "6. Optional: vst3-graph-build-sidechain mode=single with AGain SideChain.\n" +
                "Prefer Build* over vst3-graph-set. Tools disable VstHostAudioFilter on the graph GO.";
        }

        [AiPrompt(Name = "vst3-sa-dsp-midi-smoke", Role = Role.User)]
        [Description(
            "Scriptable Audio (when Unity 6000.3+): SA Generator + DSP MIDI schedule.")]
        public string SaDspMidiSmoke
        (
            [Description(ArgsHint)]
            string? unused = null
        )
        {
            return
                "Scriptable Audio + DSP MIDI (Unity 6000.3+, non-WebGL). Exact tool names:\n" +
                "1. vst3-features-status — confirm scriptable-audio / mcp-scriptable-audio.\n" +
                "2. Play Mode → vst3-host-init → vst3-load Instrument.\n" +
                "3. vst3-sa-generator-setup pluginId=<id> (if unavailable: vst3-setup-audio-filter + schedule).\n" +
                "4. vst3-dsp-midi-schedule messageType=noteon number=60 offsetMs=0 scheduleNoteOff=true.\n" +
                "5. vst3-activity-read / listen for audio.\n" +
                "Optional: vst3-sa-midi-bridge when FEATURE_SCRIPTABLE_AUDIO+MIDI present.";
        }
    }
}
