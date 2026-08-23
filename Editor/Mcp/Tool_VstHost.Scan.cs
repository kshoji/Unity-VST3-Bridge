#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-scan",
            Title = "VST3 / Scan",
            ReadOnlyHint = true)]
        [Description(
            "Scan OS-standard VST3 folders (SDK Module::getModulePaths). " +
            "Requires vst3-host-init. Edit Mode OK. Returns name/vendor/category/uid/path. " +
            "Does not redistribute plugins — paths only.")]
        public string Scan
        (
            [Description("Max plugins to include in the text response (full count still reported).")]
            int maxResults = 200
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireInitialized("vst3-scan", out var error))
                    return error!;

                var plugins = Host.Scan();
                RememberScan(plugins);
                return $"[Success] vst3-scan\n{FormatScanned(plugins, maxResults)}";
            });
        }

        [AiTool(
            "vst3-scan-folder",
            Title = "VST3 / Scan Folder",
            ReadOnlyHint = true)]
        [Description(
            "Scan a specific folder recursively for VST3 plugins. Empty/null folder = standard folders. " +
            "Requires vst3-host-init. Edit Mode OK.")]
        public string ScanFolder
        (
            [Description("Absolute folder path. Pass empty to use standard folders.")]
            string? folderPath = null,
            [Description("Max plugins to include in the text response.")]
            int maxResults = 200
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireInitialized("vst3-scan-folder", out var error))
                    return error!;

                var plugins = Host.ScanFolder(string.IsNullOrWhiteSpace(folderPath) ? null : folderPath);
                RememberScan(plugins);
                return
                    $"[Success] vst3-scan-folder folder={(string.IsNullOrWhiteSpace(folderPath) ? "(default)" : folderPath)}\n" +
                    FormatScanned(plugins, maxResults);
            });
        }

        [AiTool(
            "vst3-default-scan-folders",
            Title = "VST3 / Default Scan Folders",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "List OS-standard VST3 scan folder paths mirrored by GetDefaultScanFolders (UI/tooling). " +
            "Does not require host init. Edit Mode OK.")]
        public string DefaultScanFolders()
        {
            return MainThread.Instance.Run(() =>
            {
                var folders = VstHostManager_GetDefaultScanFolders();
                var lines = new System.Text.StringBuilder();
                lines.Append($"[Success] vst3-default-scan-folders count={folders.Count}");
                for (var i = 0; i < folders.Count; i++)
                {
                    lines.Append('\n');
                    lines.Append($"[{i}] {folders[i]}");
                }

                return lines.ToString();
            });
        }

        // Avoid alias clash with tool method name Scan vs type helpers.
        static System.Collections.Generic.IReadOnlyList<string> VstHostManager_GetDefaultScanFolders() =>
            jp.kshoji.unity.vst3nativehost.VstHostManager.GetDefaultScanFolders();
    }
}
