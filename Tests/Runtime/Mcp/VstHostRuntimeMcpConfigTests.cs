using NUnit.Framework;
using jp.kshoji.unity.vst3nativehost.mcp.runtime;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.tests.mcp
{
    public sealed class VstHostRuntimeMcpConfigTests
    {
        [Test]
        public void FormatForMcp_IncludesEnabledFlagAndFolders()
        {
            var cfg = ScriptableObject.CreateInstance<VstHostRuntimeMcpConfig>();
            cfg.mcpEnabled = true;
            cfg.host = "http://localhost:9090";
            cfg.extraScanFolders = new[] { "/tmp/vst3" };

            var text = cfg.FormatForMcp();

            Assert.That(text, Does.Contain("mcpEnabled=True"));
            Assert.That(text, Does.Contain("host=http://localhost:9090"));
            Assert.That(text, Does.Contain("extra[0]=/tmp/vst3"));

            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void LoadFromResources_DoesNotThrow()
        {
            // May be null or an asset — verification projects often have
            // Resources/VstHostRuntimeMcpConfig.asset for Standalone MCP.
            Assert.DoesNotThrow(() =>
            {
                var loaded = VstHostRuntimeMcpConfig.LoadFromResources();
                if (loaded != null)
                    Assert.IsFalse(string.IsNullOrEmpty(loaded.FormatForMcp()));
            });
        }
    }
}
