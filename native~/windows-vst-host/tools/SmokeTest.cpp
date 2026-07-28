#include <cstdio>
#include <cmath>
#include <cstdlib>
#include <string>
#include <vector>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <thread>

#include "VstHostNative.h"

#if defined(_WIN32)
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#endif

struct ScannedEntry
{
    std::u16string uid;
    std::u16string name;
    std::u16string vendor;
    std::u16string category;
    std::u16string filePath;
};

static void OnScan(const VstPluginInfo* info, void* userData)
{
    auto* list = static_cast<std::vector<ScannedEntry>*>(userData);
    ScannedEntry e;
    e.uid = info->uid ? info->uid : u"";
    e.name = info->name ? info->name : u"";
    e.vendor = info->vendor ? info->vendor : u"";
    e.category = info->category ? info->category : u"";
    e.filePath = info->filePath ? info->filePath : u"";
    list->push_back(std::move(e));
}

static std::string toNarrow(const std::u16string& s)
{
    std::string out;
    out.reserve(s.size());
    for (char16_t c : s)
        out.push_back(static_cast<char>(c < 128 ? c : '?'));
    return out;
}

static std::u16string utf8ToU16(const char* utf8)
{
    if (!utf8 || !*utf8)
        return {};
#if defined(_WIN32)
    const int len = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
    if (len <= 1)
        return {};
    std::wstring wide(static_cast<size_t>(len - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, wide.data(), len);
    return std::u16string(wide.begin(), wide.end());
#else
    // ASCII / UTF-8 BMP subset used by smoke paths; expand code units simply.
    std::u16string out;
    out.reserve(std::char_traits<char>::length(utf8));
    for (const unsigned char* p = reinterpret_cast<const unsigned char*>(utf8); *p; ++p)
    {
        if (*p < 0x80)
            out.push_back(static_cast<char16_t>(*p));
        else if ((*p & 0xE0) == 0xC0 && p[1])
        {
            out.push_back(static_cast<char16_t>(((p[0] & 0x1F) << 6) | (p[1] & 0x3F)));
            ++p;
        }
        else if ((*p & 0xF0) == 0xE0 && p[1] && p[2])
        {
            out.push_back(static_cast<char16_t>(((p[0] & 0x0F) << 12) | ((p[1] & 0x3F) << 6) | (p[2] & 0x3F)));
            p += 2;
        }
        else
            out.push_back(u'?');
    }
    return out;
#endif
}

static std::u16string resolveSmokeFolder()
{
    if (const char* env = std::getenv("VSTHOST_SMOKE_FOLDER"))
        return utf8ToU16(env);

#if defined(_WIN32)
    return u"C:\\Program Files\\Common Files\\VST3";
#else
    // Empty → native ScanFolder uses SDK getModulePaths() (Library VST3 folders).
    return {};
#endif
}

int main()
{
    const std::u16string folderStorage = resolveSmokeFolder();
    const char16_t* folder = folderStorage.empty() ? nullptr : folderStorage.c_str();

    printf("Initialize...\n");
    if (VstHost_Initialize(48000, 512) != kVstHostOk)
    {
        printf("FAIL: Initialize\n");
        return 1;
    }

    std::vector<ScannedEntry> entries;
    if (folder)
        printf("ScanFolder: %s\n", toNarrow(folderStorage).c_str());
    else
        printf("ScanFolder: <SDK default paths>\n");
    if (VstHost_ScanFolder(folder, OnScan, &entries) != kVstHostOk)
    {
        printf("FAIL: ScanFolder\n");
        VstHost_Terminate();
        return 1;
    }

    printf("Found %zu class(es)\n", entries.size());
    const ScannedEntry* again = nullptr;
    for (const auto& item : entries)
    {
        auto name = toNarrow(item.name);
        printf("  - %s [%s]\n", name.c_str(), toNarrow(item.category).c_str());
        if (name == "AGain VST3")
            again = &item;
    }
    if (!again)
    {
        for (const auto& item : entries)
        {
            if (toNarrow(item.name).find("AGain") != std::string::npos)
            {
                again = &item;
                break;
            }
        }
    }

    if (!again)
    {
        printf("FAIL: AGain not found (is again.vst3 installed?)\n");
        VstHost_Terminate();
        return 1;
    }

    VstPluginId id = -1;
    printf("Load: %s\n", toNarrow(again->filePath).c_str());
    if (VstHost_Load(again->filePath.c_str(), again->uid.c_str(), &id) != kVstHostOk || id < 1)
    {
        printf("FAIL: Load\n");
        VstHost_Terminate();
        return 1;
    }
    printf("Loaded id=%d\n", id);

    if (VstHost_Unload(id) != kVstHostOk)
    {
        printf("FAIL: Unload\n");
        VstHost_Terminate();
        return 1;
    }
    printf("Unloaded id=%d\n", id);

    if (VstHost_Load(again->filePath.c_str(), nullptr, &id) != kVstHostOk)
    {
        printf("FAIL: Load without uid\n");
        VstHost_Terminate();
        return 1;
    }
    printf("Loaded without uid id=%d\n", id);

    // Phase 4: enqueue MIDI 1.0 into the lock-free queue
    if (VstHost_SendMidi1(id, 0x90, 60, 100) != kVstHostOk)
    {
        printf("FAIL: SendMidi1 NoteOn\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    if (VstHost_SendMidi1(id, 0x80, 60, 0) != kVstHostOk)
    {
        printf("FAIL: SendMidi1 NoteOff\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    printf("SendMidi1 NoteOn/NoteOff ok\n");

    // Phase 5: Process AGain (effect) with a sine input
    constexpr int kFrames = 512;
    std::vector<float> inL(kFrames), inR(kFrames), outL(kFrames), outR(kFrames);
    for (int i = 0; i < kFrames; ++i)
    {
        const float s = 0.25f * std::sin(2.f * 3.14159265f * 440.f * i / 48000.f);
        inL[i] = s;
        inR[i] = s;
    }
    if (VstHost_Process(id, inL.data(), inR.data(), outL.data(), outR.data(), kFrames) != kVstHostOk)
    {
        printf("FAIL: Process (AGain)\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    double energy = 0.0;
    for (int i = 0; i < kFrames; ++i)
        energy += static_cast<double>(outL[i]) * outL[i] + static_cast<double>(outR[i]) * outR[i];
    printf("Process AGain energy=%.6f\n", energy);
    if (energy < 1e-8)
    {
        printf("FAIL: AGain process produced silence\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }

    // AGain SideChain: second input bus must be bound (regression for Unity crash).
    const ScannedEntry* againSide = nullptr;
    for (const auto& item : entries)
    {
        if (toNarrow(item.name).find("SideChain") != std::string::npos)
        {
            againSide = &item;
            break;
        }
    }
    if (againSide)
    {
        VstPluginId sideId = -1;
        if (VstHost_Load(againSide->filePath.c_str(), againSide->uid.c_str(), &sideId) != kVstHostOk
            || sideId < 1)
        {
            printf("FAIL: Load AGain SideChain\n");
            VstHost_Unload(id);
            VstHost_Terminate();
            return 1;
        }
        if (VstHost_Process(sideId, inL.data(), inR.data(), outL.data(), outR.data(), kFrames)
            != kVstHostOk)
        {
            printf("FAIL: Process AGain SideChain (effect)\n");
            VstHost_Unload(sideId);
            VstHost_Unload(id);
            VstHost_Terminate();
            return 1;
        }
        if (VstHost_Process(sideId, nullptr, nullptr, outL.data(), outR.data(), kFrames)
            != kVstHostOk)
        {
            printf("FAIL: Process AGain SideChain (null input)\n");
            VstHost_Unload(sideId);
            VstHost_Unload(id);
            VstHost_Terminate();
            return 1;
        }
        VstHost_Unload(sideId);
        printf("Process AGain SideChain ok\n");
    }
    else
    {
        printf("WARN: AGain SideChain not found; skipped\n");
    }

    // Phase 6: parameters / state on AGain
    int32_t paramCount = 0;
    if (VstHost_GetParameterCount(id, &paramCount) != kVstHostOk || paramCount <= 0)
    {
        printf("FAIL: GetParameterCount\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    printf("AGain parameter count=%d\n", paramCount);

    VstParamInfo pinfo{};
    if (VstHost_GetParameterInfo(id, 0, &pinfo) != kVstHostOk)
    {
        printf("FAIL: GetParameterInfo\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    printf("Param[0] id=%u title=%s\n", pinfo.id, toNarrow(std::u16string(pinfo.title)).c_str());

    double before = 0.0, after = 0.0;
    VstHost_GetParameterNormalized(id, pinfo.id, &before);
    if (VstHost_SetParameterNormalized(id, pinfo.id, 0.25) != kVstHostOk)
    {
        printf("FAIL: SetParameterNormalized\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    VstHost_GetParameterNormalized(id, pinfo.id, &after);
    printf("Param value %.3f -> %.3f\n", before, after);

    // Process again after gain change — energy should differ from previous block baseline
    if (VstHost_Process(id, inL.data(), inR.data(), outL.data(), outR.data(), kFrames) != kVstHostOk)
    {
        printf("FAIL: Process after param\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    double energy2 = 0.0;
    for (int i = 0; i < kFrames; ++i)
        energy2 += static_cast<double>(outL[i]) * outL[i] + static_cast<double>(outR[i]) * outR[i];
    printf("Process AGain after param energy=%.6f\n", energy2);

    int32_t stateSize = 0;
    VstHost_GetState(id, nullptr, 0, &stateSize);
    std::vector<uint8_t> state(static_cast<size_t>(std::max(stateSize, 0)));
    int32_t written = 0;
    if (stateSize > 0
        && VstHost_GetState(id, state.data(), static_cast<int32_t>(state.size()), &written) != kVstHostOk)
    {
        printf("FAIL: GetState\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    printf("GetState bytes=%d\n", written);
    if (written > 0 && VstHost_SetState(id, state.data(), written) != kVstHostOk)
    {
        printf("FAIL: SetState\n");
        VstHost_Unload(id);
        VstHost_Terminate();
        return 1;
    }
    printf("SetState ok\n");

    VstHost_Unload(id);

    // Phase 5: Instrument path — NoteOn + silent input Process
    const ScannedEntry* instrument = nullptr;
    for (const auto& item : entries)
    {
        auto cat = toNarrow(item.category);
        auto name = toNarrow(item.name);
        if (cat.find("Instrument") != std::string::npos
            && name.find("mda DX10") != std::string::npos)
        {
            instrument = &item;
            break;
        }
    }
    if (!instrument)
    {
        for (const auto& item : entries)
        {
            if (toNarrow(item.category).find("Instrument") != std::string::npos)
            {
                instrument = &item;
                break;
            }
        }
    }

    if (instrument)
    {
        printf("Load instrument: %s\n", toNarrow(instrument->name).c_str());
        if (VstHost_Load(instrument->filePath.c_str(), instrument->uid.c_str(), &id) != kVstHostOk)
        {
            printf("FAIL: Load instrument\n");
            VstHost_Terminate();
            return 1;
        }
        VstHost_SendMidi1(id, 0x90, 60, 100);
        std::fill(outL.begin(), outL.end(), 0.f);
        std::fill(outR.begin(), outR.end(), 0.f);
        // Warm-up a few blocks (some synths attack slowly)
        bool processOk = true;
        double instEnergy = 0.0;
        for (int block = 0; block < 8; ++block)
        {
            if (VstHost_Process(id, nullptr, nullptr, outL.data(), outR.data(), kFrames) != kVstHostOk)
            {
                processOk = false;
                break;
            }
            for (int i = 0; i < kFrames; ++i)
                instEnergy += static_cast<double>(outL[i]) * outL[i]
                              + static_cast<double>(outR[i]) * outR[i];
        }
        VstHost_SendMidi1(id, 0x80, 60, 0);
        printf("Instrument process ok=%d energy=%.6f\n", processOk ? 1 : 0, instEnergy);
        if (!processOk)
        {
            printf("FAIL: Instrument Process\n");
            VstHost_Unload(id);
            VstHost_Terminate();
            return 1;
        }
        // Soft check: warn if silent (some instruments need preset/GUI), don't fail hard
        if (instEnergy < 1e-10)
            printf("WARN: Instrument produced near-silence (may need preset)\n");
        VstHost_Unload(id);
    }
    else
    {
        printf("WARN: no Instrument found for Process smoke\n");
    }

    // Stress: rapid Load/Unload while a worker keeps calling Process (race regression).
    // Capture paths before any further scans mutate `entries`.
    const std::u16string stressPath = again->filePath;
    const std::u16string stressUid = again->uid;
    {
        printf("Load/Unload+Process stress...\n");
        std::atomic<VstPluginId> liveId{0};
        std::atomic<bool> stop{false};
        std::atomic<int> processCalls{0};
        std::thread worker([&]() {
            std::vector<float> wl(kFrames), wr(kFrames);
            while (!stop.load(std::memory_order_acquire))
            {
                const VstPluginId cur = liveId.load(std::memory_order_acquire);
                if (cur > 0)
                {
                    VstHost_Process(cur, nullptr, nullptr, wl.data(), wr.data(), kFrames);
                    processCalls.fetch_add(1, std::memory_order_relaxed);
                }
                else
                {
                    std::this_thread::yield();
                }
            }
        });

        for (int i = 0; i < 40; ++i)
        {
            VstPluginId stressId = 0;
            if (VstHost_Load(stressPath.c_str(), stressUid.c_str(), &stressId) != kVstHostOk)
            {
                printf("FAIL: stress Load iteration %d\n", i);
                stop.store(true);
                worker.join();
                VstHost_Terminate();
                return 1;
            }
            liveId.store(stressId, std::memory_order_release);
            std::this_thread::sleep_for(std::chrono::milliseconds(2));
            liveId.store(0, std::memory_order_release);
            if (VstHost_Unload(stressId) != kVstHostOk)
            {
                printf("FAIL: stress Unload iteration %d\n", i);
                stop.store(true);
                worker.join();
                VstHost_Terminate();
                return 1;
            }
        }

        stop.store(true, std::memory_order_release);
        worker.join();
        printf("Stress ok (processCalls=%d)\n", processCalls.load());
    }

    // Default-path scan (empty folder argument)
    entries.clear();
    if (VstHost_ScanFolder(nullptr, OnScan, &entries) != kVstHostOk)
    {
        printf("FAIL: default ScanFolder\n");
        VstHost_Terminate();
        return 1;
    }
    printf("Default scan found %zu class(es)\n", entries.size());

    if (VstHost_Terminate() != kVstHostOk)
    {
        printf("FAIL: Terminate\n");
        return 1;
    }

    printf("OK: Phase 3/4/5/6 smoke test passed\n");
    return 0;
}
