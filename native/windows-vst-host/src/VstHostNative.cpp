#include "VstHostNative.h"

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/connectionproxy.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/utility/uid.h"
#include "public.sdk/source/vst/utility/stringconvert.h"

#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstcomponent.h"
#include "pluginterfaces/vst/ivstevents.h"
#include "pluginterfaces/vst/ivstmidicontrollers.h"
#include "pluginterfaces/base/ipluginbase.h"
#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"

#include <atomic>
#include <cstring>
#include <filesystem>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#endif

namespace fs = std::filesystem;

namespace {

struct HostState
{
    int32_t sampleRate = 44100;
    int32_t blockSize = 512;
    bool initialized = false;
    Steinberg::Vst::HostApplication hostApp;
};

// Lock-free SPSC ring for MIDI 1.0 short messages (producer: C# / MIDI adapter,
// consumer: audio Process). Capacity must be power of two.
struct Midi1Message
{
    uint8_t status = 0;
    uint8_t data1 = 0;
    uint8_t data2 = 0;
};

class Midi1Queue
{
public:
    static constexpr uint32_t kCapacity = 1024;
    static constexpr uint32_t kMask = kCapacity - 1;

    bool tryPush(uint8_t status, uint8_t data1, uint8_t data2)
    {
        const uint32_t w = writeIndex.load(std::memory_order_relaxed);
        const uint32_t next = (w + 1) & kMask;
        if (next == readIndex.load(std::memory_order_acquire))
            return false; // full — drop

        slots[w] = Midi1Message{status, data1, data2};
        writeIndex.store(next, std::memory_order_release);
        return true;
    }

    bool tryPop(Midi1Message& out)
    {
        const uint32_t r = readIndex.load(std::memory_order_relaxed);
        if (r == writeIndex.load(std::memory_order_acquire))
            return false; // empty

        out = slots[r];
        readIndex.store((r + 1) & kMask, std::memory_order_release);
        return true;
    }

