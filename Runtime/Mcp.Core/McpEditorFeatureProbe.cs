#nullable enable
using System;
using System.Linq;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.core
{
    /// <summary>Editor-only scripting define probes (safe no-op in builds).</summary>
    public static class McpEditorFeatureProbe
    {
        public static bool HasScriptingDefine(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return false;

#if UNITY_EDITOR
            var group = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
            var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            var defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(named);
#else
            var defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
            return defines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(d => string.Equals(d.Trim(), symbol, StringComparison.Ordinal));
#else
            _ = symbol;
            return false;
#endif
        }
    }
}
