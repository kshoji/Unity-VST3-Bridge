#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-features-status",
            Title = "VST3 / Features Status",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Reports which optional VST3 Host integrations are available in this project " +
            "(MIDI Plugin, Input System, Timeline, Scriptable Audio, Chunity, Network MIDI, " +
            "Visual Scripting) by loaded assembly names and scripting defines. " +
            "Includes Documentation~ links. Safe in Edit Mode. " +
            "Call before optional MIDI / Timeline / SA tools; unavailable features will not register MCP tools.")]
        public string FeaturesStatus()
        {
            return MainThread.Instance.Run(() =>
            {
                var lines = new List<string>
                {
                    "[Success] vst3-features-status",
                    DescribeFeature(
                        "core",
                        true,
                        "jp.kshoji.unity.vst3nativehost.Runtime always present",
                        "usage.md"),
                    DescribeFeature(
                        "mcp",
                        true,
                        $"{McpAssemblyName} (this assembly; requires com.ivanmurzak.unity.mcp >= 0.76 + UNITY_MCP_READY)",
                        "mcp.md"),
                    DescribeFeature(
                        "mcp-midi",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Midi"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.Midi",
                            "FEATURE_MIDI_PLUGIN",
                            "MIDI wiring tools; needs FEATURE_MIDI_PLUGIN + UNITY_MCP_READY (vst3-sync-midi-define)"),
                        "midi-integration.md"),
                    DescribeFeature(
                        "mcp-timeline",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Timeline"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.Timeline",
                            "FEATURE_USE_TIMELINE",
                            "Timeline MCP tools"),
                        "timeline.md"),
                    DescribeFeature(
                        "mcp-inputsystem",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.InputSystem"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.InputSystem",
                            "FEATURE_INPUT_SYSTEM",
                            "Input System MCP tools"),
                        "animator-input.md"),
                    DescribeFeature(
                        "mcp-scriptable-audio",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.ScriptableAudio"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.ScriptableAudio",
                            "UNITY_6000_3_OR_NEWER",
                            "SA Generator MCP"),
                        "scriptable-audio.md"),
                    DescribeFeature(
                        "FEATURE_MIDI_PLUGIN",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Midi")
                        || HasScriptingDefine("FEATURE_MIDI_PLUGIN"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Midi",
                            "FEATURE_MIDI_PLUGIN",
                            "vst3-sync-midi-define or Window/VST3 Host/Sync MIDI Plugin Define when jp.kshoji.midi is present"),
                        "midi-integration.md"),
                    DescribeFeature(
                        "FEATURE_INPUT_SYSTEM",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.InputSystem"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.InputSystem",
                            "FEATURE_INPUT_SYSTEM",
                            "com.unity.inputsystem versionDefines"),
                        "animator-input.md"),
                    DescribeFeature(
                        "FEATURE_USE_TIMELINE",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Timeline"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Timeline",
                            "FEATURE_USE_TIMELINE",
                            "com.unity.timeline versionDefines"),
                        "timeline.md"),
                    DescribeFeature(
                        "scriptable-audio",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.ScriptableAudio"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.ScriptableAudio",
                            "UNITY_6000_3_OR_NEWER",
                            "Unity 6000.3+ non-WebGL; VstHostGenerator"),
                        "scriptable-audio.md"),
                    DescribeFeature(
                        "scriptable-audio-midi",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.ScriptableAudio.Midi"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.ScriptableAudio.Midi",
                            "FEATURE_SCRIPTABLE_AUDIO+FEATURE_MIDI_PLUGIN",
                            "VstHostDspMidiOutBridge"),
                        "scriptable-audio.md"),
                    DescribeFeature(
                        "FEATURE_CHUNITY",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Midi.Chunity"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Midi.Chunity",
                            "FEATURE_CHUNITY",
                            "requires FEATURE_MIDI_PLUGIN + Chunity"),
                        "chunity.md"),
                    DescribeFeature(
                        "FEATURE_MIDI_NETWORK",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Midi.Network"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Midi.Network",
                            "FEATURE_MIDI_NETWORK",
                            "requires FEATURE_MIDI_PLUGIN + Network MIDI"),
                        "network-midi.md"),
                    DescribeFeature(
                        "FEATURE_USE_VISUALSCRIPTING",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.VisualScripting"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.VisualScripting",
                            "FEATURE_USE_VISUALSCRIPTING",
                            "com.unity.visualscripting versionDefines"),
                        "visual-scripting.md"),
                    DescribeFeature(
                        "audio-graph",
                        true,
                        "VstAudioGraph in Runtime (no extra define)",
                        "audio-graph.md"),
                    "playModeBoundaries: EditMode=ok for ping/features/status/scan(when documented); " +
                    "PlayMode=required for note/Process/Activity audio path (see tool Descriptions).",
                    $"playMode={IsPlayMode}",
                };

                var sb = new StringBuilder();
                for (var i = 0; i < lines.Count; i++)
                {
                    if (i > 0)
                        sb.Append('\n');
                    sb.Append(lines[i]);
                }

                return sb.ToString();
            });
        }

        static string AsmOrDefine(string assemblyName, string defineHint, string how)
        {
            var asm = HasLoadedAssembly(assemblyName);
            // Composite hints (a+b) are documented only; single-symbol defines are probed.
            var def = defineHint.IndexOf('+') >= 0
                ? "n/a"
                : HasScriptingDefine(defineHint).ToString();
            return $"asm={asm} defineHint={defineHint} definePresent={def}; {how}";
        }
    }
}
