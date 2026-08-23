#nullable enable
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-programs-list",
            Title = "VST3 / Programs List",
            ReadOnlyHint = true)]
        [Description(
            "List host program names for a loaded plugin (IUnitInfo / program-change param). " +
            "Many plugins expose an empty list. Edit Mode OK when initialized.")]
        public string ProgramsList
        (
            [Description("Loaded plugin instance id.")]
            int pluginId
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-programs-list", pluginId, out var error))
                    return error!;

                var programs = Host.GetPrograms(pluginId);
                var sb = new StringBuilder();
                sb.Append($"[Success] vst3-programs-list pluginId={pluginId} count={programs.Count}");
                for (var i = 0; i < programs.Count; i++)
                {
                    sb.Append('\n');
                    sb.Append($"[{i}] {programs[i]}");
                }

                if (programs.Count == 0)
                    sb.Append("\nhint=Plugin may not expose a host program list; use presets/state instead.");

                return sb.ToString();
            });
        }

        [AiTool("vst3-set-program", Title = "VST3 / Set Program")]
        [Description(
            "Set host program by index (VstHostManager.SetProgram). " +
            "Not the same as MIDI Program Change (vst3-send-pc).")]
        public string SetProgram
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Program index from vst3-programs-list.")]
            int index
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-set-program", pluginId, out var error))
                    return error!;

                if (!Host.SetProgram(pluginId, index))
                    return $"[Error] vst3-set-program failed pluginId={pluginId} index={index}";

                Activity.PumpMainThread();
                var programs = Host.GetPrograms(pluginId);
                var name = index >= 0 && index < programs.Count ? programs[index] : "(unknown)";
                return $"[Success] vst3-set-program pluginId={pluginId} index={index} name={name}";
            });
        }
    }
}
