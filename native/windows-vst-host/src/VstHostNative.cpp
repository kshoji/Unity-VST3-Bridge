#include "VstHostNative.h"

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"

#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstcomponent.h"

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <filesystem>

namespace {

struct HostState
{
    int32_t sampleRate = 44100;
    int32_t blockSize = 512;
    bool initialized = false;
};

struct PluginInstance
{
    VST3::Hosting::Module::Ptr module;
    Steinberg::IPtr<Steinberg::Vst::IComponent> component;
    Steinberg::IPtr<Steinberg::Vst::IAudioProcessor> processor;
    Steinberg::IPtr<Steinberg::Vst::IEditController> controller;
    bool active = false;
    bool processing = false;
};

std::mutex g_mutex;
HostState g_state;
std::atomic<VstPluginId> g_nextId{1};
std::unordered_map<VstPluginId, std::unique_ptr<PluginInstance>> g_instances;

std::string toUtf8(const char16_t* s)
{
    if (!s) return {};
    std::wstring ws(reinterpret_cast<const wchar_t*>(s));
    // simple narrow conversion for ASCII-ish paths; full UTF-8 in later phase
    return std::string(ws.begin(), ws.end());
}

} // anonymous namespace

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_Initialize(int32_t sampleRate, int32_t blockSize)
{
    std::lock_guard lock(g_mutex);
    if (g_state.initialized)
        return kVstHostErrorAlreadyInitialized;

    g_state.sampleRate = sampleRate > 0 ? sampleRate : 44100;
    g_state.blockSize = blockSize > 0 ? blockSize : 512;
    g_state.initialized = true;
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Terminate()
{
    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    // Tear down all instances
    for (auto& [id, inst] : g_instances)
    {
        if (inst->processing && inst->processor)
        {
            inst->processor->setProcessing(false);
            inst->processing = false;
        }
        if (inst->active && inst->component)
        {
            inst->component->setActive(false);
            inst->active = false;
        }
        if (inst->component)
            inst->component->terminate();
        if (inst->controller)
            inst->controller->terminate();
    }
    g_instances.clear();
    g_state.initialized = false;
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// Scan
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_ScanFolder(const char16_t* folderPath,
                                              ScanCallbackFn callback,
                                              void* userData)
{
    if (!folderPath || !callback)
        return kVstHostErrorInvalidArgument;

    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    std::string folder = toUtf8(folderPath);
    auto paths = VST3::Hosting::Module::getModulePaths();

    // Filter to requested folder prefix
    for (const auto& path : paths)
    {
        if (path.find(folder) == std::string::npos)
            continue;

        std::string errorStr;
        auto mod = VST3::Hosting::Module::create(path, errorStr);
        if (!mod)
            continue;

        auto factory = mod->getFactory();
        for (auto& ci : factory.classInfos())
        {
            if (ci.category() != kVstAudioEffectClass)
                continue;

            // Convert strings to char16_t (wide) for callback
            auto toWide = [](const std::string& s) -> std::u16string {
                return std::u16string(s.begin(), s.end());
            };

            auto uid = toWide(ci.ID().toString());
            auto name = toWide(ci.name());
            auto vendor = toWide(ci.vendor());

            auto subCat = ci.subCategoriesString();
            auto category = toWide(subCat);
            auto filePath = toWide(path);

            VstPluginInfo info{};
            info.uid = uid.c_str();
            info.name = name.c_str();
            info.vendor = vendor.c_str();
            info.category = category.c_str();
            info.filePath = filePath.c_str();

            callback(&info, userData);
        }
    }

    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// Instance
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_Load(const char16_t* filePath,
                                       const char16_t* /* uid — reserved */,
                                       VstPluginId* outId)
{
    if (!filePath || !outId)
        return kVstHostErrorInvalidArgument;

    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    std::string path = toUtf8(filePath);
    std::string errorStr;
    auto mod = VST3::Hosting::Module::create(path, errorStr);
    if (!mod)
        return kVstHostErrorLoadFailed;

    auto factory = mod->getFactory();
    auto classInfos = factory.classInfos();
    if (classInfos.empty())
        return kVstHostErrorLoadFailed;

    // Use the first audio class
    const VST3::Hosting::ClassInfo* targetClass = nullptr;
    for (auto& ci : classInfos)
    {
        if (ci.category() == kVstAudioEffectClass)
        {
            targetClass = &ci;
            break;
        }
    }
    if (!targetClass)
        return kVstHostErrorLoadFailed;

    auto inst = std::make_unique<PluginInstance>();
    inst->module = mod;

    // Create component
    inst->component = factory.createInstance<Steinberg::Vst::IComponent>(targetClass->ID());
    if (!inst->component)
        return kVstHostErrorLoadFailed;

    // Initialize component
    // TODO: provide proper host context in later phase
    if (inst->component->initialize(nullptr) != Steinberg::kResultOk)
        return kVstHostErrorLoadFailed;

    // Get processor
    inst->processor = Steinberg::FUnknownPtr<Steinberg::Vst::IAudioProcessor>(inst->component);
    if (!inst->processor)
    {
        inst->component->terminate();
        return kVstHostErrorLoadFailed;
    }

    // Setup processing
    Steinberg::Vst::ProcessSetup setup{};
    setup.processMode = Steinberg::Vst::kRealtime;
    setup.symbolicSampleSize = Steinberg::Vst::kSample32;
    setup.maxSamplesPerBlock = g_state.blockSize;
    setup.sampleRate = g_state.sampleRate;

    if (inst->processor->setupProcessing(setup) != Steinberg::kResultOk)
    {
        inst->component->terminate();
        return kVstHostErrorLoadFailed;
    }

    // Activate
    if (inst->component->setActive(true) != Steinberg::kResultOk)
    {
        inst->component->terminate();
        return kVstHostErrorLoadFailed;
    }
    inst->active = true;

    // Start processing
    if (inst->processor->setProcessing(true) != Steinberg::kResultOk)
    {
        inst->component->setActive(false);
        inst->component->terminate();
        return kVstHostErrorLoadFailed;
    }
    inst->processing = true;

    VstPluginId id = g_nextId.fetch_add(1);
    *outId = id;
    g_instances[id] = std::move(inst);
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Unload(VstPluginId id)
{
    std::lock_guard lock(g_mutex);
    auto it = g_instances.find(id);
    if (it == g_instances.end())
        return kVstHostErrorInvalidId;

    auto& inst = it->second;
    if (inst->processing && inst->processor)
    {
        inst->processor->setProcessing(false);
        inst->processing = false;
    }
    if (inst->active && inst->component)
    {
        inst->component->setActive(false);
        inst->active = false;
    }
    if (inst->component)
        inst->component->terminate();
    if (inst->controller)
        inst->controller->terminate();

    g_instances.erase(it);
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// MIDI
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_SendMidi1(VstPluginId id,
                                            uint8_t status,
                                            uint8_t data1,
                                            uint8_t data2)
{
    // Stub: queue MIDI event for next Process call
    // Full implementation in Phase 4/5
    (void)id; (void)status; (void)data1; (void)data2;
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// Audio
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_Process(VstPluginId id,
                                          const float* inputL,
                                          const float* inputR,
                                          float* outputL,
                                          float* outputR,
                                          int32_t numFrames)
{
    if (!outputL || !outputR || numFrames <= 0)
        return kVstHostErrorInvalidArgument;

    // Zero output as safety default
    std::memset(outputL, 0, sizeof(float) * numFrames);
    std::memset(outputR, 0, sizeof(float) * numFrames);

    // Stub: actual VST process() call in Phase 5
    (void)id; (void)inputL; (void)inputR;
    return kVstHostOk;
}
