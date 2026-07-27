#include <cstdio>
#include <string>
#include <vector>

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
    VstHost_Unload(id);

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

    printf("OK: Phase 3 smoke test passed\n");
    return 0;
}
