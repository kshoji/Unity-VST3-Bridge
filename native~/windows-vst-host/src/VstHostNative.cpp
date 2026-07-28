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
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/common/memorystream.h"
#include "pluginterfaces/vst/ivstprocesscontext.h"
#include "pluginterfaces/vst/ivstunits.h"
#include "public.sdk/source/vst/vstaudioprocessoralgo.h"

#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <thread>
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

struct PluginInstance;

struct HostComponentHandler : public Steinberg::Vst::IComponentHandler
{
    PluginInstance* owner = nullptr;

    Steinberg::tresult PLUGIN_API queryInterface(const Steinberg::TUID _iid, void** obj) override
    {
        QUERY_INTERFACE(_iid, obj, Steinberg::FUnknown::iid, Steinberg::Vst::IComponentHandler)
        QUERY_INTERFACE(_iid, obj, Steinberg::Vst::IComponentHandler::iid, Steinberg::Vst::IComponentHandler)
        *obj = nullptr;
        return Steinberg::kNoInterface;
    }

    Steinberg::uint32 PLUGIN_API addRef() override { return 1; }
    Steinberg::uint32 PLUGIN_API release() override { return 1; }

    Steinberg::tresult PLUGIN_API beginEdit(Steinberg::Vst::ParamID /*id*/) override
    {
        return Steinberg::kResultOk;
    }

    Steinberg::tresult PLUGIN_API performEdit(Steinberg::Vst::ParamID id,
                                              Steinberg::Vst::ParamValue valueNormalized) override;

    Steinberg::tresult PLUGIN_API endEdit(Steinberg::Vst::ParamID /*id*/) override
    {
        return Steinberg::kResultOk;
    }

    Steinberg::tresult PLUGIN_API restartComponent(Steinberg::int32 /*flags*/) override
    {
        return Steinberg::kResultOk;
    }
};

struct PluginInstance
{
    VST3::Hosting::Module::Ptr module;
    Steinberg::IPtr<Steinberg::Vst::IComponent> component;
    Steinberg::IPtr<Steinberg::Vst::IAudioProcessor> processor;
    Steinberg::IPtr<Steinberg::Vst::IEditController> controller;
    Steinberg::IPtr<Steinberg::Vst::IMidiMapping> midiMapping;
    Steinberg::IPtr<Steinberg::Vst::IUnitInfo> unitInfo;
    Steinberg::IPtr<Steinberg::Vst::ConnectionProxy> componentCP;
    Steinberg::IPtr<Steinberg::Vst::ConnectionProxy> controllerCP;
    HostComponentHandler componentHandler{};
    bool controllerOwnedSeparately = false;
    bool active = false;
    bool processing = false;
    Midi1Queue midiQueue;
    Steinberg::Vst::EventList inputEvents{128};
    Steinberg::Vst::ParameterChanges inputParams{64};
    Steinberg::Vst::ParameterChanges outputParams{64};
    Steinberg::Vst::ParameterChangeTransfer paramTransfer{256};
    Steinberg::Vst::HostProcessData processData;
    Steinberg::Vst::ProcessContext processContext{};
    // Per-channel scratch so input/output / aux buses never alias the same buffer.
    std::vector<std::vector<float>> scratchChannels;
    bool processPrepared = false;
    int32_t audioInputChannels = 0;
    int32_t audioOutputChannels = 0;
    // Cached program-change parameter (ParamID) when present; kNoParamId otherwise.
    Steinberg::Vst::ParamID programChangeParamId = Steinberg::Vst::kNoParamId;
    int32_t programChangeStepCount = 0;
    std::atomic<int> activeProcesses{0};
    std::string modulePath;
};

Steinberg::tresult PLUGIN_API HostComponentHandler::performEdit(
    Steinberg::Vst::ParamID id,
    Steinberg::Vst::ParamValue valueNormalized)
{
    if (owner)
        owner->paramTransfer.addChange(id, valueNormalized, 0);
    return Steinberg::kResultOk;
}

bool prepareAudioProcess(PluginInstance& inst); // defined after g_state

