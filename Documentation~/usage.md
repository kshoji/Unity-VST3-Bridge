# Usage guide

Unity package: **Unity Plugin Host for VST3** (`jp.kshoji.unity.vst3nativehost`).

## Install

Unity Package Manager → Add package from Git URL:

```
https://github.com/kshoji/Unity-VST3-Bridge.git
```

No `?path=` suffix — `package.json` is at the repository root.

Local development:

```json
"jp.kshoji.unity.vst3nativehost": "file:C:/path/to/Unity-VST3-Bridge"
```

Requires native bridge binaries:

- Windows: `Plugins/Windows/x86_64/VstHostNative.dll` (+ ARM64 for Windows ARM64)
  — rebuild with `native~/windows-vst-host/Build.ps1 -Install`
- macOS: `Plugins/macOS/VstHostNative.bundle` (Universal)
  — rebuild with `native~/macos-vst-host/Build.sh --Install`

## Scan paths

`VstHostManager.Scan()` / `VstHost_ScanFolder` with an empty or null folder
uses the VST3 SDK’s `Module::getModulePaths()` for the **current OS**:

| OS | Typical locations |
|----|-------------------|
| Windows | `C:\Program Files\Common Files\VST3`; `%LOCALAPPDATA%\Programs\Common\VST3`; `<host>\VST3` |
| macOS | `~/Library/Audio/Plug-Ins/VST3`; `/Library/Audio/Plug-Ins/VST3`; app-local `VST3` |

`GetDefaultScanFolders()` mirrors those paths in C# for UI / tooling. You can
also pass an explicit folder path to scan only that tree (bundle and flat
`.vst3` layouts are supported).

Do **not** redistribute third-party `.vst3` files with your project or this
package. Install plugins on the machine that runs the host.

## Quick start (no MIDI)

```csharp
using jp.kshoji.unity.vst3nativehost;

var host = VstHostManager.Instance;
host.InitializeFromAudioSettings();

var plugins = host.Scan(); // default folders
int id = host.CreateInstance(plugins[0].Path, plugins[0].Uid);

var filter = gameObject.AddComponent<VstHostAudioFilter>();
filter.PluginId = id;
filter.Mode = VstHostAudioFilter.ProcessMode.Instrument;

host.NoteOn(id, channel: 0, note: 60, velocity: 100);
```

Audio path: native `process` from `OnAudioFilterRead` (Path B). Details:
`audio-path.md`.

## Optional MIDI Plugin integration

MIDI is **optional**. This package compiles and runs without Unity MIDI Plugin.

1. Add Unity MIDI Plugin to the same Unity project (`Assets/MIDI` or equivalent).
2. Ensure scripting define **`FEATURE_MIDI_PLUGIN`**  
   (menu **Window → VST3 Host → Sync MIDI Plugin Define**, or Editor auto-sync).
3. Add component **`VstHostMidiAdapter`**, set **`TargetPluginId`** after `CreateInstance`.
4. Initialize MIDI as usual (`MidiManager.InitializeMidi` / `InitializeMidi2`).

MIDI 2.0 channel voice is down-converted to MIDI 1.0. SysEx / per-note are
skipped. Full steps: `midi-integration.md` and Verification B in `verification.md`.

Do **not** register VST routing in MIDI core `midi2Plugins`; keep routing in
this package.

## Parameters and state

See `parameters.md`. Simple IMGUI panel: `VstHostParameterPanel`.
Plugin-native GUI (`IPlugView`) is not hosted.

## Samples and verification

- Package Manager → Samples → **VST3 Host Sample**
- Checklist: `verification.md` (VST-only, MIDI+VST, IL2CPP, MIDI isolation)

## Related documents

| Doc | Topic |
|-----|--------|
| `audio-path.md` | Path B / realtime rules |
| `midi-integration.md` | Optional MIDI adapter |
| `parameters.md` | Parameters / presets / state |
| `limitations.md` | Known limits |
| `macos-portability.md` | Phase M / Windows-only inventory |
| `NOTICE.md` (repo root) | Trademark / SDK / distribution |
