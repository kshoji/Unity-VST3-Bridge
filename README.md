# Unity Plugin Host for VST3

A Unity package for hosting VST3 instruments and effects through a native bridge, with optional MIDI plugin integration.

**UPM name:** `jp.kshoji.unity.vst3nativehost`  
**Repository:** https://github.com/kshoji/Unity-VST3-Bridge

This package is distributed **separately** from [Unity MIDI Plugin](https://github.com/kshoji/Unity-MIDI-Plugin). MIDI releases do not include `VstHostNative.dll`, the VST3 SDK, or VST host C#.

## Install from Git URL

Unity Package Manager → Add package from Git URL:

```
https://github.com/kshoji/Unity-VST3-Bridge.git
```

No `?path=` suffix is needed — `package.json` is at the repository root.

## Package layout

- `Runtime/` — runtime C# code
- `Editor/` — editor-only code
- `Plugins/Windows/x86_64/` — native bridge DLL (Editor + Standalone Win64)
- `Plugins/Windows/ARM64/` — native bridge DLL (Standalone Windows ARM64)
- `Plugins/macOS/` — native bridge `.bundle` (Phase M; scaffold + `.meta` policy today)
- `Samples~/` — importable samples
- `Documentation~/` — package documentation (usage, verification, plan, macOS portability)
- `Tests/` — optional package tests
- `native/` — C++ source for `VstHostNative` (repo only; excluded from registry via `.npmignore`)

## Documentation

| Doc | Content |
|-----|---------|
| [`Documentation~/usage.md`](Documentation~/usage.md) | Scan paths, quick start, MIDI optional steps |
| [`Documentation~/audio-path.md`](Documentation~/audio-path.md) | Audio return Path B |
| [`Documentation~/midi-integration.md`](Documentation~/midi-integration.md) | MIDI adapter details |
| [`Documentation~/parameters.md`](Documentation~/parameters.md) | Parameters / presets / state |
| [`Documentation~/verification.md`](Documentation~/verification.md) | Manual / IL2CPP / isolation checks |
| [`Documentation~/limitations.md`](Documentation~/limitations.md) | Known limits |
| [`Documentation~/macos-portability.md`](Documentation~/macos-portability.md) | Windows-only surface + Phase M prep |
| [`NOTICE.md`](NOTICE.md) | Trademark, VST3 SDK license, distribution boundary |
| [`Documentation~/vst3-native-host-plan.md`](Documentation~/vst3-native-host-plan.md) | Design plan (canonical) |

## Status

Phases 0–8 are complete on branch `feature/vst3-native-host-20260727`:
native host, MIDI, audio return, parameters / state / IMGUI panel, sample scene,
IL2CPP Win64 verify, documentation / trademark / SDK notices.

Import **VST3 Host Sample** from Package Manager, or follow `Documentation~/verification.md`.

## Audio (Path B)

1. `VstHostManager.Instance.InitializeFromAudioSettings()`
2. `CreateInstance(...)` → assign id to `VstHostAudioFilter.PluginId`
3. Mode: `Instrument` (VSTi) or `Effect`
4. Ensure an `AudioSource` is playing (component auto-creates a silent loop clip)

```csharp
var host = VstHostManager.Instance;
host.InitializeFromAudioSettings();
int id = host.CreateInstance(path, uid);
var filter = GetComponent<VstHostAudioFilter>();
filter.PluginId = id;
filter.Mode = VstHostAudioFilter.ProcessMode.Instrument;
host.NoteOn(id, 0, 60, 100);
```

## Scan paths

Empty/`null` scan uses Windows VST3 standard locations via the SDK
(`Common Files\VST3`, per-user common, app-local `VST3`). Pass a folder path
to scan a custom tree. Details: `Documentation~/usage.md`.

## Optional MIDI Plugin integration

This package does **not** depend on Unity MIDI Plugin. When both are in the same
project, Editor auto-syncs the `FEATURE_MIDI_PLUGIN` define
(`Window/VST3 Host/Sync MIDI Plugin Define`). That enables assembly
`jp.kshoji.unity.vst3nativehost.Midi` and component `VstHostMidiAdapter`.

Without MIDI Plugin, use manual APIs on `VstHostManager` (`NoteOn` / `NoteOff`, etc.).

Audio returns via Path B: native `process` called from `OnAudioFilterRead`.

## Native build / VST3 SDK

Rebuilders need the SDK submodule and Visual Studio + CMake:

```powershell
git submodule update --init --recursive -- native/windows-vst-host/vst3sdk
cd native/windows-vst-host
.\Build.ps1 -Install
```

See `native/windows-vst-host/README.md` and `NOTICE.md` for license and
submodule details. Prebuilt DLLs under `Plugins/` are enough for normal UPM use.

## Debug tracing (load / lifecycle)

Informational load logs (`CreateInstance` / `DestroyInstance` / Initialize /
Terminate / Scan) are off by default. To enable them, add scripting define
**`VSTHOST_DEBUG`** (Project Settings → Player → Other Settings → Scripting
Define Symbols). Errors and warnings remain always on.

## Known limitations (summary)

VST3 only · Windows first · no plugin-native GUI · MIDI 2.0 down-convert ·
commercial plugin compatibility not guaranteed · no third-party `.vst3` in the
package. Full list: `Documentation~/limitations.md`.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.
