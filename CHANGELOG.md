# Changelog

## 1.4.2

### Added
- **Runtime MCP (Standalone):** `Runtime/Mcp*.Runtime` assemblies register `vst3-*`
  host control, graph, presets (Resources), MIDI send, and optional wiring tools in
  Desktop builds and Editor Play Mode (Unity-MCP ≥ 0.76; not bundled).
  - `VstHostRuntimeMcpConfig` (Resources) + `VstHostMcpRuntimeBootstrap` (opt-in;
    non-empty **token** required when enabled).
  - Window → VST3 Host → **Export Runtime MCP Config to Resources** (and Project Settings).
  - Shared helpers: `Runtime/Mcp.Core/` (`McpExecutionContext`, tool helpers).
  - New: `vst3-timeline-director-control` (scene `PlayableDirector` play/pause/stop/time).
  - `vst3-scan` merges `extraScanFolders` from the runtime config after OS-standard folders.
  - Package tests under `Tests/Runtime/Mcp/` (when Unity-MCP resolves).
  - Docs: Runtime MCP sections in [Documentation~/mcp.md](Documentation~/mcp.md),
    [verification.md](Documentation~/verification.md), [limitations.md](Documentation~/limitations.md).
  - `link.xml` preserves for MCP / ReflectorNet under IL2CPP.

### Changed
- Most former Editor MCP tools moved to Runtime asmdefs so Editor Play Mode and
  Standalone share one tool ID set (no duplicate `tools/list` entries).
- Resource `vst3://settings` reads `VstHostRuntimeMcpConfig` in builds.

### Editor-only (unchanged capability, Editor asmdefs)
- `vst3-settings-get/set`, `vst3-sync-midi-define`, `vst3-verify-platforms`,
  `vst3-preset-create-asset`, `vst3-parameter-panel-setup`, `vst3-animator-setup`,
  `vst3-vs-register`, Timeline asset tools, CC mapping CRUD / midi-learn.

## 1.4.1

### Removed
- Development / smoke-test Unity-MCP prompts (`Prompt_VstHost.cs`):
  `vst3-quick-start-note`, `vst3-validate-troubleshoot`, `vst3-params-preset-smoke`,
  `vst3-midi-adapter-smoke`, `vst3-midi-learn-smoke`, `vst3-graph-parallel-smoke`,
  `vst3-sa-dsp-midi-smoke`.
- Development Unity-MCP tools: `vst3-ping`, `vst3-validate`.

### Changed
- `vst3-verify-platforms` remains available (moved next to host status tooling).
- Docs: [Documentation~/mcp.md](Documentation~/mcp.md) and
  [verification.md](Documentation~/verification.md) no longer document smoke prompts
  or the removed tools.

## 1.4.0

### Added
- Optional Unity-MCP tools: `Editor/Mcp/` asmdef
  `jp.kshoji.unity.vst3nativehost.Mcp` (compiles only when
  `com.ivanmurzak.unity.mcp` ≥ 0.76 and `UNITY_MCP_READY`). Unity-MCP is not bundled.
  - Core: `vst3-ping`, `features-status`, `host-status`, host init/terminate, scan,
    settings, load/unload, audio filter, note/CC/PC/pitch/aftertouch/midi1,
    note-off-all, validate, verify-platforms, activity read/clear/enable,
    diagnostics-read, `sync-midi-define`, `event-sink-setup`,
    `animator-setup`, `dsp-midi-schedule`, `vs-register`
  - Params / presets: params-list/get/set, programs-list/set-program, state-get/set,
    preset create/capture/apply/list/ab, parameter-panel-setup
  - MIDI wiring (`Editor/Mcp.Midi/`, `FEATURE_MIDI_PLUGIN`): adapter-setup, smf-link,
    cc-mapping create/list/edit, midi-learn, channel-routes, filter-link, route-sync
  - Audio Graph: status/clear/build-*/set/connect/bypass/gains/channel-routes/arm
  - Optional integrations: `Mcp.Timeline`, `Mcp.InputSystem`, `Mcp.ScriptableAudio`,
    `Mcp.ScriptableAudio.Midi`, `Mcp.Midi.Chunity`, `Mcp.Midi.Network`
  - Resources: `vst3://features|scanned|instances|activity/recent|diagnostics|settings|params/{id}|graph/{object}`
  - Prompts: `vst3-quick-start-note`, `vst3-validate-troubleshoot`, `vst3-params-preset-smoke`,
    `vst3-midi-adapter-smoke`, `vst3-midi-learn-smoke`, `vst3-graph-parallel-smoke`,
    `vst3-sa-dsp-midi-smoke`
  - Docs: [Documentation~/mcp.md](Documentation~/mcp.md)
- `VstHostActivity` recent ring (`GetRecent` / `ClearRecent`); capture without
  Activity Monitor subscribers.
- `VstHostAudioDiagnostics.Snapshot` / `ClearLifetime` lifetime counters.
- `VstHostProjectSettings` made public for MCP / tooling.
- `VstHostMidiDefineSync` public API for MCP define sync.
- MCP smoke feedback: prompts use exact tools/list names + virtual inject path;
  `vst3-params-list preferAutomate`; `vst3-preset-create-asset overwrite`;
  `vst3-validate` reports `__VstHostMcpAudio`; Adapter/Mapper + Activity dedupe
  MIDI1+MIDI2 dual-dispatch duplicates.
- `vst3-preset-ab`: Success reports slot bytes/sha8/slotsEqual/applied/preferSlotB;
  Warning when slotsEqual; optional silent Process flush before capture;
  `SetState` raises Activity (apply/toggle visible in activity-read).

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
