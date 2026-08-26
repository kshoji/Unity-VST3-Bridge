using System.Linq;
using System.Reflection;
using NUnit.Framework;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.tests
{
    public sealed class McpRuntimeAssemblyRegistrationTests
    {
        const string Primary = "jp.kshoji.unity.vst3nativehost.Mcp.Runtime";

        [Test]
        public void IsOptionalRuntimeAssembly_AcceptsOptionalRuntimeAssemblies()
        {
            Assert.IsTrue(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(
                "jp.kshoji.unity.vst3nativehost.Mcp.Midi.Runtime", Primary));
            Assert.IsTrue(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(
                "jp.kshoji.unity.vst3nativehost.Mcp.Timeline.Runtime", Primary));
        }

        [Test]
        public void IsOptionalRuntimeAssembly_RejectsPrimaryCoreAndEditor()
        {
            Assert.IsFalse(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(Primary, Primary));
            Assert.IsFalse(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(
                "jp.kshoji.unity.vst3nativehost.Mcp.Core", Primary));
            Assert.IsFalse(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(
                "jp.kshoji.unity.vst3nativehost.Mcp", Primary));
            Assert.IsFalse(McpRuntimeAssemblyRegistration.IsOptionalRuntimeAssembly(null, Primary));
        }
    }
}
