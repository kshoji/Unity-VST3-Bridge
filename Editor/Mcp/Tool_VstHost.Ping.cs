#nullable enable
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-ping",
            Title = "VST3 / Ping",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Reachability check for Unity Plugin Host for VST3 MCP tools. " +
            "Returns package id, version, MCP asmdef name, and Unity version. " +
            "Safe in Edit Mode and Play Mode. Does not initialize the native host.")]
        public string Ping()
        {
            return MainThread.Instance.Run(() =>
            {
                TryGetPackageVersion(out var version, out var source);
                var mcpAsm = typeof(Tool_VstHost).Assembly.GetName().Name ?? McpAssemblyName;
                var sb = new StringBuilder();
                sb.Append("[Success] vst3-ping ok");
                sb.Append($" package={PackageName}");
                sb.Append($" version={version}");
                sb.Append($" versionSource={source}");
                sb.Append($" mcpAssembly={mcpAsm}");
                sb.Append($" runtimeAssembly={typeof(jp.kshoji.unity.vst3nativehost.VstHostManager).Assembly.GetName().Name}");
                sb.Append($" unity={Application.unityVersion}");
                sb.Append($" playMode={IsPlayMode}");
                return sb.ToString();
            });
        }
    }
}
