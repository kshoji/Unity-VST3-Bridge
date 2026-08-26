#nullable enable
using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    /// <summary>
    /// Runtime MCP connection settings for Standalone builds.
    /// Place under a <c>Resources</c> folder (e.g. Resources/VstHostRuntimeMcpConfig.asset).
    /// </summary>
    [CreateAssetMenu(
        fileName = "VstHostRuntimeMcpConfig",
        menuName = "VST3 Host/Runtime MCP Config",
        order = 100)]
    public sealed class VstHostRuntimeMcpConfig : ScriptableObject
    {
        [Tooltip("When false, the runtime MCP connection is not started.")]
        public bool mcpEnabled;

        [Tooltip("MCP server URL (e.g. http://localhost:8080).")]
        public string host = "http://localhost:8080";

        [Tooltip("Bearer token presented to the MCP server.")]
        public string token = "";

        [Tooltip("Call vst3-host-init (InitializeFromAudioSettings) after MCP connects.")]
        public bool autoInitializeHostOnStart = true;

        [Tooltip("Optional extra VST3 scan folders (absolute paths).")]
        public string[] extraScanFolders = System.Array.Empty<string>();

        [Tooltip("Preferred plugin name substring for auto-load helpers (future use).")]
        public string preferredPluginNameContains = "";

        const string DefaultResourceName = "VstHostRuntimeMcpConfig";

        /// <summary>Load the config from Resources, or null when missing.</summary>
        public static VstHostRuntimeMcpConfig? LoadFromResources()
        {
            var cfg = Resources.Load<VstHostRuntimeMcpConfig>(DefaultResourceName);
            if (cfg != null)
                return cfg;

            var all = Resources.LoadAll<VstHostRuntimeMcpConfig>(string.Empty);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>Text snapshot for MCP resources / debugging.</summary>
        public string FormatForMcp()
        {
            var folders = extraScanFolders ?? Array.Empty<string>();
            var sb = new System.Text.StringBuilder();
            sb.Append($"mcpEnabled={mcpEnabled}");
            sb.Append($" host={host}");
            sb.Append($" autoInitializeHostOnStart={autoInitializeHostOnStart}");
            sb.Append($" preferredPluginNameContains={preferredPluginNameContains}");
            sb.Append($" extraScanFolderCount={folders.Length}");
            for (var i = 0; i < folders.Length; i++)
            {
                sb.Append('\n');
                sb.Append($"extra[{i}]={folders[i]}");
            }

            return sb.ToString();
        }
    }
}
