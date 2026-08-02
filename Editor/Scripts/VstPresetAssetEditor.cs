using System.IO;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    [CustomEditor(typeof(VstPresetAsset))]
    internal sealed class VstPresetAssetEditor : UnityEditor.Editor
    {
        private int capturePluginId = 1;
        private int applyPluginId = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (VstPresetAsset)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture / Apply (Play Mode)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !VstHostManager.Instance.IsInitialized))
            {
                capturePluginId = EditorGUILayout.IntField("Capture Plugin Id", capturePluginId);
                if (GUILayout.Button("Capture State From Plugin"))
                {
                    if (asset.CaptureFrom(capturePluginId))
                    {
                        EditorUtility.SetDirty(asset);
                        Debug.Log($"[VstPresetAsset] Captured {asset.State.Length} bytes into '{asset.name}'.");
                    }
                    else
                    {
                        Debug.LogWarning("[VstPresetAsset] Capture failed.");
                    }
                }

                applyPluginId = EditorGUILayout.IntField("Apply Plugin Id", applyPluginId);
                using (new EditorGUI.DisabledScope(!asset.HasState))
                {
                    if (GUILayout.Button("Apply State To Plugin"))
                    {
                        if (!asset.ApplyTo(applyPluginId))
                            Debug.LogWarning("[VstPresetAsset] Apply failed.");
                    }
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Capture / Apply require Play Mode with an initialized VST host.", MessageType.Info);
        }

        [MenuItem("Assets/Create/VST3 Host/Preset From Selection", false, 121)]
        private static void CreatePresetAsset()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets";
            else if (!Directory.Exists(path))
                path = Path.GetDirectoryName(path) ?? "Assets";

            var asset = ScriptableObject.CreateInstance<VstPresetAsset>();
            asset.DisplayName = "New VST Preset";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, "VstPreset.asset"));
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
