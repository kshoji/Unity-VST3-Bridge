# Changelog

## 1.3.0

### Breaking
- Removed **`VstPluginChain`**. Use **`VstAudioGraph`** builders instead:
  - `SetSlots` + Parallel mix → `BuildParallelInstrumentsThenSerialEffects`
  - `SetSlots` + StrictSerial → `BuildStrictSerial`
  - `MixExternalInput` → `Build*(…, mixExternalInput: true)` / ExternalIn
  - Channel routes: slot index → **instrument node id**
  - See [Documentation~/audio-graph.md](Documentation~/audio-graph.md) migration table.
- Removed [Documentation~/plugin-chain.md](Documentation~/plugin-chain.md) (replaced by `audio-graph.md`).
- `VstHostChannelRouteSync` now syncs Adapter ↔ **`VstAudioGraph`**
  (`AdapterToGraph` / `GraphToAdapter`; former Chain enum names removed).
- `VstHostDspMidiOutBridge` routes via **`VstAudioGraph`** (not Chain).
- `VstHostChuckEffectBridge` target **`AudioGraphExternalInput`**
  (replaces Plugin Chain External Input).
- Sample left panel: **Single | Audio Graph** only (Plugin Chain mode removed).

### Added
- Native `VstHost_ProcessWithSidechain` (Win / macOS / Linux shared ABI): feed Aux
  input bus 0 with planar L/R; `VstHost_Process` unchanged (silent Aux).
- Managed `VstHostNative.VstHost_ProcessWithSidechain` + `VstHostManager.ProcessWithSidechain`.
- SmokeTest energy check for SDK **AGain SideChain** with real sidechain audio.
- Docs: sidechain verify steps in [Documentation~/verification.md](Documentation~/verification.md).
- `VstAudioGraph` (Filter / `OnAudioFilterRead`): DAG nodes (ExternalIn, Instrument,
  Effect main/sidechain, Mix, Split, Gain, Output), arm + scratch pool, ChannelRoutes,
  `BuildParallelInstrumentsThenSerialEffects` / `BuildStrictSerial` /
  `BuildSendReturn` / `BuildSidechain` / `BuildSidechainFromSingleSource`.
- Sample **Audio Graph** demos: Parallel→Serial / Send/Return / Sidechain.
- Editor: **Audio Graph** window (`Window → VST3 Host → Audio Graph`) — read-only
  topology (nodes / edges / ChannelRoutes / arm status; Send & Sidechain highlighted).
  Thin `VstAudioGraph` Inspector overview + Open Window button.
- EditMode/PlayMode graph tests (topo / cycle / `Build*` counts),
  Audio Graph manual matrix in [verification.md](Documentation~/verification.md),
  `Run-TestsAndBuildVerify.ps1` (tests + Standalone verify builds),
  ARM64 importer detect without ARM64 player module,
  Linux64 verify IL2CPP→Mono fallback + Windows soft-pass on plugin flags.

## 1.2.0

### Added
- Linux support: `native~/linux-vst-host/` (CMake + `Build.sh`),
  `Plugins/Linux/x86_64/VstHostNative.so`, Linux scan paths,
  `VstHostBuildVerify` Standalone Linux64 helper, PluginImporter sanitize for
  `.so`, and documentation updates.
- SmokeTest applies controller `defaultNormalized` before the first energy
  check so SDK samples like AGain Sample Accurate pass without VSTGUI.

### Changed
- Docs describe a WSL2 (native build / smoke) → full Linux desktop (Unity
  Editor audio) workflow. Linux Editor was verified with SDK **mda DX10**
  (Instrument) and **Plugin Chain** on Ubuntu under VirtualBox.

## 1.1.0

### Added
- `VstMidiParameterMapping` + `VstHostMidiParameterMapper` (CC / 14-bit CC / pitch bend → parameters, MIDI Learn).
- `VstHostSmfLink` to wire `SmfPlayer.outputDeviceId` to a virtual device for `VstHostMidiAdapter`.
- Channel routes on `VstHostMidiAdapter` (multi-timbral), optional Program Change → `SetProgram`, forward filters for CC / pitch bend / PC.
- `VstHostMidiAdapter.ChannelRoutes` public accessor; `VstHostChannelRouteSync` to align Adapter ↔ `VstPluginChain` routes.
- `VstHostMidiFilterLink` — wire `MidiChannelFilter` → `VstHostMidiAdapter`.
- `VstHostEventSink` — UnityEvent-friendly Note / Parameter / Program / CC / PitchBend API
  (works without MIDI; usable from `MidiInputRouter`).
