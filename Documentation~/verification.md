# Verification

## Prerequisites

- Unity 2022.3+ (or Unity 6)
- Native bridge present for your Editor OS:
  - **Windows:** `.\native~\windows-vst-host\Build.ps1 -Install`
  - **macOS:** `./native~/macos-vst-host/Build.sh --Install`
- Local `.vst3` plugins (do **not** redistribute third-party plugins)

## VST only (manual notes)

1. Create an empty Unity project **or** use Package Manager Git URL:
   `https://github.com/kshoji/Unity-VST3-Bridge.git`
2. Import sample **VST3 Host Sample**.
3. Open `VstHostSampleScene`, Enter Play Mode.
4. Confirm scan lists plugins, Load succeeds, **Note On** produces audio.
5. Optional chain check: switch to **Plugin Chain**, pick Instrument + Effect, **Build Chain**, **Note On**, toggle **Bypass effect**.
6. Confirm `Assets/MIDI` is **not** required.

Expected: instrument sound through `VstHostAudioFilter` / `AudioSource` (or `VstPluginChain` in chain mode).

For load/unload crash investigation, add scripting define **`VSTHOST_DEBUG`**
(Player Settings) to restore `[VstHost] CreateInstance` / `DestroyInstance` traces.
Leave it unset for normal use.

## MIDI + VST (same Unity project)

1. Open a Unity project that already contains Unity MIDI Plugin.
2. Add the VST package as a local package in `Packages/manifest.json`:

```json
"jp.kshoji.unity.vst3nativehost": "file:/absolute/path/to/Unity-VST3-Bridge"
```

3. Wait for compile. Menu **Window → VST3 Host → Sync MIDI Plugin Define** (or auto-sync) so `FEATURE_MIDI_PLUGIN` is set.
4. Import **VST3 Host Sample** (or add `VstHostSampleController` to a scene that already has `MidiManager`).
5. Ensure MIDI Plugin is initialized (`MidiManager.InitializeMidi*` as usual).
6. Enter Play Mode, load a VSTi, play notes from a MIDI device **or** the sample Note On buttons.

Expected: device → MIDI Plugin events → `VstHostMidiAdapter` → native queue → VSTi audio.

Do **not** place VST scripts under `Assets/MIDI`.

## Native smoke (optional, no Unity)

```powershell
# Windows
.\native~\windows-vst-host\Build.ps1
.\native~\windows-vst-host\build-x64\bin\Release\VstHostSmokeTest.exe
```

```bash
# macOS
./native~/macos-vst-host/Build.sh
./native~/macos-vst-host/build/bin/VstHostSmokeTest
# optional explicit folder:
VSTHOST_SMOKE_FOLDER="$HOME/Library/Audio/Plug-Ins/VST3" ./native~/macos-vst-host/build/bin/VstHostSmokeTest
```

Expects AGain / `again.vst3` (or another free sample) installed locally.

## IL2CPP Standalone Windows x64

Automated (recommended):

```powershell
.\native~\windows-vst-host\Run-Il2CppVerify.ps1
```

Manual: menu **Window → VST3 Host → Build IL2CPP Win64 (Verify)**. Asserts `VstHostNative.dll` is in the player output.

## Standalone macOS

1. Menu **Window → VST3 Host → Verify Plugin Platforms** (includes macOS bundle flags).
2. Menu **Window → VST3 Host → Build Standalone OSX (Verify)** (Mono backend).
3. Confirm the player contains `VstHostNative.bundle`.

When linking the repo via `file:` / Git, `native~/**/build*` outputs may appear — use **Window → VST3 Host → Sanitize Extra Native Plugins**.

## MIDI-only build must not contain VST

```powershell
.\native~\windows-vst-host\Verify-MidiIsolation.ps1 -MidiRepoRoot "<Unity-MIDI-Plugin>"
```

```bash
./native~/macos-vst-host/Verify-MidiIsolation.sh "<Unity-MIDI-Plugin>"
```

Fails if `VstHostNative` (`.dll` / `.bundle` / `.dylib`), VST3 SDK trees, or VST Runtime scripts appear under the MIDI repo `Assets` / `native` / `Packages` (markdown docs are allowed).
