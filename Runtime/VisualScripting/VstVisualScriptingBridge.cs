#if FEATURE_USE_VISUALSCRIPTING && UNITY_2021_1_OR_NEWER
using Unity.VisualScripting;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.visualscripting
{
    /// <summary>
    /// Forwards <see cref="VstHostActivity"/> to Visual Scripting EventBus on this GameObject.
    /// Required for On VST Parameter Changed / On VST Host Activity event units.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstVisualScriptingBridge : MonoBehaviour
    {
        [SerializeField] private bool forwardParameterChanges = true;
        [SerializeField] private bool forwardAllActivity;

        private void OnEnable()
        {
            VstHostActivity.Raised += OnActivity;
        }

        private void OnDisable()
        {
            VstHostActivity.Raised -= OnActivity;
        }

        private void OnActivity(VstHostActivityEntry entry)
        {
            if (forwardAllActivity)
            {
                EventBus.Trigger(
                    new EventHook(VstVisualScriptingHooks.Activity, gameObject),
                    entry);
            }

            if (forwardParameterChanges && entry.Kind == VstHostActivityKind.Parameter)
            {
                EventBus.Trigger(
                    new EventHook(VstVisualScriptingHooks.ParameterChanged, gameObject),
                    entry);
            }
        }
    }
}
#endif