    void clear()
    {
        readIndex.store(writeIndex.load(std::memory_order_relaxed), std::memory_order_relaxed);
    }

private:
    Midi1Message slots[kCapacity]{};
    std::atomic<uint32_t> writeIndex{0};
    std::atomic<uint32_t> readIndex{0};
};

struct PluginInstance
{
    VST3::Hosting::Module::Ptr module;
    Steinberg::IPtr<Steinberg::Vst::IComponent> component;
    Steinberg::IPtr<Steinberg::Vst::IAudioProcessor> processor;
    Steinberg::IPtr<Steinberg::Vst::IEditController> controller;
    Steinberg::IPtr<Steinberg::Vst::IMidiMapping> midiMapping;
    Steinberg::IPtr<Steinberg::Vst::ConnectionProxy> componentCP;
    Steinberg::IPtr<Steinberg::Vst::ConnectionProxy> controllerCP;
    bool controllerOwnedSeparately = false;
    bool active = false;
    bool processing = false;
    Midi1Queue midiQueue;
    Steinberg::Vst::EventList inputEvents{128};
    Steinberg::Vst::ParameterChanges inputParams{64};
};

void tryBindMidiMapping(PluginInstance& inst)
{
    inst.midiMapping.reset();
    if (!inst.controller)
        return;

    Steinberg::Vst::IMidiMapping* mapping = nullptr;
    if (inst.controller->queryInterface(Steinberg::Vst::IMidiMapping::iid,
                                         reinterpret_cast<void**>(&mapping))
        == Steinberg::kResultTrue)
    {
        inst.midiMapping = Steinberg::owned(mapping);
    }
}

void pushMappedParam(PluginInstance& inst,
                     Steinberg::Vst::CtrlNumber ctrl,
                     int16_t channel,
                     Steinberg::Vst::ParamValue normalized)
{
    if (!inst.midiMapping)
        return;

    Steinberg::Vst::ParamID paramId = Steinberg::Vst::kNoParamId;
    if (inst.midiMapping->getMidiControllerAssignment(0, channel, ctrl, paramId)
            != Steinberg::kResultOk
        || paramId == Steinberg::Vst::kNoParamId)
        return;

    Steinberg::int32 index = 0;
    if (auto* queue = inst.inputParams.addParameterData(paramId, index))
    {
        Steinberg::int32 pointIndex = 0;
        queue->addPoint(0, normalized, pointIndex);
    }
}

// Convert queued MIDI 1.0 short messages into VST3 EventList / ParameterChanges.
// Note / poly pressure → EventList. CC / channel pressure / pitch bend →
// IMidiMapping parameter changes when available.
// Called from the audio Process path (Phase 5); kept ready in Phase 4.
[[maybe_unused]] void drainMidiQueue(PluginInstance& inst)
{
    inst.inputEvents.clear();
    inst.inputParams.clearQueue();

    Midi1Message msg{};
    while (inst.midiQueue.tryPop(msg))
    {
        const uint8_t statusHi = static_cast<uint8_t>(msg.status & 0xF0);
        const int16_t channel = static_cast<int16_t>(msg.status & 0x0F);

        Steinberg::Vst::Event ev{};
        ev.busIndex = 0;
        ev.sampleOffset = 0;
        ev.ppqPosition = 0;
        ev.flags = Steinberg::Vst::Event::kIsLive;

        switch (statusHi)
        {
        case 0x90: // Note On (velocity 0 = Note Off)
            if (msg.data2 == 0)
            {
                ev.type = Steinberg::Vst::Event::kNoteOffEvent;
                ev.noteOff.channel = channel;
                ev.noteOff.pitch = msg.data1;
                ev.noteOff.velocity = 0.f;
                ev.noteOff.noteId = -1;
                ev.noteOff.tuning = 0.f;
            }
            else
            {
                ev.type = Steinberg::Vst::Event::kNoteOnEvent;
                ev.noteOn.channel = channel;
                ev.noteOn.pitch = msg.data1;
                ev.noteOn.tuning = 0.f;
                ev.noteOn.velocity = msg.data2 / 127.f;
                ev.noteOn.length = 0;
                ev.noteOn.noteId = -1;
            }
            inst.inputEvents.addEvent(ev);
            break;

        case 0x80: // Note Off
            ev.type = Steinberg::Vst::Event::kNoteOffEvent;
            ev.noteOff.channel = channel;
            ev.noteOff.pitch = msg.data1;
            ev.noteOff.velocity = msg.data2 / 127.f;
            ev.noteOff.noteId = -1;
            ev.noteOff.tuning = 0.f;
            inst.inputEvents.addEvent(ev);
            break;

        case 0xA0: // Polyphonic Aftertouch
            ev.type = Steinberg::Vst::Event::kPolyPressureEvent;
            ev.polyPressure.channel = channel;
            ev.polyPressure.pitch = msg.data1;
            ev.polyPressure.pressure = msg.data2 / 127.f;
            ev.polyPressure.noteId = -1;
            inst.inputEvents.addEvent(ev);
            break;

        case 0xB0: // Control Change
            pushMappedParam(inst, msg.data1, channel, msg.data2 / 127.0);
            break;

        case 0xD0: // Channel Aftertouch
            pushMappedParam(inst, Steinberg::Vst::kAfterTouch, channel, msg.data1 / 127.0);
            break;

        case 0xE0: // Pitch Bend
        {
            const int bend = msg.data1 | (msg.data2 << 7);
            pushMappedParam(inst, Steinberg::Vst::kPitchBend, channel, bend / 16383.0);
            break;
        }

        case 0xC0: // Program Change — deferred (needs program-list param)
        default:
            break;
        }
    }
}

std::mutex g_mutex;
HostState g_state;
std::atomic<VstPluginId> g_nextId{1};
std::unordered_map<VstPluginId, std::unique_ptr<PluginInstance>> g_instances;

std::string toUtf8(const char16_t* s)
{
    if (!s || !*s)
        return {};
#ifdef _WIN32
    const wchar_t* ws = reinterpret_cast<const wchar_t*>(s);
    const int len = WideCharToMultiByte(CP_UTF8, 0, ws, -1, nullptr, 0, nullptr, nullptr);
    if (len <= 1)
        return {};
    std::string out(static_cast<size_t>(len - 1), '\0');
    WideCharToMultiByte(CP_UTF8, 0, ws, -1, out.data(), len, nullptr, nullptr);
    return out;
#else
    return Steinberg::Vst::StringConvert::convert(std::u16string(s));
#endif
}

std::u16string toUtf16(const std::string& utf8)
{
#ifdef _WIN32
    if (utf8.empty())
        return {};
    const int len = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
    if (len <= 1)
        return {};
    std::wstring wide(static_cast<size_t>(len - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, wide.data(), len);
    return std::u16string(wide.begin(), wide.end());
#else
    return Steinberg::Vst::StringConvert::convert(utf8);
#endif
}

void collectVst3Paths(const fs::path& root, std::vector<std::string>& out)
{
    std::error_code ec;
    if (!fs::exists(root, ec))
        return;

    // Direct .vst3 file (flat) or directory bundle
    if (root.extension() == ".vst3")
    {
        out.push_back(root.generic_u8string());
        return;
    }

    if (!fs::is_directory(root, ec))
        return;

    for (auto it = fs::recursive_directory_iterator(
             root, fs::directory_options::skip_permission_denied, ec);
         it != fs::recursive_directory_iterator(); ++it)
    {
        const auto& p = it->path();
        if (p.extension() != ".vst3")
            continue;

        // Skip nested Contents/x86_64-win/*.vst3 binaries inside a bundle.
        // Bundle path ends with ".vst3" as a directory; inner file also ends with ".vst3".
        bool insideBundle = false;
        for (auto parent = p.parent_path(); !parent.empty() && parent != parent.root_path();
             parent = parent.parent_path())
        {
            if (parent.extension() == ".vst3" && fs::is_directory(parent, ec))
            {
                insideBundle = true;
                break;
            }
        }
        if (insideBundle)
            continue;

        out.push_back(p.generic_u8string());

        // Do not recurse into .vst3 bundle directories
        if (it->is_directory(ec))
            it.disable_recursion_pending();
    }
}

void collectDefaultVst3Paths(std::vector<std::string>& out)
{
    // Prefer SDK known-folder enumeration (handles bundle + flat + shortcuts).
    auto known = VST3::Hosting::Module::getModulePaths();
    out.insert(out.end(), known.begin(), known.end());
}

void destroyInstance(PluginInstance& inst)
{
    if (inst.processing && inst.processor)
    {
        inst.processor->setProcessing(false);
        inst.processing = false;
    }
    if (inst.active && inst.component)
    {
        inst.component->setActive(false);
        inst.active = false;
    }

    // Disconnect controller ↔ component (PlugProvider order)
    if (inst.componentCP)
        inst.componentCP->disconnect();
    if (inst.controllerCP)
        inst.controllerCP->disconnect();
    inst.componentCP.reset();
    inst.controllerCP.reset();

    const bool controllerIsComponent =
        inst.component &&
        Steinberg::FUnknownPtr<Steinberg::Vst::IEditController>(inst.component).getInterface() != nullptr;

    if (inst.component)
        inst.component->terminate();

    if (inst.controller && !controllerIsComponent && inst.controllerOwnedSeparately)
        inst.controller->terminate();

    inst.midiQueue.clear();
    inst.inputEvents.clear();
    inst.inputParams.clearQueue();
    inst.midiMapping.reset();
    inst.processor.reset();
    inst.controller.reset();
    inst.component.reset();
    inst.module.reset();
}

bool setupControllerAndConnect(PluginInstance& inst, const VST3::Hosting::PluginFactory& factory)
{
    using namespace Steinberg;
    using namespace Steinberg::Vst;

    FUnknown* hostCtx = &g_state.hostApp;

    // Single-component plugins expose IEditController on the component.
    Steinberg::Vst::IEditController* ctrlFromComponent = nullptr;
    if (inst.component->queryInterface(IEditController::iid, (void**)&ctrlFromComponent) == kResultTrue)
    {
        inst.controller = Steinberg::owned(ctrlFromComponent);
        inst.controllerOwnedSeparately = false;
        tryBindMidiMapping(inst);
        return true;
    }

    TUID controllerCID{};
    if (inst.component->getControllerClassId(controllerCID) != kResultTrue)
        return true; // no controller — still usable for process-only hosts

    inst.controller = factory.createInstance<IEditController>(VST3::UID(controllerCID));
    if (!inst.controller)
        return true; // continue without controller

    if (inst.controller->initialize(hostCtx) != kResultOk)
    {
        inst.controller.reset();
        return true; // continue without controller
    }
    inst.controllerOwnedSeparately = true;

    auto compICP = FUnknownPtr<IConnectionPoint>(inst.component);
    auto ctrlICP = FUnknownPtr<IConnectionPoint>(inst.controller);
    if (compICP && ctrlICP)
    {
        inst.componentCP = owned(new ConnectionProxy(compICP));
        inst.controllerCP = owned(new ConnectionProxy(ctrlICP));

        // Connection failure is non-fatal for load/unload.
        inst.componentCP->connect(ctrlICP);
        inst.controllerCP->connect(compICP);
    }

    tryBindMidiMapping(inst);
    return true;
}

void emitClassInfos(const VST3::Hosting::Module::Ptr& mod,
                    ScanCallbackFn callback,
                    void* userData)
{
    auto factory = mod->getFactory();
    for (auto& ci : factory.classInfos())
    {
        if (ci.category() != kVstAudioEffectClass)
            continue;

        auto uid = toUtf16(ci.ID().toString());
        auto name = toUtf16(ci.name());
        auto vendor = toUtf16(ci.vendor());
        auto category = toUtf16(ci.subCategoriesString());
        auto filePath = toUtf16(mod->getPath());

        VstPluginInfo info{};
        info.uid = uid.c_str();
        info.name = name.c_str();
        info.vendor = vendor.c_str();
        info.category = category.c_str();
        info.filePath = filePath.c_str();
        callback(&info, userData);
    }
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
    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(&g_state.hostApp);
    g_state.initialized = true;
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Terminate()
{
    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    for (auto& [id, inst] : g_instances)
        destroyInstance(*inst);
    g_instances.clear();

    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(nullptr);
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
    if (!callback)
        return kVstHostErrorInvalidArgument;

    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    std::vector<std::string> paths;
    const std::string folder = toUtf8(folderPath);
    if (folder.empty())
        collectDefaultVst3Paths(paths);
    else
        collectVst3Paths(fs::u8path(folder), paths);

    for (const auto& path : paths)
    {
        std::string errorStr;
        auto mod = VST3::Hosting::Module::create(path, errorStr);
        if (!mod)
            continue;
        emitClassInfos(mod, callback, userData);
    }

    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// Instance
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_Load(const char16_t* filePath,
                                       const char16_t* uid,
                                       VstPluginId* outId)
{
    if (!filePath || !outId)
        return kVstHostErrorInvalidArgument;

    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    const std::string path = toUtf8(filePath);
    std::string errorStr;
    auto mod = VST3::Hosting::Module::create(path, errorStr);
    if (!mod)
    {
        // Keep lightweight; detailed logging belongs in host tooling.
        return kVstHostErrorLoadFailed;
    }

    auto factory = mod->getFactory();
    const auto classInfos = factory.classInfos();
    if (classInfos.empty())
        return kVstHostErrorLoadFailed;

    const std::string wantedUid = toUtf8(uid);
    const VST3::Hosting::ClassInfo* targetClass = nullptr;

    if (!wantedUid.empty())
    {
        auto parsed = VST3::UID::fromString(wantedUid);
        if (!parsed)
            return kVstHostErrorInvalidArgument;

        for (auto& ci : classInfos)
        {
            if (ci.category() == kVstAudioEffectClass && ci.ID() == *parsed)
            {
                targetClass = &ci;
                break;
            }
        }
        // Fallback: COM-format UID string comparison
        if (!targetClass)
        {
            for (auto& ci : classInfos)
            {
                if (ci.category() == kVstAudioEffectClass &&
                    (ci.ID().toString() == wantedUid || ci.ID().toString(true) == wantedUid))
                {
                    targetClass = &ci;
                    break;
                }
            }
        }
    }
    else
    {
        for (auto& ci : classInfos)
        {
            if (ci.category() == kVstAudioEffectClass)
            {
                targetClass = &ci;
                break;
            }
        }
    }

    if (!targetClass)
        return kVstHostErrorLoadFailed;

    auto inst = std::make_unique<PluginInstance>();
    inst->module = mod;

    Steinberg::FUnknown* hostCtx = &g_state.hostApp;
    inst->component = factory.createInstance<Steinberg::Vst::IComponent>(targetClass->ID());
    if (!inst->component)
        return kVstHostErrorLoadFailed;

    const auto initRes = inst->component->initialize(hostCtx);
    if (initRes != Steinberg::kResultOk)
    {
        inst->component->terminate();
        return kVstHostErrorLoadFailed;
    }

    if (!setupControllerAndConnect(*inst, factory))
    {
        destroyInstance(*inst);
        return kVstHostErrorLoadFailed;
    }

    inst->processor = Steinberg::FUnknownPtr<Steinberg::Vst::IAudioProcessor>(inst->component);
    if (!inst->processor)
    {
        destroyInstance(*inst);
        return kVstHostErrorLoadFailed;
    }

    Steinberg::Vst::ProcessSetup setup{};
    setup.processMode = Steinberg::Vst::kRealtime;
    setup.symbolicSampleSize = Steinberg::Vst::kSample32;
    setup.maxSamplesPerBlock = g_state.blockSize;
    setup.sampleRate = static_cast<double>(g_state.sampleRate);

    if (inst->processor->setupProcessing(setup) != Steinberg::kResultOk)
    {
        destroyInstance(*inst);
        return kVstHostErrorLoadFailed;
    }

    // Arrange default stereo audio buses when available.
    {
        using namespace Steinberg::Vst;
        SpeakerArrangement inArr = SpeakerArr::kStereo;
        SpeakerArrangement outArr = SpeakerArr::kStereo;
        const int32_t inBuses = inst->component->getBusCount(kAudio, kInput);
        const int32_t outBuses = inst->component->getBusCount(kAudio, kOutput);
        std::vector<SpeakerArrangement> inputs(static_cast<size_t>(std::max(inBuses, 0)), inArr);
        std::vector<SpeakerArrangement> outputs(static_cast<size_t>(std::max(outBuses, 0)), outArr);
        if (inBuses > 0 || outBuses > 0)
        {
            inst->processor->setBusArrangements(
                inputs.empty() ? nullptr : inputs.data(), inBuses,
                outputs.empty() ? nullptr : outputs.data(), outBuses);
        }
    }

    // Bus activation: enable first stereo in/out when present.
    const auto activateBuses = [](Steinberg::Vst::IComponent* comp) {
        using namespace Steinberg::Vst;
        for (int32_t type = 0; type < 2; ++type)
        {
            const auto busType = type == 0 ? kAudio : kEvent;
            const int32_t inCount = comp->getBusCount(busType, kInput);
            const int32_t outCount = comp->getBusCount(busType, kOutput);
            for (int32_t i = 0; i < inCount; ++i)
                comp->activateBus(busType, kInput, i, true);
            for (int32_t i = 0; i < outCount; ++i)
                comp->activateBus(busType, kOutput, i, true);
        }
    };
    activateBuses(inst->component);

    // Prefer setActive; setProcessing can fail on some plugins before buses/process
    // data are fully wired (Phase 5). Treat setProcessing failure as soft.
    if (inst->component->setActive(true) != Steinberg::kResultOk)
    {
        destroyInstance(*inst);
        return kVstHostErrorLoadFailed;
    }
    inst->active = true;

    if (inst->processor->setProcessing(true) == Steinberg::kResultOk)
        inst->processing = true;

    const VstPluginId id = g_nextId.fetch_add(1);
    *outId = id;
    g_instances[id] = std::move(inst);
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Unload(VstPluginId id)
{
    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    auto it = g_instances.find(id);
    if (it == g_instances.end())
        return kVstHostErrorInvalidId;

    destroyInstance(*it->second);
    g_instances.erase(it);
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// MIDI (lock-free queue) / Audio (Phase 5 completes ring-buffer return path)
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_SendMidi1(VstPluginId id,
                                            uint8_t status,
                                            uint8_t data1,
                                            uint8_t data2)
{
    // Lookup without holding g_mutex during enqueue so audio Process can proceed.
    PluginInstance* inst = nullptr;
    {
        std::lock_guard lock(g_mutex);
        if (!g_state.initialized)
            return kVstHostErrorNotInitialized;
        auto it = g_instances.find(id);
        if (it == g_instances.end())
            return kVstHostErrorInvalidId;
        inst = it->second.get();
    }

    if (!inst->midiQueue.tryPush(status, data1, data2))
    {
        // Queue full: drop oldest then retry once (prefer live notes).
        Midi1Message discarded{};
        inst->midiQueue.tryPop(discarded);
        if (!inst->midiQueue.tryPush(status, data1, data2))
            return kVstHostErrorInvalidArgument;
    }
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Process(VstPluginId id,
                                          const float* inputL,
                                          const float* inputR,
                                          float* outputL,
                                          float* outputR,
                                          int32_t numFrames)
{
    if (!outputL || !outputR || numFrames <= 0)
        return kVstHostErrorInvalidArgument;

    std::memset(outputL, 0, sizeof(float) * static_cast<size_t>(numFrames));
    std::memset(outputR, 0, sizeof(float) * static_cast<size_t>(numFrames));

    {
        std::lock_guard lock(g_mutex);
        if (!g_state.initialized)
            return kVstHostErrorNotInitialized;
        if (g_instances.find(id) == g_instances.end())
            return kVstHostErrorInvalidId;
    }

    // Phase 5: drainMidiQueue(*inst) then processor->process(...).
    // Do not drain here — that would discard notes before audio is wired.
    (void)inputL;
    (void)inputR;
    return kVstHostOk;
}
