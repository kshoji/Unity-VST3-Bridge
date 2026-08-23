using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// High-level manager for discovering and instantiating VST3 plugins.
    /// </summary>
    public sealed class VstHostManager : IDisposable
    {
        private static VstHostManager instance;

        private bool initialized;
        private readonly Dictionary<int, LoadedPluginInfo> loadedPlugins =
            new Dictionary<int, LoadedPluginInfo>();

        public int SampleRate { get; private set; }
        public int BlockSize { get; private set; }
        public bool IsInitialized => initialized;
        public IReadOnlyDictionary<int, LoadedPluginInfo> LoadedPlugins => loadedPlugins;

        public static VstHostManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new VstHostManager();
                return instance;
            }
        }

        private VstHostManager() { }

        public readonly struct LoadedPluginInfo
        {
            public readonly string FilePath;
            public readonly string Uid;

            public LoadedPluginInfo(string filePath, string uid)
            {
                FilePath = filePath;
                Uid = uid;
            }
        }

        public struct ScannedPlugin
        {
            public string Uid;
            public string Name;
            public string Vendor;
            public string Category;
            public string FilePath;

            public override string ToString() =>
                $"{Name} ({Vendor}) [{Category}] uid={Uid} path={FilePath}";
        }

        /// <summary>
        /// OS-standard VST3 folders (Windows Common Files / macOS Library /
        /// Linux ~/.vst3 and /usr[/local]/lib/vst3). Prefer <see cref="Scan"/> with a
        /// null/empty folder so the native bridge uses SDK
        /// <c>Module::getModulePaths()</c> for the current platform.
        /// </summary>
        public static IReadOnlyList<string> GetDefaultScanFolders()
        {
            var list = new List<string>(3);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (!string.IsNullOrEmpty(home))
                list.Add(Path.Combine(home, "Library", "Audio", "Plug-Ins", "VST3"));
            list.Add("/Library/Audio/Plug-Ins/VST3");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_LINUX64
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (!string.IsNullOrEmpty(home))
                list.Add(Path.Combine(home, ".vst3"));
            list.Add("/usr/lib/vst3");
            list.Add("/usr/local/lib/vst3");
#else
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            if (!string.IsNullOrEmpty(programData))
                list.Add(Path.Combine(programData, "VST3"));

            // FOLDERID_UserProgramFilesCommon ≈ %LOCALAPPDATA%\Programs\Common
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
                list.Add(Path.Combine(localAppData, "Programs", "Common", "VST3"));
#endif
            return list;
        }

        public bool Initialize(int sampleRate = 44100, int blockSize = 512)
        {
            if (initialized)
            {
                Debug.LogWarning("[VstHost] Already initialized.");
                return true;
            }

            // Unity DSP buffers are often 256–1024; keep headroom for OnAudioFilterRead.
            if (blockSize < 256)
                blockSize = 256;
            if (blockSize > 8192)
                blockSize = 8192;

            var result = VstHostNative.VstHost_Initialize(sampleRate, blockSize);
            if (result == VstHostResult.ErrorAlreadyInitialized)
            {
                // Native DLL can outlive C# after domain reload or missed Play Mode cleanup.
                VstHostNative.VstHost_Terminate();
                result = VstHostNative.VstHost_Initialize(sampleRate, blockSize);
            }

            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] Initialize failed: {result}");
                return false;
            }

            SampleRate = sampleRate;
            BlockSize = blockSize;
            initialized = true;
            loadedPlugins.Clear();
            VstHostLog.Trace($"[VstHost] Initialized (sampleRate={sampleRate}, blockSize={blockSize})");
            return true;
        }

        /// <summary>
        /// Initialize using <see cref="AudioSettings"/> sample rate and DSP buffer size.
        /// Prefer this when using <c>OnAudioFilterRead</c> (see <c>VstHostAudioFilter</c>).
        /// </summary>
        public bool InitializeFromAudioSettings(int blockSizeHeadroom = 2)
        {
            var config = AudioSettings.GetConfiguration();
            var sampleRate = config.sampleRate > 0 ? config.sampleRate : AudioSettings.outputSampleRate;
            var dsp = config.dspBufferSize > 0 ? config.dspBufferSize : 1024;
            var block = Mathf.Max(dsp * Mathf.Max(1, blockSizeHeadroom), 1024);
            return Initialize(sampleRate, block);
        }

        public void Terminate()
        {
            if (!initialized)
            {
                // Editor: C# may have been domain-reloaded while native stayed initialized.
                var orphan = VstHostNative.VstHost_Terminate();
                if (orphan == VstHostResult.Ok)
                {
                    loadedPlugins.Clear();
                    VstHostLog.Trace("[VstHost] Terminated (native was still active).");
                }
                else if (orphan == VstHostResult.ErrorBusy)
                {
                    Debug.LogError(
                        "[VstHost] Terminate timed out (ErrorBusy): a plugin Process is still active. " +
                        "Retry Terminate later; the native host was left initialized.");
                }
                return;
            }

            var result = VstHostNative.VstHost_Terminate();
            if (result == VstHostResult.ErrorBusy)
            {
                Debug.LogError(
                    "[VstHost] Terminate timed out (ErrorBusy): a plugin Process is still active. " +
                    "Retry Terminate later; instances were left loaded.");
                return;
            }

            if (result != VstHostResult.Ok)
                Debug.LogError($"[VstHost] Terminate failed: {result}");

            loadedPlugins.Clear();
            initialized = false;
            VstHostLog.Trace("[VstHost] Terminated.");
        }

        /// <summary>
        /// Scan Windows standard VST3 folders (bundle + flat .vst3).
        /// </summary>
        public List<ScannedPlugin> Scan()
        {
            return ScanFolder(null);
        }

        /// <summary>
        /// Scan a specific folder recursively. Pass null/empty to use standard folders.
        /// </summary>
        public List<ScannedPlugin> ScanFolder(string folderPath)
        {
            if (!initialized)
            {
                Debug.LogError("[VstHost] Not initialized. Call Initialize() first.");
                return new List<ScannedPlugin>();
            }

            if (scanResults == null)
                scanResults = new List<ScannedPlugin>();
            scanResults.Clear();

            var result = VstHostNative.VstHost_ScanFolder(folderPath ?? string.Empty, OnScanCallback, IntPtr.Zero);
            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] Scan failed: {result}");
                return new List<ScannedPlugin>();
            }

            var copy = new List<ScannedPlugin>(scanResults);
            scanResults.Clear();
            VstHostLog.Trace($"[VstHost] Scan found {copy.Count} plugin class(es).");
            return copy;
        }

        [ThreadStatic] private static List<ScannedPlugin> scanResults;

        [MonoPInvokeCallback(typeof(ScanCallback))]
        private static void OnScanCallback(IntPtr infoPtr, IntPtr userData)
        {
            if (scanResults == null)
                scanResults = new List<ScannedPlugin>();

            var info = Marshal.PtrToStructure<VstPluginInfo>(infoPtr);
            scanResults.Add(new ScannedPlugin
            {
                Uid = info.Uid,
                Name = info.Name,
                Vendor = info.Vendor,
                Category = info.Category,
                FilePath = info.FilePath,
            });
        }

        /// <summary>Create a plugin instance. Returns id (&gt;= 1) or -1 on failure.</summary>
        public int CreateInstance(string filePath, string uid = null)
        {
            if (!initialized)
            {
                Debug.LogError("[VstHost] Not initialized.");
                return -1;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[VstHost] CreateInstance: filePath is empty.");
                return -1;
            }

            var result = VstHostNative.VstHost_Load(filePath, uid, out var id);
            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] CreateInstance failed for '{filePath}' uid='{uid}': {result}");
                return -1;
            }

            loadedPlugins[id] = new LoadedPluginInfo(filePath, uid ?? string.Empty);
            VstHostLog.Trace($"[VstHost] CreateInstance id={id} path='{filePath}' uid='{uid}'");
            return id;
        }

        /// <summary>Destroy a previously created instance.</summary>
        public bool DestroyInstance(int id)
        {
            if (!initialized)
                return true;

            var result = VstHostNative.VstHost_Unload(id);
            if (result == VstHostResult.ErrorBusy)
            {
                Debug.LogError(
                    $"[VstHost] DestroyInstance timed out for id={id} (ErrorBusy): " +
                    "plugin still processing. Retry Unload later or call Terminate.");
                return false;
            }

            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] DestroyInstance failed for id={id}: {result}");
                return false;
            }

            loadedPlugins.Remove(id);
            VstHostLog.Trace($"[VstHost] DestroyInstance id={id}");
            return true;
        }

        // Backward-compatible aliases
        public int LoadPlugin(string filePath, string uid = null) => CreateInstance(filePath, uid);
        public bool UnloadPlugin(int id) => DestroyInstance(id);

        /// <summary>
        /// Enqueue a MIDI 1.0 short message (any thread; no Unity API / logging here).
        /// Failures and Activity Monitor lines are delivered on the main thread via
        /// <see cref="VstHostActivity.PumpMainThread"/>.
        /// </summary>
        public bool SendMidi1(int pluginId, byte status, byte data1, byte data2)
        {
            if (!initialized) return false;

            var result = VstHostNative.VstHost_SendMidi1(pluginId, status, data1, data2);
            if (result != VstHostResult.Ok)
            {
                VstHostActivity.RecordMidi1Fail(pluginId, result);
                return false;
            }

            VstHostActivity.EnqueueMidi1(pluginId, status, data1, data2);
            return true;
        }

        /// <summary>Manual Note On (MIDI-less workflow). Channel 0–15.</summary>
        public bool NoteOn(int pluginId, int channel, int note, int velocity = 100)
        {
            Midi1Util.NoteOn(channel, note, velocity, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        /// <summary>Manual Note Off (MIDI-less workflow). Channel 0–15.</summary>
        public bool NoteOff(int pluginId, int channel, int note, int velocity = 0)
        {
            Midi1Util.NoteOff(channel, note, velocity, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        public bool ControlChange(int pluginId, int channel, int controller, int value)
        {
            Midi1Util.ControlChange(channel, controller, value, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        public bool ProgramChange(int pluginId, int channel, int program)
        {
            Midi1Util.ProgramChange(channel, program, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        public bool PitchBend(int pluginId, int channel, int amount14)
        {
            Midi1Util.PitchBend(channel, amount14, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        public bool ChannelAftertouch(int pluginId, int channel, int pressure)
        {
            Midi1Util.ChannelAftertouch(channel, pressure, out var s, out var d1, out var d2);
            return SendMidi1(pluginId, s, d1, d2);
        }

        /// <summary>
        /// Run one audio block on the native bridge (audio-thread safe).
        /// Pass null inputs for instruments (silence). Planar stereo buffers.
        /// </summary>
        public unsafe bool Process(int pluginId, float[] inputL, float[] inputR, float[] outputL, float[] outputR, int numFrames)
        {
            if (!initialized || outputL == null || outputR == null || numFrames <= 0)
                return false;
            if (outputL.Length < numFrames || outputR.Length < numFrames)
                return false;
            if (numFrames > BlockSize)
                return false;

            fixed (float* outL = outputL)
            fixed (float* outR = outputR)
            {
                if (inputL != null && inputR != null && inputL.Length >= numFrames && inputR.Length >= numFrames)
                {
                    fixed (float* inL = inputL)
                    fixed (float* inR = inputR)
                    {
                        return VstHostNative.VstHost_Process(pluginId, inL, inR, outL, outR, numFrames)
                               == VstHostResult.Ok;
                    }
                }

                return VstHostNative.VstHost_Process(pluginId, null, null, outL, outR, numFrames)
                       == VstHostResult.Ok;
            }
        }

        /// <summary>
        /// Like <see cref="Process"/>, but feeds Aux input bus 0 (second audio input) from
        /// <paramref name="sidechainL"/> / <paramref name="sidechainR"/>.
        /// Pass null sidechain buffers to behave like <see cref="Process"/> (silent Aux).
        /// Mono Aux plugins receive L duplicated when R is null.
        /// Audio-thread safe. Planar stereo buffers.
        /// </summary>
        public unsafe bool ProcessWithSidechain(
            int pluginId,
            float[] inputL,
            float[] inputR,
            float[] sidechainL,
            float[] sidechainR,
            float[] outputL,
            float[] outputR,
            int numFrames)
        {
            if (!initialized || outputL == null || outputR == null || numFrames <= 0)
                return false;
            if (outputL.Length < numFrames || outputR.Length < numFrames)
                return false;
            if (numFrames > BlockSize)
                return false;

            var hasMain = inputL != null && inputR != null
                          && inputL.Length >= numFrames && inputR.Length >= numFrames;
            var hasSide = sidechainL != null && sidechainL.Length >= numFrames;
            if (hasSide && sidechainR != null && sidechainR.Length < numFrames)
                return false;

            fixed (float* outL = outputL)
            fixed (float* outR = outputR)
            {
                if (hasMain && hasSide)
                {
                    fixed (float* inL = inputL)
                    fixed (float* inR = inputR)
                    fixed (float* scL = sidechainL)
                    {
                        if (sidechainR != null)
                        {
                            fixed (float* scR = sidechainR)
                            {
                                return VstHostNative.VstHost_ProcessWithSidechain(
                                           pluginId, inL, inR, scL, scR, outL, outR, numFrames)
                                       == VstHostResult.Ok;
                            }
                        }

                        return VstHostNative.VstHost_ProcessWithSidechain(
                                   pluginId, inL, inR, scL, null, outL, outR, numFrames)
                               == VstHostResult.Ok;
                    }
                }

                if (hasMain)
                {
                    fixed (float* inL = inputL)
                    fixed (float* inR = inputR)
                    {
                        return VstHostNative.VstHost_ProcessWithSidechain(
                                   pluginId, inL, inR, null, null, outL, outR, numFrames)
                               == VstHostResult.Ok;
                    }
                }

                if (hasSide)
                {
                    fixed (float* scL = sidechainL)
                    {
                        if (sidechainR != null)
                        {
                            fixed (float* scR = sidechainR)
                            {
                                return VstHostNative.VstHost_ProcessWithSidechain(
                                           pluginId, null, null, scL, scR, outL, outR, numFrames)
                                       == VstHostResult.Ok;
                            }
                        }

                        return VstHostNative.VstHost_ProcessWithSidechain(
                                   pluginId, null, null, scL, null, outL, outR, numFrames)
                               == VstHostResult.Ok;
                    }
                }

                return VstHostNative.VstHost_ProcessWithSidechain(
                           pluginId, null, null, null, null, outL, outR, numFrames)
                       == VstHostResult.Ok;
            }
        }

        public List<VstParamInfo> GetParameters(int pluginId)
        {
            var list = new List<VstParamInfo>();
            if (!initialized) return list;

            var result = VstHostNative.VstHost_GetParameterCount(pluginId, out var count);
            if (result != VstHostResult.Ok || count <= 0)
                return list;

            for (var i = 0; i < count; i++)
            {
                if (VstHostNative.VstHost_GetParameterInfo(pluginId, i, out var info) == VstHostResult.Ok)
                    list.Add(info);
            }
            return list;
        }

        public bool TryGetParameterNormalized(int pluginId, uint paramId, out double value)
        {
            value = 0;
            if (!initialized) return false;
            return VstHostNative.VstHost_GetParameterNormalized(pluginId, paramId, out value) == VstHostResult.Ok;
        }

        public bool SetParameterNormalized(int pluginId, uint paramId, double value)
        {
            if (!initialized) return false;
            var result = VstHostNative.VstHost_SetParameterNormalized(pluginId, paramId, value);
            if (result != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] SetParameterNormalized failed id={pluginId} param={paramId}: {result}");
                return false;
            }

            VstHostActivity.Raise(
                VstHostActivityKind.Parameter,
                pluginId,
                $"param={paramId} value={value:0.###}");
            return true;
        }

        public List<string> GetPrograms(int pluginId)
        {
            var list = new List<string>();
            if (!initialized) return list;

            var result = VstHostNative.VstHost_GetProgramCount(pluginId, out var count);
            if (result != VstHostResult.Ok || count <= 0)
                return list;

            var buffer = Marshal.AllocHGlobal(256 * sizeof(char));
            try
            {
                for (var i = 0; i < count; i++)
                {
                    if (VstHostNative.VstHost_GetProgramName(pluginId, i, buffer, 256) != VstHostResult.Ok)
                    {
                        list.Add($"Program {i}");
                        continue;
                    }
                    list.Add(Marshal.PtrToStringUni(buffer) ?? $"Program {i}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return list;
        }

        public bool SetProgram(int pluginId, int index)
        {
            if (!initialized) return false;
            var result = VstHostNative.VstHost_SetProgram(pluginId, index);
            if (result != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] SetProgram failed id={pluginId} index={index}: {result}");
                return false;
            }

            VstHostActivity.Raise(VstHostActivityKind.Program, pluginId, $"program={index}");
            return true;
        }

        public byte[] GetState(int pluginId)
        {
            if (!initialized) return null;

            var probe = VstHostNative.VstHost_GetState(pluginId, null, 0, out var needed);
            if (probe != VstHostResult.ErrorBufferTooSmall && probe != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] GetState failed id={pluginId}: {probe}");
                return null;
            }
            if (needed <= 0) return Array.Empty<byte>();

            var buffer = new byte[needed];
            var result = VstHostNative.VstHost_GetState(pluginId, buffer, buffer.Length, out var written);
            if (result != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] GetState copy failed id={pluginId}: {result}");
                return null;
            }
            if (written != buffer.Length)
                Array.Resize(ref buffer, written);
            return buffer;
        }

        public bool SetState(int pluginId, byte[] state)
        {
            if (!initialized || state == null || state.Length == 0) return false;
            var result = VstHostNative.VstHost_SetState(pluginId, state, state.Length);
            if (result != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] SetState failed id={pluginId}: {result}");
                return false;
            }

            VstHostActivity.Raise(VstHostActivityKind.State, pluginId, $"stateBytes={state.Length}");
            return true;
        }

        public void Dispose()
        {
            Terminate();
            if (instance == this)
                instance = null;
        }
    }
}
