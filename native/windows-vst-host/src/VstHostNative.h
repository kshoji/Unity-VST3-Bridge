#pragma once

#include <cstdint>

#ifdef _WIN32
#define VSTHOST_API extern "C" __declspec(dllexport)
#else
#define VSTHOST_API extern "C" __attribute__((visibility("default")))
#endif

enum VstHostResult : int32_t
{
    kVstHostOk = 0,
    kVstHostErrorNotInitialized = -1,
    kVstHostErrorAlreadyInitialized = -2,
    kVstHostErrorInvalidId = -3,
    kVstHostErrorLoadFailed = -4,
    kVstHostErrorScanFailed = -5,
    kVstHostErrorProcessFailed = -6,
    kVstHostErrorInvalidArgument = -7,
};

using VstPluginId = int32_t;

struct VstPluginInfo
{
    const char16_t* uid;
    const char16_t* name;
    const char16_t* vendor;
    const char16_t* category;
    const char16_t* filePath;
};

using ScanCallbackFn = void (*)(const VstPluginInfo* info, void* userData);

// --- Lifecycle ---
VSTHOST_API VstHostResult VstHost_Initialize(int32_t sampleRate, int32_t blockSize);
VSTHOST_API VstHostResult VstHost_Terminate();

// --- Scan ---
// folderPath: UTF-16 path to scan recursively for .vst3 (bundle or flat).
// If folderPath is null or empty, scans Windows standard VST3 folders.
VSTHOST_API VstHostResult VstHost_ScanFolder(const char16_t* folderPath,
                                              ScanCallbackFn callback,
                                              void* userData);

// --- Instance ---
// uid: 32-char hex class UID (optional; null/empty = first Audio Module Class).
VSTHOST_API VstHostResult VstHost_Load(const char16_t* filePath,
                                       const char16_t* uid,
                                       VstPluginId* outId);
VSTHOST_API VstHostResult VstHost_Unload(VstPluginId id);

// --- MIDI (lock-free queue → drained in Process) ---
VSTHOST_API VstHostResult VstHost_SendMidi1(VstPluginId id,
                                            uint8_t status,
                                            uint8_t data1,
                                            uint8_t data2);

// --- Audio (Phase 5 completes Unity return path; drains MIDI queue) ---
VSTHOST_API VstHostResult VstHost_Process(VstPluginId id,
                                          const float* inputL,
                                          const float* inputR,
                                          float* outputL,
                                          float* outputR,
                                          int32_t numFrames);
