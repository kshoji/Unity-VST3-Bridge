using System.Linq;
using System.Reflection;
using com.IvanMurzak.McpPlugin;
using NUnit.Framework;
using jp.kshoji.unity.vst3nativehost.mcp.runtime;

namespace jp.kshoji.unity.vst3nativehost.tests.mcp
{
    public sealed class VstHostMcpToolSmokeTests
    {
        [Test]
        public void Tool_VstHost_RegistersHostStatusAndParamGet()
        {
            var type = typeof(Tool_VstHost);
            Assert.IsTrue(type.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().Name == "AiToolTypeAttribute"));

            AssertToolId(type, "HostStatus", "vst3-host-status");
            AssertToolId(type, "ParamGet", "vst3-param-get");
        }

        static void AssertToolId(System.Type type, string methodName, string expectedId)
        {
            var method = type.GetMethod(methodName);
            Assert.IsNotNull(method, methodName);
            var data = method!.GetCustomAttributesData()
                .FirstOrDefault(c => c.AttributeType == typeof(AiToolAttribute));
            Assert.IsNotNull(data, methodName);
            Assert.AreEqual(expectedId, data!.ConstructorArguments[0].Value as string);
        }

        [Test]
        public void HostStatus_ReturnsSuccess()
        {
            var tool = new Tool_VstHost();
            var result = tool.HostStatus();

            Assert.That(result, Does.StartWith("[Success] vst3-host-status"));
            Assert.That(result, Does.Contain("session="));
        }

        [Test]
        public void ParamGet_ReturnsErrorWhenPluginNotLoaded()
        {
            var tool = new Tool_VstHost();
            var result = tool.ParamGet(999, null, null);

            Assert.That(result, Does.StartWith("[Error]"));
            Assert.That(result, Does.Contain("vst3-param-get"));
        }
    }
}
