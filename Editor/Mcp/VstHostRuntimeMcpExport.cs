#nullable enable
using System.IO;
using jp.kshoji.unity.vst3nativehost.Editor;
using jp.kshoji.unity.vst3nativehost.mcp.runtime;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    /// <summary>Editor helper: copy Project Settings scan prefs into Resources runtime MCP config.</summary>
    static class VstHostRuntimeMcpExport
    {
        public const string DefaultAssetPath = "Assets/Resources/VstHostRuntimeMcpConfig.asset";

        [MenuItem("Window/VST3 Host/Export Runtime MCP Config to Resources")]
        public static void ExportFromMenu()
        {
            var path = ExportToResources(DefaultAssetPath, out var created);
            if (string.IsNullOrEmpty(path))
                return;

            EditorUtility.DisplayDialog(
                "VST3 Host",
                created
                    ? $"Created {path}\n\nCopied scan folders and auto-init from Project Settings.\n" +
                      "Set mcpEnabled and token before Standalone builds."
                    : $"Updated {path}\n\nCopied scan folders and auto-init from Project Settings.\n" +
                      "Review mcpEnabled and token before Standalone builds.",
                "OK");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
        }

        /// <summary>
        /// Creates or updates <see cref="VstHostRuntimeMcpConfig"/> at the given Assets path.
        /// Does not enable MCP or overwrite host/token unless the asset is newly created.
        /// </summary>
        public static string? ExportToResources(string assetPath, out bool created)
        {
            created = false;
            assetPath = assetPath.Trim().Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[VST3 Host] Export path must start with Assets/.");
                return null;
            }

            if (!assetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                assetPath += ".asset";

            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir))
                EnsureAssetFolder(dir);

            var cfg = AssetDatabase.LoadAssetAtPath<VstHostRuntimeMcpConfig>(assetPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<VstHostRuntimeMcpConfig>();
                AssetDatabase.CreateAsset(cfg, assetPath);
                created = true;
            }

            var project = VstHostProjectSettings.instance;
            cfg.autoInitializeHostOnStart = project.AutoInitializeOnPlay;
            cfg.preferredPluginNameContains = project.PreferredPluginNameContains ?? string.Empty;
            cfg.extraScanFolders = project.ExtraScanFolders ?? System.Array.Empty<string>();

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return assetPath;
        }

        static void EnsureAssetFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