- `VstPresetAsset`, `VstPresetBrowser`, Editor preset browser (`Window/VST3 Host/Preset Browser`).
- Timeline: `VstParameterTrack` / `VstParameterClip` / mixer, `VstProgramChangeMarker`, `VstTimelineNotificationReceiver`, `VstParameterTarget`
  (`FEATURE_USE_TIMELINE` via `com.unity.timeline` versionDefines).
- Animator: `VstAnimatorMapping`, `VstAnimatorDriver` (VST ↔ Animator floats).
- Input System: `InputSystemToVstBridge` (notes, parameters, CC, pitch bend, program)
  (`FEATURE_INPUT_SYSTEM` via `com.unity.inputsystem` versionDefines).
- Editor: Plugin Browser (category + vendor/tag), Activity Monitor, Virtual Controller, Project Settings (extra scan folders).
- `VstHostActivity` bus hooked from `SendMidi1` / `SetParameterNormalized` / `SetProgram`.
- `VstPluginChain` for parallel instruments + serial effects (channel route helper).
- `VstHostDspMidiQueue` + flush hooks on `VstHostAudioFilter` / chain.
- Scriptable Audio (Unity 6.3+): `VstHostGenerator` (`IAudioGenerator`), `VstHostDspMidiOutBridge` (`IMidiDspTimedMidiOutput`).
- Sample: **Plugin Chain** mode plus right-panel `VstHostSampleFeatureDemos` (Guide / Presets / Mapping / Routes).
- Visual Scripting: VST3 Host units + `VstVisualScriptingBridge`
  (`FEATURE_USE_VISUALSCRIPTING` via `com.unity.visualscripting` versionDefines).
- Network MIDI: `VstHostNetworkMidiLink` (`FEATURE_MIDI_NETWORK`).
- Chunity: `VstHostChuckEventMidiLink`, `VstHostChuckEffectBridge`; `VstPluginChain.MixExternalInput`.
- Docs: [Documentation~/midi-integration.md](Documentation~/midi-integration.md), [parameters.md](Documentation~/parameters.md), [timeline.md](Documentation~/timeline.md), [animator-input.md](Documentation~/animator-input.md), [editor-tools.md](Documentation~/editor-tools.md), [plugin-chain.md](Documentation~/plugin-chain.md) (removed in 1.3.0 → [audio-graph.md](Documentation~/audio-graph.md)), [scriptable-audio.md](Documentation~/scriptable-audio.md), [visual-scripting.md](Documentation~/visual-scripting.md), [network-midi.md](Documentation~/network-midi.md), [chunity.md](Documentation~/chunity.md).
- Runtime tests for mapping / preset asset helpers.

## 1.0.0

### Fixed
- Commit Windows `VstHostNative.dll.meta` (x86_64 + ARM64) and stop ignoring
  them in `Plugins/Windows/**/.gitignore` (macOS already tracked `.meta`).
- `VstHostBuildVerify` now requires Windows ARM64 to be explicitly enabled and
  rejects cross-OS platform flags on each plugin binary.
- Sample README updated for Windows + macOS (no longer Windows-only).

### Added
- macOS support: `native~/macos-vst-host/` (CMake + `Build.sh`), Universal
  `Plugins/macOS/VstHostNative.bundle`, macOS scan paths, Editor OSX verify /
  isolation helpers, documentation updates.
- Renamed `native/` → **`native~/`** so Git URL / `file:` installs do not import
  SDK sources (`.npmignore` alone does not apply). See [Documentation~/package-excludes.md](Documentation~/package-excludes.md).
- Initial package layout (Runtime, Editor, Tests, Samples~, Documentation~, Plugins).
- Docs: [NOTICE.md](NOTICE.md) SDK/trademark details, [Documentation~/usage.md](Documentation~/usage.md),
  [Documentation~/limitations.md](Documentation~/limitations.md).
- `Plugins/macOS/` scaffold.
