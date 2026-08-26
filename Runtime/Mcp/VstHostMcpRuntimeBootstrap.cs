#nullable enable
using System;
using System.Reflection;
using System.Threading.Tasks;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.Unity.MCP;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.runtime
{
    /// <summary>
    /// Starts <see cref="UnityMcpPluginRuntime"/> in Standalone builds when
    /// <see cref="VstHostRuntimeMcpConfig.mcpEnabled"/> is true.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VST3 Host/MCP Runtime Bootstrap")]
    public sealed class VstHostMcpRuntimeBootstrap : MonoBehaviour
    {
        [SerializeField]
        VstHostRuntimeMcpConfig? config;

        [SerializeField]
        bool useSerializedConfig;

        static bool s_started;

        public static void AutoStart()
        {
#if UNITY_EDITOR
            return;
#else
            if (s_started)
                return;

            var cfg = VstHostRuntimeMcpConfig.LoadFromResources();
            if (cfg == null || !cfg.mcpEnabled)
                return;

            TryConnect(cfg);
#endif
        }

        void Awake()
        {
#if !UNITY_EDITOR
            var cfg = useSerializedConfig && config != null
                ? config
                : VstHostRuntimeMcpConfig.LoadFromResources();
            if (cfg == null || !cfg.mcpEnabled)
                return;

            TryConnect(cfg);
#endif
        }

        internal static void TryConnect(VstHostRuntimeMcpConfig cfg)
        {
            if (s_started)
                return;

            try
            {
                var runtimeAssembly = typeof(Tool_VstHost).Assembly;
                UnityMcpPluginRuntime.Initialize(mcpBuilder =>
                    {
                        mcpBuilder.WithConfig(c =>
                        {
                            c.Host = string.IsNullOrWhiteSpace(cfg.host)
                                ? "http://localhost:8080"
                                : cfg.host.Trim();
                            var token = cfg.token ?? string.Empty;
                            c.CredentialProvider = () => Task.FromResult<string?>(
                                string.IsNullOrEmpty(token) ? null : token);
                            // McpPlugin ctor runs GenerateSkillFilesIfNeeded; without a root it
                            // logs InvalidOperationException (MCP-Plugin-dotnet #107 / Unity-MCP #766).
                            // Standalone has no Unity project folder — use persistentDataPath.
                            c.ProjectRootPath = Application.persistentDataPath;
                        });
                        McpRuntimeAssemblyRegistration.RegisterToolsAndResources(
                            mcpBuilder, runtimeAssembly);
                    })
                    .Build()
                    .Connect();

                s_started = true;
                Debug.Log(
                    "[VST3 Host] Runtime MCP connected. " +
                    $"assembly={runtimeAssembly.GetName().Name} host={cfg.host}");

                if (cfg.autoInitializeHostOnStart)
                {
                    var host = VstHostManager.Instance;
                    if (!host.IsInitialized)
                    {
                        host.InitializeFromAudioSettings(2);
                        Debug.Log(
                            "[VST3 Host] Runtime auto host-init " +
                            $"sr={host.SampleRate} block={host.BlockSize}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VST3 Host] Runtime MCP connect failed: {ex.Message}");
            }
        }

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RuntimeInitialize()
        {
            AutoStart();
        }
#endif
    }
}
