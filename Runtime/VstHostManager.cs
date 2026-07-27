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
        /// Windows standard VST3 folders (Common Files + user Common Files).
        /// </summary>
        public static IReadOnlyList<string> GetDefaultScanFolders()
        {
            var list = new List<string>(2);
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            if (!string.IsNullOrEmpty(programData))
                list.Add(Path.Combine(programData, "VST3"));

            // FOLDERID_UserProgramFilesCommon ≈ %LOCALAPPDATA%\Programs\Common
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
                list.Add(Path.Combine(localAppData, "Programs", "Common", "VST3"));

            return list;
        }

        public bool Initialize(int sampleRate = 44100, int blockSize = 512)
        {
            if (initialized)
            {
                Debug.LogWarning("[VstHost] Already initialized.");
                return true;
            }

            var result = VstHostNative.VstHost_Initialize(sampleRate, blockSize);
            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] Initialize failed: {result}");
                return false;
            }

            SampleRate = sampleRate;
            BlockSize = blockSize;
            initialized = true;
            Debug.Log($"[VstHost] Initialized (sampleRate={sampleRate}, blockSize={blockSize})");
            return true;
        }

        public void Terminate()
        {
            if (!initialized) return;

            var result = VstHostNative.VstHost_Terminate();
            if (result != VstHostResult.Ok)
                Debug.LogError($"[VstHost] Terminate failed: {result}");

            loadedPlugins.Clear();
            initialized = false;
            Debug.Log("[VstHost] Terminated.");
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
            Debug.Log($"[VstHost] Scan found {copy.Count} plugin class(es).");
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
            Debug.Log($"[VstHost] CreateInstance id={id} path='{filePath}' uid='{uid}'");
            return id;
        }

        /// <summary>Destroy a previously created instance.</summary>
        public bool DestroyInstance(int id)
        {
            if (!initialized)
            {
                Debug.LogError("[VstHost] Not initialized.");
                return false;
            }

            var result = VstHostNative.VstHost_Unload(id);
            if (result != VstHostResult.Ok)
            {
                Debug.LogError($"[VstHost] DestroyInstance failed for id={id}: {result}");
                return false;
            }

            loadedPlugins.Remove(id);
            Debug.Log($"[VstHost] DestroyInstance id={id}");
            return true;
        }

        // Backward-compatible aliases
        public int LoadPlugin(string filePath, string uid = null) => CreateInstance(filePath, uid);
        public bool UnloadPlugin(int id) => DestroyInstance(id);

        public bool SendMidi1(int pluginId, byte status, byte data1, byte data2)
        {
            if (!initialized) return false;

            var result = VstHostNative.VstHost_SendMidi1(pluginId, status, data1, data2);
            if (result != VstHostResult.Ok)
            {
                Debug.LogWarning($"[VstHost] SendMidi1 failed for id={pluginId}: {result}");
                return false;
            }
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

        public void Dispose()
        {
            Terminate();
            if (instance == this)
                instance = null;
        }
    }
}
