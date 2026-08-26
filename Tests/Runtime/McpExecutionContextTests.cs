using NUnit.Framework;
using jp.kshoji.unity.vst3nativehost.mcp.core;

namespace jp.kshoji.unity.vst3nativehost.tests
{
    public sealed class McpExecutionContextTests
    {
        [Test]
        public void SessionKind_IsNonEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(McpExecutionContext.SessionKind));
        }

        [Test]
        public void VstHostToolHelpers_ErrorMessages_ContainToolId()
        {
            const string toolId = "vst3-test-tool";
            Assert.That(VstHostToolHelpers.ErrorRequiresPlayMode(toolId), Does.Contain(toolId));
            Assert.That(VstHostToolHelpers.ErrorRequiresInitialized(toolId), Does.Contain(toolId));
            Assert.That(VstHostToolHelpers.ErrorPluginNotLoaded(toolId, 1), Does.Contain(toolId));
        }
    }
}
