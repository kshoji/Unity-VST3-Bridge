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
    kVstHostErrorNotSupported = -8,
    kVstHostErrorBufferTooSmall = -9,
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

struct VstParamInfo
{
    uint32_t id;
    char16_t title[128];
    char16_t shortTitle[128];
    char16_t units[128];
    int32_t stepCount;
    double defaultNormalized;
    int32_t flags; // VST3 ParameterInfo::ParameterFlags
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

// --- Parameters (Phase 6) ---
VSTHOST_API VstHostResult VstHost_GetParameterCount(VstPluginId id, int32_t* outCount);
VSTHOST_API VstHostResult VstHost_GetParameterInfo(VstPluginId id,
                                                   int32_t index,
                                                   VstParamInfo* outInfo);
VSTHOST_API VstHostResult VstHost_GetParameterNormalized(VstPluginId id,
                                                         uint32_t paramId,
                                                         double* outValue);
// Sets controller value and queues a realtime parameter change for Process.
VSTHOST_API VstHostResult VstHost_SetParameterNormalized(VstPluginId id,
                                                         uint32_t paramId,
                                                         double value);

// --- Presets / programs (best-effort via IUnitInfo / program-change param) ---
VSTHOST_API VstHostResult VstHost_GetProgramCount(VstPluginId id, int32_t* outCount);
VSTHOST_API VstHostResult VstHost_GetProgramName(VstPluginId id,
                                                 int32_t index,
                                                 char16_t* outName,
                                                 int32_t nameChars);
VSTHOST_API VstHostResult VstHost_SetProgram(VstPluginId id, int32_t index);

// --- State (component + controller blob) ---
// Format: magic 'VHS1' | uint32 compLen | compBytes | uint32 ctrlLen | ctrlBytes
VSTHOST_API VstHostResult VstHost_GetState(VstPluginId id,
                                           uint8_t* buffer,
                                           int32_t bufferSize,
                                           int32_t* outWritten);
VSTHOST_API VstHostResult VstHost_SetState(VstPluginId id,
                                           const uint8_t* buffer,
                                           int32_t size);
