#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    /// <summary>EventBus hook names for VST3 Host Visual Scripting nodes.</summary>
    public static class VstVisualScriptingHooks
    {
        public const string ParameterChanged = "VstHost.ParameterChanged";
        public const string Activity = "VstHost.Activity";
    }
}
#endif