void tryBindMidiMapping(PluginInstance& inst)
{
    inst.midiMapping.reset();
    inst.unitInfo.reset();
    inst.programChangeParamId = Steinberg::Vst::kNoParamId;
    inst.programChangeStepCount = 0;
    if (!inst.controller)
        return;

    Steinberg::Vst::IMidiMapping* mapping = nullptr;
    if (inst.controller->queryInterface(Steinberg::Vst::IMidiMapping::iid,
                                         reinterpret_cast<void**>(&mapping))
        == Steinberg::kResultTrue)
    {
        inst.midiMapping = Steinberg::owned(mapping);
    }

    Steinberg::Vst::IUnitInfo* units = nullptr;
    if (inst.controller->queryInterface(Steinberg::Vst::IUnitInfo::iid,
                                         reinterpret_cast<void**>(&units))
        == Steinberg::kResultTrue)
    {
        inst.unitInfo = Steinberg::owned(units);
    }

    const int32_t count = inst.controller->getParameterCount();
    for (int32_t i = 0; i < count; ++i)
    {
        Steinberg::Vst::ParameterInfo info{};
        if (inst.controller->getParameterInfo(i, info) != Steinberg::kResultOk)
            continue;
        if ((info.flags & Steinberg::Vst::ParameterInfo::kIsProgramChange) != 0)
        {
            inst.programChangeParamId = info.id;
            inst.programChangeStepCount = info.stepCount;
            break;
        }
    }
}

