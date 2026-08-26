#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-features-status",
            Title = "VST3 / Features Status",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Reports which optional VST3 Host integrations are available " +
            "(MIDI Plugin, Input System, Timeline, Scriptable Audio, Chunity, Network MIDI, " +
            "Visual Scripting) by loaded assembly names and scripting defines.")]
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
                        "mcp-runtime",
                        HasLoadedAssembly(VstHostToolHelpers.McpRuntimeAssemblyName),
                        AsmOrDefine(
                            VstHostToolHelpers.McpRuntimeAssemblyName,
                            "FEATURE_UNITY_MCP",
                            "Runtime MCP tools + resources (this assembly)"),
                        "mcp.md"),
                    DescribeFeature(
                        "mcp-editor",
                        HasLoadedAssembly(VstHostToolHelpers.McpEditorAssemblyName),
                        AsmOrDefine(
                            VstHostToolHelpers.McpEditorAssemblyName,
                            "FEATURE_UNITY_MCP",
                            "Editor-only MCP tools (scene/asset wiring)"),
                        "mcp.md"),
                    DescribeFeature(
                        "mcp-midi",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Midi.Runtime")
                        || HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Midi"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.Midi.Runtime",
                            "FEATURE_MIDI_PLUGIN",
                            "MIDI wiring (Runtime); cc-mapping-* Editor-only in Mcp.Midi"),
                        "midi-integration.md"),
                    DescribeFeature(
                        "mcp-timeline",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Timeline.Runtime")
                        || HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Timeline"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.Timeline.Runtime",
                            "FEATURE_USE_TIMELINE",
                            "director-control (Runtime); param-track/marker Editor-only"),
                        "timeline.md"),
                    DescribeFeature(
                        "mcp-inputsystem",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.InputSystem.Runtime"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.InputSystem.Runtime",
                            "FEATURE_INPUT_SYSTEM",
                            "inputsystem-bridge (Runtime + Play Mode)"),
                        "animator-input.md"),
                    DescribeFeature(
                        "mcp-scriptable-audio",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.ScriptableAudio.Runtime"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Mcp.ScriptableAudio.Runtime",
                            "UNITY_6000_3_OR_NEWER",
                            "sa-generator-setup (Runtime)"),
                        "scriptable-audio.md"),
                    DescribeFeature(
                        "FEATURE_MIDI_PLUGIN",
                        HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Midi")
                        || McpEditorFeatureProbe.HasScriptingDefine("FEATURE_MIDI_PLUGIN"),
                        AsmOrDefine(
                            "jp.kshoji.unity.vst3nativehost.Midi",
                            "FEATURE_MIDI_PLUGIN",
                            "MIDI integration runtime"),
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
                    "playModeBoundaries: EditMode=ok for features/status/scan; " +
                    "PlayMode or running build required for note/Process/Activity audio path.",
                    $"session={McpExecutionContext.SessionKind}",
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
            var def = defineHint.IndexOf('+') >= 0
                ? "n/a"
                : McpEditorFeatureProbe.HasScriptingDefine(defineHint).ToString();
            return $"asm={asm} defineHint={defineHint} definePresent={def}; {how}";
        }
    }
}
