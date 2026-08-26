#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-scan",
            Title = "VST3 / Scan",
            ReadOnlyHint = true)]
        [Description(
            "Scan OS-standard VST3 folders, then merge VstHostRuntimeMcpConfig.extraScanFolders " +
            "(Resources) when present. Dedupes by uid|path. Requires vst3-host-init. " +
            "Returns name/vendor/category/uid/path.")]
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

                var plugins = ScanStandardPlusRuntimeExtras(out var extraFolderCount);
                RememberScan(plugins);
                var extraMsg = extraFolderCount > 0
                    ? $" extraFolders={extraFolderCount}"
                    : string.Empty;
                return
                    $"[Success] vst3-scan{extraMsg}\n" +
                    VstHostToolHelpers.FormatScanned(plugins, maxResults);
            });
        }

        /// <summary>
        /// OS-standard scan plus optional <see cref="VstHostRuntimeMcpConfig.extraScanFolders"/>.
        /// </summary>
        internal static List<VstHostManager.ScannedPlugin> ScanStandardPlusRuntimeExtras(
            out int extraFolderCount)
        {
            extraFolderCount = 0;
            var merged = new List<VstHostManager.ScannedPlugin>();
            merged.AddRange(Host.Scan());

            var cfg = VstHostRuntimeMcpConfig.LoadFromResources();
            var extras = cfg?.extraScanFolders;
            if (extras == null || extras.Length == 0)
                return DedupeScanned(merged);

            for (var i = 0; i < extras.Length; i++)
            {
                var folder = extras[i];
                if (string.IsNullOrWhiteSpace(folder))
                    continue;
                extraFolderCount++;
                merged.AddRange(Host.ScanFolder(folder.Trim()));
            }

            return DedupeScanned(merged);
        }

        static List<VstHostManager.ScannedPlugin> DedupeScanned(
            List<VstHostManager.ScannedPlugin> plugins)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<VstHostManager.ScannedPlugin>(plugins.Count);
            for (var i = 0; i < plugins.Count; i++)
            {
                var p = plugins[i];
                var key = (p.Uid ?? string.Empty) + "|" + (p.FilePath ?? string.Empty);
                if (!seen.Add(key))
                    continue;
                unique.Add(p);
            }

            return unique;
        }

        [AiTool(
            "vst3-scan-folder",
            Title = "VST3 / Scan Folder",
            ReadOnlyHint = true)]
        [Description(
            "Scan a specific folder recursively for VST3 plugins. Empty/null folder = standard folders. " +
            "Requires vst3-host-init.")]
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
                    VstHostToolHelpers.FormatScanned(plugins, maxResults);
            });
        }

        [AiTool(
            "vst3-default-scan-folders",
            Title = "VST3 / Default Scan Folders",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "List OS-standard VST3 scan folder paths. Does not require host init.")]
        public string DefaultScanFolders()
        {
            return MainThread.Instance.Run(() =>
            {
                var folders = VstHostManager.GetDefaultScanFolders();
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
    }
}
