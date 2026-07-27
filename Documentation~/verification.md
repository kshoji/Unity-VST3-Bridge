# Verification (Phase 7)

## Prerequisites

- Windows 10/11 x64
- Unity 2022.3+ or Unity 6 (MIDI sample project uses Unity 6)
- `VstHostNative.dll` present for x64 + ARM64 (`.\native\windows-vst-host\Build.ps1 -Install`)
- Local `.vst3` plugins (do **not** redistribute third-party plugins)

## Verification A — VST only (manual notes)

1. Create an empty Unity project **or** use Package Manager Git URL:
   `https://github.com/kshoji/Unity-VST3-Bridge.git`
2. Import sample **VST3 Host Sample**.
3. Open `VstHostSampleScene`, Enter Play Mode.
4. Confirm scan lists plugins, Load succeeds, **Note On** produces audio.
5. Confirm `Assets/MIDI` is **not** required.

Expected: instrument sound through `VstHostAudioFilter` / `AudioSource`.

For load/unload crash investigation, add scripting define **`VSTHOST_DEBUG`**
(Player Settings) to restore `[VstHost] CreateInstance` / `DestroyInstance` traces.
Leave it unset for normal use.

## Verification B — MIDI + VST (same Unity project)

Use the MIDI project at `C:\Users\0x0ba\Documents\github\Unity-MIDI-Plugin`.

1. Open that project in Unity.
2. Add the VST package as a local package in `Packages/manifest.json`:

```json
"jp.kshoji.unity.vst3nativehost": "file:C:/Users/0x0ba/Documents/github/Unity-VST3-Bridge"
```

3. Wait for compile. Menu **Window → VST3 Host → Sync MIDI Plugin Define** (or auto-sync) so `FEATURE_MIDI_PLUGIN` is set.
4. Import **VST3 Host Sample** (or add `VstHostSampleController` to a scene that already has `MidiManager`).
5. Ensure MIDI Plugin is initialized (existing MIDI sample scene / `MidiManager.InitializeMidi*` as usual).
6. Enter Play Mode, load a VSTi, play notes from a MIDI device **or** the sample Note On buttons.

Expected: device → MIDI Plugin events → `VstHostMidiAdapter` → native queue → VSTi audio.

Do **not** place VST scripts under `Assets/MIDI`.

## IL2CPP Standalone Windows x64

Automated (recommended):

```powershell
.\native\windows-vst-host\Run-Il2CppVerify.ps1
```

Uses Unity 2022.3.x by default, embeds the package **without** `native/`, builds Standalone Win64 IL2CPP, and asserts `VstHostNative.dll` is in the player output.

Manual:

1. File → Build Settings → Windows → Architecture **x86_64** (or **ARM64** with `Plugins/Windows/ARM64` deployed).
2. Player Settings → Configuration → Scripting Backend **IL2CPP**.
3. Menu **Window → VST3 Host → Verify Plugin Platforms** (x64: Editor + Win64; ARM64: Windows ARM64 only).
4. `Runtime/link.xml` preserves the Runtime assembly for P/Invoke.
5. Menu **Window → VST3 Host → Build IL2CPP Win64 (Verify)**, or Build and Run and play Note On.

When linking the repo via `file:` / Git, `native/**/build*` DLLs may appear — use **Window → VST3 Host → Sanitize Extra Native Plugins**.

## MIDI-only build must not contain VST

From this repository:

```powershell
.\native\windows-vst-host\Verify-MidiIsolation.ps1 `
  -MidiRepoRoot "C:\Users\0x0ba\Documents\github\Unity-MIDI-Plugin"
```

The script fails if `VstHostNative.dll`, VST3 SDK trees, or VST Runtime scripts appear under the MIDI repo `Assets` / release packaging paths (plan documents are allowed).
