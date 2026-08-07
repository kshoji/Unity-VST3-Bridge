using NUnit.Framework;

namespace jp.kshoji.unity.vst3nativehost.Editor.Tests
{
    public sealed class VstHostBuildVerifyTests
    {
        [Test]
        public void VerifyPluginPlatforms_NativeBinariesConfigured()
        {
            Assert.IsTrue(
                VstHostBuildVerify.VerifyPluginPlatforms(out var message),
                message);
            StringAssert.Contains("OK", message);
        }
    }
}
