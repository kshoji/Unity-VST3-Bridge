#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using VstEditorSettings = jp.kshoji.unity.vst3nativehost.Editor.VstHostProjectSettings;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-settings-get",
            Title = "VST3 / Settings Get",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Editor only — read Project Settings → VST3 Host. " +
            "Standalone builds use VstHostRuntimeMcpConfig (vst3://settings). Edit Mode OK.")]
        public string SettingsGet()
        {
            return MainThread.Instance.Run(() =>
                $"[Success] vst3-settings-get\n{FormatSettings()}");
        }

        [AiTool("vst3-settings-set", Title = "VST3 / Settings Set")]
        [Description(
            "Editor only — update Project Settings → VST3 Host. " +
            "Standalone: edit Resources/VstHostRuntimeMcpConfig.asset instead.")]
        public string SettingsSet
        (
            [Description("Auto Initialize On Play. Null = leave unchanged.")]
            bool? autoInitializeOnPlay = null,
            [Description("Preferred plugin name substring filter. Null = leave unchanged.")]
            string? preferredPluginNameContains = null,
            [Description("Replace extra scan folders. Null = leave unchanged. Empty array clears.")]
            string[]? extraScanFolders = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var s = VstEditorSettings.instance;
                if (autoInitializeOnPlay.HasValue)
                    s.AutoInitializeOnPlay = autoInitializeOnPlay.Value;
                if (preferredPluginNameContains != null)
                    s.PreferredPluginNameContains = preferredPluginNameContains;
                if (extraScanFolders != null)
                    s.ExtraScanFolders = extraScanFolders;
                s.Save();
                return $"[Success] vst3-settings-set\n{FormatSettings()}";
            });
        }
    }
}