void bindComponentHandler(PluginInstance& inst)
{
    inst.componentHandler.owner = &inst;
    if (inst.controller)
        inst.controller->setComponentHandler(&inst.componentHandler);
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
// Caller must clear inputEvents / inputParams before calling.
void drainMidiQueue(PluginInstance& inst)
{
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
std::shared_mutex g_audioLifecycleMutex; // shared: Process/SendMidi1, unique: Load/Unload/Terminate
HostState g_state;
std::atomic<VstPluginId> g_nextId{1};

struct ModuleCacheEntry
{
    VST3::Hosting::Module::Ptr module;
    int refCount = 0;
};

std::unordered_map<std::string, ModuleCacheEntry> g_moduleCache;
std::unordered_map<VstPluginId, std::unique_ptr<PluginInstance>> g_instances;

void destroyInstance(PluginInstance& inst);
void finishDestroyInstance(std::unique_ptr<PluginInstance> inst);

std::string normalizeModulePath(const std::string& path)
{
    std::error_code ec;
    const auto canonical = std::filesystem::weakly_canonical(std::filesystem::u8path(path), ec);
    return ec ? path : canonical.generic_u8string();
}

VST3::Hosting::Module::Ptr acquireModule(const std::string& path, std::string& errorStr)
{
    const auto key = normalizeModulePath(path);
    auto& entry = g_moduleCache[key];
    if (entry.module)
    {
        entry.refCount++;
        return entry.module;
    }

    auto mod = VST3::Hosting::Module::create(path, errorStr);
    if (!mod)
        return {};
    entry.module = mod;
    entry.refCount = 1;
    return mod;
}

void releaseModule(const VST3::Hosting::Module::Ptr& module)
{
    if (!module)
        return;

    for (auto& [key, entry] : g_moduleCache)
    {
        if (entry.module.get() == module.get())
        {
            if (entry.refCount > 0)
                entry.refCount--;
            // Keep Module::Ptr until Terminate so FreeLibrary cannot race the audio thread.
            return;
        }
    }
}

void finishDestroyInstance(std::unique_ptr<PluginInstance> inst)
{
    if (!inst)
        return;

    // Wait until audio/MIDI borrowers leave. Teardown always runs on this thread
    // (never from OnAudioFilterRead).
    for (int spins = 0; inst->activeProcesses.load(std::memory_order_acquire) > 0; ++spins)
    {
        if (spins < 40)
            std::this_thread::yield();
        else
            std::this_thread::sleep_for(std::chrono::microseconds(100));
    }

    destroyInstance(*inst);
}

/// Borrow an instance while incrementing activeProcesses under g_mutex.
struct InstanceBorrow
{
    PluginInstance* inst = nullptr;

    InstanceBorrow() = default;
    explicit InstanceBorrow(VstPluginId id)
    {
        std::lock_guard lock(g_mutex);
        if (!g_state.initialized)
            return;
        auto it = g_instances.find(id);
        if (it == g_instances.end())
            return;
        inst = it->second.get();
        inst->activeProcesses.fetch_add(1, std::memory_order_acq_rel);
    }

    InstanceBorrow(const InstanceBorrow&) = delete;
    InstanceBorrow& operator=(const InstanceBorrow&) = delete;

    InstanceBorrow(InstanceBorrow&& other) noexcept : inst(other.inst)
    {
        other.inst = nullptr;
    }

    InstanceBorrow& operator=(InstanceBorrow&& other) noexcept
    {
        if (this != &other)
        {
            release();
            inst = other.inst;
            other.inst = nullptr;
        }
        return *this;
    }

    ~InstanceBorrow() { release(); }

    explicit operator bool() const { return inst != nullptr; }
    PluginInstance* operator->() const { return inst; }
    PluginInstance& operator*() const { return *inst; }

    void release()
    {
        if (!inst)
            return;
        inst->activeProcesses.fetch_sub(1, std::memory_order_acq_rel);
        inst = nullptr;
    }
};

bool prepareAudioProcess(PluginInstance& inst)
{
    using namespace Steinberg::Vst;

    inst.processData.unprepare();
    inst.processPrepared = false;
    inst.scratchChannels.clear();

    // bufferSamples = 0 → allocate bus/channel pointer slots; we bind external buffers each Process.
    if (!inst.processData.prepare(*inst.component, 0, kSample32))
        return false;

    inst.processContext = {};
    inst.processContext.sampleRate = static_cast<double>(g_state.sampleRate);
    inst.processContext.tempo = 120.0;
    inst.processContext.state = ProcessContext::kPlaying | ProcessContext::kTempoValid
                                 | ProcessContext::kContTimeValid;
    inst.processContext.projectTimeSamples = 0;
    inst.processContext.continousTimeSamples = 0;

    inst.processData.processContext = &inst.processContext;
    inst.processData.inputEvents = &inst.inputEvents;
    inst.processData.inputParameterChanges = &inst.inputParams;
    inst.processData.outputParameterChanges = &inst.outputParams;
    inst.processData.processMode = kRealtime;
    inst.processData.symbolicSampleSize = kSample32;

    BusInfo info{};
    inst.audioInputChannels = 0;
    inst.audioOutputChannels = 0;
    if (inst.component->getBusCount(kAudio, kInput) > 0
        && inst.component->getBusInfo(kAudio, kInput, 0, info) == Steinberg::kResultOk)
        inst.audioInputChannels = info.channelCount;
    if (inst.component->getBusCount(kAudio, kOutput) > 0
        && inst.component->getBusInfo(kAudio, kOutput, 0, info) == Steinberg::kResultOk)
        inst.audioOutputChannels = info.channelCount;

    // One scratch plane per channel across all buses (main L/R use host buffers instead).
    int32_t scratchNeed = 0;
    for (int32_t b = 0; b < inst.processData.numInputs; ++b)
        scratchNeed += inst.processData.inputs[b].numChannels;
    for (int32_t b = 0; b < inst.processData.numOutputs; ++b)
        scratchNeed += inst.processData.outputs[b].numChannels;
    inst.scratchChannels.resize(static_cast<size_t>(std::max(scratchNeed, 1)));
    for (auto& ch : inst.scratchChannels)
        ch.assign(static_cast<size_t>(g_state.blockSize), 0.f);

    inst.processPrepared = true;
    return true;
}

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
    inst.outputParams.clearQueue();
    if (inst.processPrepared)
    {
        inst.processData.unprepare();
        inst.processPrepared = false;
    }
    inst.scratchChannels.clear();
    inst.paramTransfer.removeChanges();
    if (inst.controller)
        inst.controller->setComponentHandler(nullptr);
    inst.componentHandler.owner = nullptr;
    inst.unitInfo.reset();
    inst.midiMapping.reset();
    inst.processor.reset();
    inst.controller.reset();
    inst.component.reset();
    releaseModule(inst.module);
    inst.module = nullptr;
    inst.modulePath.clear();
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
        bindComponentHandler(inst);
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
    bindComponentHandler(inst);
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
    // Block audio/MIDI borrowers before tearing down instances or FreeLibrary.
    std::unique_lock audioLock(g_audioLifecycleMutex);

    std::vector<std::unique_ptr<PluginInstance>> pending;
    {
        std::lock_guard lock(g_mutex);
        if (!g_state.initialized)
            return kVstHostErrorNotInitialized;

        pending.reserve(g_instances.size());
        for (auto& [id, inst] : g_instances)
            pending.push_back(std::move(inst));
        g_instances.clear();
    }

    for (auto& inst : pending)
        finishDestroyInstance(std::move(inst));

    {
        std::lock_guard lock(g_mutex);
        g_moduleCache.clear(); // FreeLibrary only here, after Process has stopped.
        Steinberg::Vst::PluginContextFactory::instance().setPluginContext(nullptr);
        g_state.initialized = false;
    }
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

    // Exclusive: do not create/destroy while any Process is mid-flight.
    std::unique_lock audioLock(g_audioLifecycleMutex);
    std::lock_guard lock(g_mutex);
    if (!g_state.initialized)
        return kVstHostErrorNotInitialized;

    const std::string path = toUtf8(filePath);
    std::string errorStr;
    auto mod = acquireModule(path, errorStr);
    if (!mod)
        return kVstHostErrorLoadFailed;

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
    inst->modulePath = path;

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
        finishDestroyInstance(std::move(inst));
        return kVstHostErrorLoadFailed;
    }

    inst->processor = Steinberg::FUnknownPtr<Steinberg::Vst::IAudioProcessor>(inst->component);
    if (!inst->processor)
    {
        finishDestroyInstance(std::move(inst));
        return kVstHostErrorLoadFailed;
    }

    Steinberg::Vst::ProcessSetup setup{};
    setup.processMode = Steinberg::Vst::kRealtime;
    setup.symbolicSampleSize = Steinberg::Vst::kSample32;
    setup.maxSamplesPerBlock = g_state.blockSize;
    setup.sampleRate = static_cast<double>(g_state.sampleRate);

    if (inst->processor->setupProcessing(setup) != Steinberg::kResultOk)
    {
        finishDestroyInstance(std::move(inst));
        return kVstHostErrorLoadFailed;
    }

    // Arrange buses: main stereo (or mono), keep Aux buses mono (e.g. AGain SideChain).
    {
        using namespace Steinberg::Vst;
        const int32_t inBuses = inst->component->getBusCount(kAudio, kInput);
        const int32_t outBuses = inst->component->getBusCount(kAudio, kOutput);
        std::vector<SpeakerArrangement> inputs(static_cast<size_t>(std::max(inBuses, 0)));
        std::vector<SpeakerArrangement> outputs(static_cast<size_t>(std::max(outBuses, 0)));
        BusInfo bi{};
        for (int32_t i = 0; i < inBuses; ++i)
        {
            if (inst->component->getBusInfo(kAudio, kInput, i, bi) == Steinberg::kResultOk
                && bi.busType == kAux)
                inputs[static_cast<size_t>(i)] = SpeakerArr::kMono;
            else
                inputs[static_cast<size_t>(i)] = SpeakerArr::kStereo;
        }
        for (int32_t i = 0; i < outBuses; ++i)
        {
            if (inst->component->getBusInfo(kAudio, kOutput, i, bi) == Steinberg::kResultOk
                && bi.busType == kAux)
                outputs[static_cast<size_t>(i)] = SpeakerArr::kMono;
            else
                outputs[static_cast<size_t>(i)] = SpeakerArr::kStereo;
        }
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
    // data are fully wired. Treat setProcessing failure as soft.
    if (inst->component->setActive(true) != Steinberg::kResultOk)
    {
        finishDestroyInstance(std::move(inst));
        return kVstHostErrorLoadFailed;
    }
    inst->active = true;

    // Some plugins return false until buses/process data exist; retry after prepare.
    if (inst->processor->setProcessing(true) == Steinberg::kResultOk)
        inst->processing = true;

    if (!prepareAudioProcess(*inst))
    {
        finishDestroyInstance(std::move(inst));
        return kVstHostErrorLoadFailed;
    }

    if (!inst->processing
        && inst->processor->setProcessing(true) == Steinberg::kResultOk)
        inst->processing = true;

    // setProcessing may still fail on some plugins until the first process(); keep soft.
    const VstPluginId id = g_nextId.fetch_add(1);
    *outId = id;
    g_instances[id] = std::move(inst);
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_Unload(VstPluginId id)
{
    // Wait until no Process/SendMidi1 is using any instance, then destroy.
    std::unique_lock audioLock(g_audioLifecycleMutex);

    std::unique_ptr<PluginInstance> doomed;
    {
        std::lock_guard lock(g_mutex);
        if (!g_state.initialized)
            return kVstHostErrorNotInitialized;

        auto it = g_instances.find(id);
        if (it == g_instances.end())
            return kVstHostErrorInvalidId;

        doomed = std::move(it->second);
        g_instances.erase(it);
    }

    finishDestroyInstance(std::move(doomed));
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// MIDI (lock-free queue) / Audio
// ---------------------------------------------------------------------------

VSTHOST_API VstHostResult VstHost_SendMidi1(VstPluginId id,
                                            uint8_t status,
                                            uint8_t data1,
                                            uint8_t data2)
{
    std::shared_lock audioLock(g_audioLifecycleMutex);
    InstanceBorrow borrow(id);
    if (!borrow)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;

    if (!borrow->midiQueue.tryPush(status, data1, data2))
    {
        // Queue full: drop oldest then retry once (prefer live notes).
        Midi1Message discarded{};
        borrow->midiQueue.tryPop(discarded);
        if (!borrow->midiQueue.tryPush(status, data1, data2))
            return kVstHostErrorInvalidArgument;
    }
    return kVstHostOk;
}

namespace {

// SEH must not share a frame with C++ objects that need unwinding.
Steinberg::tresult safeProcessorProcess(Steinberg::Vst::IAudioProcessor* processor,
                                         Steinberg::Vst::ProcessData& data)
{
#if defined(_MSC_VER)
    __try
    {
        return processor->process(data);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return Steinberg::kResultFalse;
    }
#else
    return processor->process(data);
#endif
}

float* ensureScratchChannel(PluginInstance& inst, size_t index, int32_t numFrames)
{
    if (index >= inst.scratchChannels.size())
        inst.scratchChannels.resize(index + 1);
    auto& ch = inst.scratchChannels[index];
    if (static_cast<int32_t>(ch.size()) < numFrames)
        ch.assign(static_cast<size_t>(numFrames), 0.f);
    else
        std::memset(ch.data(), 0, sizeof(float) * static_cast<size_t>(numFrames));
    return ch.data();
}

} // namespace

VSTHOST_API VstHostResult VstHost_Process(VstPluginId id,
                                          const float* inputL,
                                          const float* inputR,
                                          float* outputL,
                                          float* outputR,
                                          int32_t numFrames)
{
    if (!outputL || !outputR || numFrames <= 0)
        return kVstHostErrorInvalidArgument;

    std::shared_lock audioLock(g_audioLifecycleMutex);
    InstanceBorrow borrow(id);
    if (!borrow)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;

    PluginInstance* inst = borrow.inst;

    if (!inst->processPrepared || !inst->processor)
    {
        std::memset(outputL, 0, sizeof(float) * static_cast<size_t>(numFrames));
        std::memset(outputR, 0, sizeof(float) * static_cast<size_t>(numFrames));
        return kVstHostErrorProcessFailed;
    }

    if (!inst->processing)
    {
        if (inst->processor->setProcessing(true) == Steinberg::kResultOk)
            inst->processing = true;
    }

    if (numFrames > g_state.blockSize)
    {
        std::memset(outputL, 0, sizeof(float) * static_cast<size_t>(numFrames));
        std::memset(outputR, 0, sizeof(float) * static_cast<size_t>(numFrames));
        return kVstHostErrorInvalidArgument;
    }

    using namespace Steinberg::Vst;

    inst->inputEvents.clear();
    inst->inputParams.clearQueue();
    drainMidiQueue(*inst);
    inst->paramTransfer.transferChangesTo(inst->inputParams);
    inst->outputParams.clearQueue();

    inst->processContext.sampleRate = static_cast<double>(g_state.sampleRate);
    inst->processContext.projectTimeSamples = inst->processContext.continousTimeSamples;
    inst->processData.numSamples = numFrames;

    size_t scratchIndex = 0;
    auto nextScratch = [&]() -> float* {
        return ensureScratchChannel(*inst, scratchIndex++, numFrames);
    };

    // Bind EVERY audio input bus. Unbound side-chain buses crash many plugins (e.g. AGain SideChain).
    // silenceFlags must equal getChannelMask(n) — plugins compare equality, not bit-subset.
    for (int32_t b = 0; b < inst->processData.numInputs; ++b)
    {
        auto& bus = inst->processData.inputs[b];
        if (!bus.channelBuffers32)
            continue;
        for (int32_t c = 0; c < bus.numChannels; ++c)
        {
            if (b == 0 && c == 0 && inputL)
                bus.channelBuffers32[c] = const_cast<float*>(inputL);
            else if (b == 0 && c == 1 && (inputR || inputL))
                bus.channelBuffers32[c] = const_cast<float*>(inputR ? inputR : inputL);
            else
                bus.channelBuffers32[c] = nextScratch();
        }
        const bool mainHasAudio = (b == 0 && inputL != nullptr);
        bus.silenceFlags = mainHasAudio ? 0 : getChannelMask(bus.numChannels);
    }

    // Bind EVERY audio output bus; only bus 0 carries host-visible L/R.
    if (inst->processData.numOutputs <= 0)
    {
        std::memset(outputL, 0, sizeof(float) * static_cast<size_t>(numFrames));
        std::memset(outputR, 0, sizeof(float) * static_cast<size_t>(numFrames));
        return kVstHostErrorProcessFailed;
    }

    for (int32_t b = 0; b < inst->processData.numOutputs; ++b)
    {
        auto& bus = inst->processData.outputs[b];
        if (!bus.channelBuffers32)
            continue;
        for (int32_t c = 0; c < bus.numChannels; ++c)
        {
            if (b == 0 && c == 0)
                bus.channelBuffers32[c] = outputL;
            else if (b == 0 && c == 1)
                bus.channelBuffers32[c] = outputR;
            else
                bus.channelBuffers32[c] = nextScratch();
        }
        bus.silenceFlags = 0;
    }

    const auto result = safeProcessorProcess(inst->processor, inst->processData);
    inst->processContext.continousTimeSamples += numFrames;

    // Mono out → duplicate to R.
    if (inst->audioOutputChannels == 1)
        std::memcpy(outputR, outputL, sizeof(float) * static_cast<size_t>(numFrames));

    // Unbind pointers so destroy cannot leave dangling refs.
    for (int32_t b = 0; b < inst->processData.numInputs; ++b)
    {
        auto& bus = inst->processData.inputs[b];
        if (!bus.channelBuffers32)
            continue;
        for (int32_t c = 0; c < bus.numChannels; ++c)
            bus.channelBuffers32[c] = nullptr;
    }
    for (int32_t b = 0; b < inst->processData.numOutputs; ++b)
    {
        auto& bus = inst->processData.outputs[b];
        if (!bus.channelBuffers32)
            continue;
        for (int32_t c = 0; c < bus.numChannels; ++c)
            bus.channelBuffers32[c] = nullptr;
    }

    if (result != Steinberg::kResultOk)
        return kVstHostErrorProcessFailed;
    return kVstHostOk;
}

// ---------------------------------------------------------------------------
// Parameters / Programs / State
// ---------------------------------------------------------------------------

namespace {

// Prefer InstanceBorrow for any use that continues after releasing g_mutex.

void copyString128(char16_t* dst, size_t dstChars, const Steinberg::Vst::String128 src)
{
    if (!dst || dstChars == 0)
        return;
    size_t i = 0;
    for (; i + 1 < dstChars && src[i] != 0; ++i)
        dst[i] = static_cast<char16_t>(src[i]);
    dst[i] = 0;
}

constexpr uint32_t kStateMagic = 0x31534856u; // 'VHS1' LE

bool writeU32(std::vector<uint8_t>& out, uint32_t v)
{
    out.push_back(static_cast<uint8_t>(v & 0xff));
    out.push_back(static_cast<uint8_t>((v >> 8) & 0xff));
    out.push_back(static_cast<uint8_t>((v >> 16) & 0xff));
    out.push_back(static_cast<uint8_t>((v >> 24) & 0xff));
    return true;
}

bool readU32(const uint8_t*& p, const uint8_t* end, uint32_t& v)
{
    if (end - p < 4)
        return false;
    v = static_cast<uint32_t>(p[0]) | (static_cast<uint32_t>(p[1]) << 8)
        | (static_cast<uint32_t>(p[2]) << 16) | (static_cast<uint32_t>(p[3]) << 24);
    p += 4;
    return true;
}

} // namespace

VSTHOST_API VstHostResult VstHost_GetParameterCount(VstPluginId id, int32_t* outCount)
{
    if (!outCount)
        return kVstHostErrorInvalidArgument;
    *outCount = 0;
    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->controller)
        return kVstHostErrorNotSupported;
    *outCount = inst->controller->getParameterCount();
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_GetParameterInfo(VstPluginId id,
                                                   int32_t index,
                                                   VstParamInfo* outInfo)
{
    if (!outInfo || index < 0)
        return kVstHostErrorInvalidArgument;
    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->controller)
        return kVstHostErrorNotSupported;

    Steinberg::Vst::ParameterInfo info{};
    if (inst->controller->getParameterInfo(index, info) != Steinberg::kResultOk)
        return kVstHostErrorInvalidArgument;

    std::memset(outInfo, 0, sizeof(*outInfo));
    outInfo->id = info.id;
    copyString128(outInfo->title, 128, info.title);
    copyString128(outInfo->shortTitle, 128, info.shortTitle);
    copyString128(outInfo->units, 128, info.units);
    outInfo->stepCount = info.stepCount;
    outInfo->defaultNormalized = info.defaultNormalizedValue;
    outInfo->flags = info.flags;
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_GetParameterNormalized(VstPluginId id,
                                                         uint32_t paramId,
                                                         double* outValue)
{
    if (!outValue)
        return kVstHostErrorInvalidArgument;
    *outValue = 0.0;
    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->controller)
        return kVstHostErrorNotSupported;
    *outValue = inst->controller->getParamNormalized(paramId);
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_SetParameterNormalized(VstPluginId id,
                                                         uint32_t paramId,
                                                         double value)
{
    if (value < 0.0)
        value = 0.0;
    if (value > 1.0)
        value = 1.0;

    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->controller)
        return kVstHostErrorNotSupported;

    // UI-thread controller update + audio-thread queue for process.
    if (inst->controller->setParamNormalized(paramId, value) != Steinberg::kResultOk)
        return kVstHostErrorInvalidArgument;
    inst->paramTransfer.addChange(paramId, value, 0);
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_GetProgramCount(VstPluginId id, int32_t* outCount)
{
    if (!outCount)
        return kVstHostErrorInvalidArgument;
    *outCount = 0;
    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;

    if (inst->unitInfo)
    {
        const int32_t lists = inst->unitInfo->getProgramListCount();
        if (lists > 0)
        {
            Steinberg::Vst::ProgramListInfo listInfo{};
            if (inst->unitInfo->getProgramListInfo(0, listInfo) == Steinberg::kResultOk)
            {
                *outCount = listInfo.programCount;
                return kVstHostOk;
            }
        }
    }

    if (inst->programChangeParamId != Steinberg::Vst::kNoParamId && inst->programChangeStepCount > 0)
    {
        *outCount = inst->programChangeStepCount + 1;
        return kVstHostOk;
    }
    return kVstHostErrorNotSupported;
}

VSTHOST_API VstHostResult VstHost_GetProgramName(VstPluginId id,
                                                 int32_t index,
                                                 char16_t* outName,
                                                 int32_t nameChars)
{
    if (!outName || nameChars <= 0 || index < 0)
        return kVstHostErrorInvalidArgument;
    outName[0] = 0;

    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;

    if (inst->unitInfo)
    {
        const int32_t lists = inst->unitInfo->getProgramListCount();
        if (lists > 0)
        {
            Steinberg::Vst::ProgramListInfo listInfo{};
            if (inst->unitInfo->getProgramListInfo(0, listInfo) == Steinberg::kResultOk
                && index < listInfo.programCount)
            {
                Steinberg::Vst::String128 name{};
                if (inst->unitInfo->getProgramName(listInfo.id, index, name) == Steinberg::kResultOk)
                {
                    copyString128(outName, static_cast<size_t>(nameChars), name);
                    return kVstHostOk;
                }
            }
        }
    }

    if (inst->programChangeParamId != Steinberg::Vst::kNoParamId
        && inst->programChangeStepCount > 0
        && index <= inst->programChangeStepCount)
    {
        // Fallback label when IUnitInfo has no names.
        char buf[64];
        std::snprintf(buf, sizeof(buf), "Program %d", index);
        size_t i = 0;
        for (; i + 1 < static_cast<size_t>(nameChars) && buf[i]; ++i)
            outName[i] = static_cast<char16_t>(buf[i]);
        outName[i] = 0;
        return kVstHostOk;
    }
    return kVstHostErrorNotSupported;
}

VSTHOST_API VstHostResult VstHost_SetProgram(VstPluginId id, int32_t index)
{
    if (index < 0)
        return kVstHostErrorInvalidArgument;
    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->controller || inst->programChangeParamId == Steinberg::Vst::kNoParamId)
        return kVstHostErrorNotSupported;

    double normalized = 0.0;
    if (inst->programChangeStepCount > 0)
        normalized = static_cast<double>(index) / static_cast<double>(inst->programChangeStepCount);
    return VstHost_SetParameterNormalized(id, inst->programChangeParamId, normalized);
}

VSTHOST_API VstHostResult VstHost_GetState(VstPluginId id,
                                           uint8_t* buffer,
                                           int32_t bufferSize,
                                           int32_t* outWritten)
{
    if (!outWritten)
        return kVstHostErrorInvalidArgument;
    *outWritten = 0;

    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->component)
        return kVstHostErrorNotSupported;

    Steinberg::MemoryStream compStream;
    Steinberg::MemoryStream ctrlStream;
    inst->component->getState(&compStream);
    if (inst->controller)
        inst->controller->getState(&ctrlStream);

    compStream.truncateToCursor();
    ctrlStream.truncateToCursor();
    const auto cLen = static_cast<uint32_t>(compStream.getSize());
    const auto tLen = static_cast<uint32_t>(ctrlStream.getSize());

    const int32_t needed = static_cast<int32_t>(4 + 4 + cLen + 4 + tLen);
    *outWritten = needed;
    if (!buffer || bufferSize < needed)
        return kVstHostErrorBufferTooSmall;

    std::vector<uint8_t> blob;
    blob.reserve(static_cast<size_t>(needed));
    writeU32(blob, kStateMagic);
    writeU32(blob, cLen);
    if (cLen > 0 && compStream.getData())
        blob.insert(blob.end(),
                    reinterpret_cast<uint8_t*>(compStream.getData()),
                    reinterpret_cast<uint8_t*>(compStream.getData()) + cLen);
    writeU32(blob, tLen);
    if (tLen > 0 && ctrlStream.getData())
        blob.insert(blob.end(),
                    reinterpret_cast<uint8_t*>(ctrlStream.getData()),
                    reinterpret_cast<uint8_t*>(ctrlStream.getData()) + tLen);

    std::memcpy(buffer, blob.data(), blob.size());
    *outWritten = static_cast<int32_t>(blob.size());
    return kVstHostOk;
}

VSTHOST_API VstHostResult VstHost_SetState(VstPluginId id,
                                           const uint8_t* buffer,
                                           int32_t size)
{
    if (!buffer || size < 12)
        return kVstHostErrorInvalidArgument;

    InstanceBorrow inst(id);
    if (!inst)
        return g_state.initialized ? kVstHostErrorInvalidId : kVstHostErrorNotInitialized;
    if (!inst->component)
        return kVstHostErrorNotSupported;

    const uint8_t* p = buffer;
    const uint8_t* end = buffer + size;
    uint32_t magic = 0, cLen = 0, tLen = 0;
    if (!readU32(p, end, magic) || magic != kStateMagic)
        return kVstHostErrorInvalidArgument;
    if (!readU32(p, end, cLen))
        return kVstHostErrorInvalidArgument;
    if (end - p < static_cast<ptrdiff_t>(cLen))
        return kVstHostErrorInvalidArgument;
    const uint8_t* cBytes = p;
    p += cLen;
    if (!readU32(p, end, tLen))
        return kVstHostErrorInvalidArgument;
    if (end - p < static_cast<ptrdiff_t>(tLen))
        return kVstHostErrorInvalidArgument;
    const uint8_t* tBytes = p;

    if (cLen > 0)
    {
        Steinberg::MemoryStream compStream(const_cast<uint8_t*>(cBytes), cLen);
        Steinberg::int64 dummy = 0;
        compStream.seek(0, Steinberg::IBStream::kIBSeekSet, &dummy);
        if (inst->component->setState(&compStream) != Steinberg::kResultOk)
            return kVstHostErrorLoadFailed;
        if (inst->controller)
        {
            compStream.seek(0, Steinberg::IBStream::kIBSeekSet, &dummy);
            inst->controller->setComponentState(&compStream);
        }
    }

    if (tLen > 0 && inst->controller)
    {
        Steinberg::MemoryStream ctrlStream(const_cast<uint8_t*>(tBytes), tLen);
        Steinberg::int64 dummy = 0;
        ctrlStream.seek(0, Steinberg::IBStream::kIBSeekSet, &dummy);
        if (inst->controller->setState(&ctrlStream) != Steinberg::kResultOk)
            return kVstHostErrorLoadFailed;
    }

    return kVstHostOk;
}
