#nullable enable
using System;
using System.Reflection;
using com.IvanMurzak.McpPlugin;

namespace jp.kshoji.unity.vst3nativehost.mcp.core
{
    /// <summary>Registers optional <c>*.Mcp.*.Runtime</c> tool assemblies with Unity-MCP.</summary>
    public static class McpRuntimeAssemblyRegistration
    {
        const string Prefix = "jp.kshoji.unity.vst3nativehost.Mcp.";

        public static void RegisterToolsAndResources(object mcpBuilder, Assembly primaryAssembly)
        {
            InvokeBuilder(mcpBuilder, "WithToolsFromAssembly", primaryAssembly);
            InvokeBuilder(mcpBuilder, "WithResourcesFromAssembly", primaryAssembly);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name;
                if (!IsOptionalRuntimeAssembly(name, primaryAssembly.GetName().Name))
                    continue;
                InvokeBuilder(mcpBuilder, "WithToolsFromAssembly", asm);
                InvokeBuilder(mcpBuilder, "WithResourcesFromAssembly", asm);
            }
        }

        /// <summary>True for optional <c>*.Mcp.*.Runtime</c> assemblies other than the primary Runtime asm.</summary>
        public static bool IsOptionalRuntimeAssembly(string? assemblyName, string? primaryName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;
            if (assemblyName == primaryName)
                return false;
            if (assemblyName == "jp.kshoji.unity.vst3nativehost.Mcp.Core")
                return false;
            return assemblyName.StartsWith(Prefix, StringComparison.Ordinal)
                   && assemblyName.EndsWith(".Runtime", StringComparison.Ordinal);
        }

        static void InvokeBuilder(object mcpBuilder, string methodName, Assembly assembly)
        {
            var method = mcpBuilder.GetType().GetMethod(methodName, new[] { typeof(Assembly) });
            method?.Invoke(mcpBuilder, new object[] { assembly });
        }
    }
}
