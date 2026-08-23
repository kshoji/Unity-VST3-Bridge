#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost.Editor;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool("vst3-sync-midi-define", Title = "VST3 / Sync MIDI Plugin Define")]
        [Description(
            "Sync FEATURE_MIDI_PLUGIN scripting define across build targets " +
            "(same as Window/VST3 Host/Sync MIDI Plugin Define). " +
            "Enables jp.kshoji.unity.vst3nativehost.Midi and MIDI MCP tools " +
            "(jp.kshoji.unity.vst3nativehost.Mcp.Midi) when jp.kshoji.midi.asmdef is present. " +
            "Edit Mode only; may require a domain reload. Call before MIDI wiring tools if missing.")]
        public string SyncMidiDefine()
        {
            return MainThread.Instance.Run(() =>
            {
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return
                        "[Error] vst3-sync-midi-define: exit Play Mode first " +
                        "(define sync is skipped while entering Play Mode).";
                }

                var hasMidi = VstHostMidiDefineSync.HasMidiAsmdef();
                VstHostMidiDefineSync.SyncDefines();
                var definePresent = HasScriptingDefine(VstHostMidiDefineSync.DefineSymbol);
                var midiAsm = HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Midi");
                var mcpMidiAsm = HasLoadedAssembly("jp.kshoji.unity.vst3nativehost.Mcp.Midi");

                return
                    $"[Success] vst3-sync-midi-define hasMidiAsmdef={hasMidi} " +
                    $"definePresent={definePresent} midiAsmLoaded={midiAsm} mcpMidiAsmLoaded={mcpMidiAsm}. " +
                    (hasMidi && !mcpMidiAsm
                        ? "If MIDI MCP tools are missing, wait for domain reload / recompile then retry vst3-features-status."
                        : hasMidi
                            ? "MIDI MCP tools (Mcp.Midi) should be available."
                            : "jp.kshoji.midi.asmdef not found — install Unity MIDI Plugin under Assets.");
            });
        }
    }
}
