using NUnit.Framework;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Tests
{
    public sealed class VstMidiParameterMappingTests
    {
        [Test]
        public void MapNormalized_AppliesInvertAndRange()
        {
            var binding = VstMidiParameterMapping.Binding.CreateDefault(
                VstMidiParameterMapping.SourceType.ControlChange, 1, 42u);
            binding.minNormalized = 0.25f;
            binding.maxNormalized = 0.75f;
            binding.invert = true;

            Assert.AreEqual(0.75f, binding.MapNormalized(0f), 1e-5f);
            Assert.AreEqual(0.25f, binding.MapNormalized(1f), 1e-5f);
            Assert.AreEqual(0.5f, binding.MapNormalized(0.5f), 1e-5f);
        }

        [Test]
        public void UpsertControlChangeBinding_ReplacesSameController()
        {
            var so = ScriptableObject.CreateInstance<VstMidiParameterMapping>();
            so.UpsertControlChangeBinding(channel: 0, controller: 74, parameterId: 10u);
            so.UpsertControlChangeBinding(channel: 0, controller: 74, parameterId: 99u);

            Assert.AreEqual(1, so.Bindings.Length);
            Assert.AreEqual(99u, so.Bindings[0].parameterId);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void UpsertPitchBendBinding_AddsEntry()
        {
            var so = ScriptableObject.CreateInstance<VstMidiParameterMapping>();
            so.UpsertPitchBendBinding(channel: -1, parameterId: 7u);

            Assert.AreEqual(1, so.Bindings.Length);
            Assert.AreEqual(VstMidiParameterMapping.SourceType.PitchBend, so.Bindings[0].source);
            Assert.AreEqual(7u, so.Bindings[0].parameterId);
            Object.DestroyImmediate(so);
        }
    }

    public sealed class VstPresetAssetTests
    {
        [Test]
        public void HasState_FalseWhenEmpty()
        {
            var asset = ScriptableObject.CreateInstance<VstPresetAsset>();
            Assert.IsFalse(asset.HasState);
            Assert.IsFalse(string.IsNullOrEmpty(asset.DisplayName));
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void DisplayName_FallsBackToAssetName()
        {
            var asset = ScriptableObject.CreateInstance<VstPresetAsset>();
            asset.name = "MyPreset";
            asset.DisplayName = string.Empty;
            Assert.AreEqual("MyPreset", asset.DisplayName);
            asset.DisplayName = "Bright Pad";
            Assert.AreEqual("Bright Pad", asset.DisplayName);
            Object.DestroyImmediate(asset);
        }
    }
}
