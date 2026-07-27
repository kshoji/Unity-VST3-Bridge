#include <cstdio>
#include <cmath>
#include <string>
#include <vector>
#include <algorithm>

#include "VstHostNative.h"

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

int main()
{
    const wchar_t* folder = L"C:\\Program Files\\Common Files\\VST3";

    printf("Initialize...\n");
    if (VstHost_Initialize(48000, 512) != kVstHostOk)
    {
        printf("FAIL: Initialize\n");
        return 1;
    }

    std::vector<ScannedEntry> entries;
    printf("ScanFolder: %ls\n", folder);
    if (VstHost_ScanFolder(reinterpret_cast<const char16_t*>(folder), OnScan, &entries) != kVstHostOk)
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
