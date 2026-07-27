#pragma once

#include <cstdint>

#ifdef _WIN32
#define VSTHOST_API extern "C" __declspec(dllexport)
#else
#define VSTHOST_API extern "C" __attribute__((visibility("default")))
#endif

// Error codes
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

// Opaque plugin instance id
using VstPluginId = int32_t;

// Scan result entry (passed back via callback)
struct VstPluginInfo
{
    const char16_t* uid;        // class UID as hex string
    const char16_t* name;
    const char16_t* vendor;
    const char16_t* category;   // e.g. "Instrument" or "Fx"
    const char16_t* filePath;
};

using ScanCallbackFn = void (*)(const VstPluginInfo* info, void* userData);

// --- Lifecycle ---
VSTHOST_API VstHostResult VstHost_Initialize(int32_t sampleRate, int32_t blockSize);
VSTHOST_API VstHostResult VstHost_Terminate();

// --- Scan ---
VSTHOST_API VstHostResult VstHost_ScanFolder(const char16_t* folderPath,
                                              ScanCallbackFn callback,
                                              void* userData);

// --- Instance ---
VSTHOST_API VstHostResult VstHost_Load(const char16_t* filePath,
                                       const char16_t* uid,
                                       VstPluginId* outId);
VSTHOST_API VstHostResult VstHost_Unload(VstPluginId id);

// --- MIDI ---
VSTHOST_API VstHostResult VstHost_SendMidi1(VstPluginId id,
                                            uint8_t status,
                                            uint8_t data1,
                                            uint8_t data2);

// --- Audio ---
VSTHOST_API VstHostResult VstHost_Process(VstPluginId id,
                                          const float* inputL,
                                          const float* inputR,
                                          float* outputL,
                                          float* outputR,
                                          int32_t numFrames);
