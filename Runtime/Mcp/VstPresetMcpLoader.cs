#nullable enable
using System;
using jp.kshoji.unity.vst3nativehost;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    /// <summary>Load <see cref="VstPresetAsset"/> for MCP preset tools (Editor paths + Resources).</summary>
    internal static class VstPresetMcpLoader
    {
        public static VstPresetAsset? Load(string pathOrResource, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(pathOrResource))
            {
                error = "[Error] preset path/resource name is required.";
                return null;
            }

            var path = pathOrResource.Trim().Replace('\\', '/');

#if UNITY_EDITOR
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    path += ".asset";
                var editorPreset = AssetDatabase.LoadAssetAtPath<VstPresetAsset>(path);
                if (editorPreset == null)
                {
                    error = $"[Error] VstPresetAsset not found at '{path}'.";
                    return null;
                }

                return editorPreset;
            }
#endif
            var resourcePath = path;
            if (resourcePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                resourcePath = resourcePath.Substring(0, resourcePath.Length - ".asset".Length);
            const string resourcesPrefix = "Resources/";
            if (resourcePath.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
                resourcePath = resourcePath.Substring(resourcesPrefix.Length);

            var preset = UnityEngine.Resources.Load<VstPresetAsset>(resourcePath);
            if (preset != null)
                return preset;

            var all = UnityEngine.Resources.LoadAll<VstPresetAsset>(string.Empty);
            for (var i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p == null)
                    continue;
                if (string.Equals(p.name, resourcePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.name, path, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            error =
                $"[Error] VstPresetAsset not found for '{pathOrResource}'. " +
                "Use Resources path (e.g. Presets/MyPreset) or Assets/... in Editor.";
            return null;
        }

        public static System.Collections.Generic.List<VstPresetAsset> LoadAllFromResources()
        {
            var all = UnityEngine.Resources.LoadAll<VstPresetAsset>(string.Empty);
            var list = new System.Collections.Generic.List<VstPresetAsset>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    list.Add(all[i]);
            }

            return list;
        }
    }
}
