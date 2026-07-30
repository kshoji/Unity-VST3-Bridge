# Unity Plugin Host for VST3

A Unity package for hosting VST3 instruments and effects through a native bridge, with optional MIDI plugin integration.

**UPM name:** `jp.kshoji.unity.vst3nativehost`  
**Repository:** https://github.com/kshoji/Unity-VST3-Bridge

This package is distributed **separately** from
[Unity MIDI Plugin](https://assetstore.unity.com/packages/slug/198917)
(Unity Asset Store). MIDI releases do not include `VstHostNative.dll`, the VST3
SDK, or VST host C#.

## Install from Git URL

Unity Package Manager → Add package from Git URL:

```
https://github.com/kshoji/Unity-VST3-Bridge.git
```

## Package layout

- `Runtime/` — runtime C# code
- `Editor/` — editor-only code
- `Plugins/Windows/x86_64/` — native bridge DLL (Editor + Standalone Win64)
- `Plugins/Windows/ARM64/` — native bridge DLL (Standalone Windows ARM64)
- `Plugins/macOS/` — native bridge `.bundle` (Editor OSX + Standalone OSXUniversal)
- `Samples~/` — importable samples
- `Documentation~/` — package documentation (usage, verification, limitations, etc.)
- `Tests/` — optional package tests
- `native~/` — C++ source for `VstHostNative` (**Git repo only**)
  - Trailing `~` = Unity AssetDatabase ignores this folder on Git URL / `file:` installs
  - Also listed in `.npmignore` for registry publishes
  - `native~/windows-vst-host/` — Windows DLL
  - `native~/macos-vst-host/` — macOS bundle (shares SDK submodule under windows tree)

## Documentation

| Doc | Content |
|-----|---------|
| [Documentation~/usage.md](Documentation~/usage.md) | Scan paths, quick start, MIDI optional steps |
| [Documentation~/audio-path.md](Documentation~/audio-path.md) | Audio return path |
| [Documentation~/midi-integration.md](Documentation~/midi-integration.md) | MIDI adapter details |
| [Documentation~/parameters.md](Documentation~/parameters.md) | Parameters / presets / state |
| [Documentation~/verification.md](Documentation~/verification.md) | Manual / IL2CPP / isolation checks |
| [Documentation~/limitations.md](Documentation~/limitations.md) | Known limits |
| [Documentation~/package-excludes.md](Documentation~/package-excludes.md) | Why `native~/` (Git/file: vs `.npmignore`) |
| [NOTICE.md](NOTICE.md) | Trademark, VST3 SDK license, distribution boundary |

## Status

Native host (Windows + macOS), MIDI, audio return, parameters / state / IMGUI panel,
CC→parameter mapping, SMF link, preset assets / browser,
sample scene, IL2CPP Win64 / Standalone OSX verify helpers, and documentation /
trademark / SDK notices are included.

Import **VST3 Host Sample** from Package Manager, or follow [Documentation~/verification.md](Documentation~/verification.md).

## Audio

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

Empty/`null` scan uses OS-standard VST3 locations via the SDK
(Windows Common Files / macOS Library folders, plus app-local `VST3`).
Pass a folder path to scan a custom tree. Details: [Documentation~/usage.md](Documentation~/usage.md).

## Optional MIDI Plugin integration

This package does **not** depend on Unity MIDI Plugin. MIDI input from devices
is available when you add
[Unity MIDI Plugin](https://assetstore.unity.com/packages/slug/198917)
from the Unity Asset Store.

<a href="https://assetstore.unity.com/packages/slug/198917"><img src="https://assetstorev1-prd-cdn.unity3d.com/key-image/11cbc98e-7a67-4718-9d89-0910920f5883.webp" alt="Unity MIDI Plugin on the Unity Asset Store" width="320" /></a>

When both packages are in the same project, Editor auto-syncs the
`FEATURE_MIDI_PLUGIN` define (`Window/VST3 Host/Sync MIDI Plugin Define`).
That enables assembly `jp.kshoji.unity.vst3nativehost.Midi` and component
`VstHostMidiAdapter`.

Without MIDI Plugin, use manual APIs on `VstHostManager` (`NoteOn` / `NoteOff`, etc.).

Audio returns via native `process` called from `OnAudioFilterRead`.

## Native build / VST3 SDK

Rebuilders need the SDK submodule (under `native~/windows-vst-host/vst3sdk`) plus
platform toolchains:

```powershell
# Windows
git submodule update --init --recursive -- native~/windows-vst-host/vst3sdk
cd native~/windows-vst-host
.\Build.ps1 -Install
```

```bash
# macOS
git submodule update --init --recursive -- native~/windows-vst-host/vst3sdk
cd native~/macos-vst-host
./Build.sh --Install
```

See [native~/windows-vst-host/README.md](native~/windows-vst-host/README.md),
[native~/macos-vst-host/README.md](native~/macos-vst-host/README.md), and
[NOTICE.md](NOTICE.md). Prebuilt binaries under `Plugins/` are enough for normal UPM use.

## Debug tracing (load / lifecycle)

Informational load logs (`CreateInstance` / `DestroyInstance` / Initialize /
Terminate / Scan) are off by default. To enable them, add scripting define
**`VSTHOST_DEBUG`** (Project Settings → Player → Other Settings → Scripting
Define Symbols). Errors and warnings remain always on.

## Known limitations (summary)

VST3 only · Windows + macOS · no plugin-native GUI · MIDI 2.0 down-convert ·
commercial plugin compatibility not guaranteed · no third-party `.vst3` in the
package. Full list: [Documentation~/limitations.md](Documentation~/limitations.md).

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.
