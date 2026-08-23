# Unity-MCP integration (optional)

Optional [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) tools let Cursor (and other MCP hosts) drive this package: scan / load / note / parameters / presets / Activity / Audio Graph, plus MIDI Plugin wiring when present.

**Unity-MCP is not bundled** with this package. Install it separately; without it, the MCP assemblies do not compile.

## Requirements

| Item | Value |
|------|-------|
| Unity-MCP | `com.ivanmurzak.unity.mcp` ≥ **0.76.0** (sets `FEATURE_UNITY_MCP` + `UNITY_MCP_READY` when NuGet deps resolve) |
| This package | `jp.kshoji.unity.vst3nativehost` (Git URL / local `file:`) |
| MIDI tools | Optional [Unity MIDI Plugin](https://assetstore.unity.com/packages/slug/198917) + `FEATURE_MIDI_PLUGIN` |

Custom tool conventions follow the Unity-MCP wiki:
[Custom Tools Development](https://github.com/IvanMurzak/Unity-MCP/wiki/Custom-Tools-Development).

## Layout

| Path | asmdef | When it compiles |
|------|--------|------------------|
| `Editor/Mcp/` | `jp.kshoji.unity.vst3nativehost.Mcp` | Unity-MCP ready |
| `Editor/Mcp.Midi/` | `…Mcp.Midi` | Unity-MCP + `FEATURE_MIDI_PLUGIN` |
| `Editor/Mcp.Timeline/` | `…Mcp.Timeline` | Unity-MCP + `FEATURE_USE_TIMELINE` |
| `Editor/Mcp.InputSystem/` | `…Mcp.InputSystem` | Unity-MCP + Input System |
| `Editor/Mcp.ScriptableAudio/` | `…Mcp.ScriptableAudio` | Unity-MCP + Unity 6000.3+ |
| `Editor/Mcp.ScriptableAudio.Midi/` | `…Mcp.ScriptableAudio.Midi` | SA + MIDI |
| `Editor/Mcp.Midi.Chunity/` | `…Mcp.Midi.Chunity` | MIDI + Chunity |
| `Editor/Mcp.Midi.Network/` | `…Mcp.Midi.Network` | MIDI + Network MIDI |

Runtime assemblies never reference Unity-MCP. Missing optional packages simply omit those tools from registration.

## Quick start

1. Install Unity-MCP and open the project in Cursor (or another MCP client).
2. Call `vst3-ping` then `vst3-features-status`.
3. Prefer built-in prompts: `vst3-quick-start-note` (MIDI-less), `vst3-validate-troubleshoot`, `vst3-params-preset-smoke`, `vst3-midi-adapter-smoke`, `vst3-graph-parallel-smoke`.

Use only tool names from `tools/list`. Always finish hanging notes with `vst3-note-off-all`.

## Edit Mode vs Play Mode

| Kind | Examples | Behavior |
|------|----------|----------|
| Edit Mode OK | `vst3-ping`, `features-status`, `host-status`, scan/settings/list (when Description says so) | Normal success |
| Play Mode required | note / MIDI1 send / AudioFilter setup / Process-backed paths | `[Error] {toolId} requires Play Mode...` |
| Host uninitialized | load / note / params | `[Error] {toolId} requires an initialized VST host...` |

## Tool groups (prefix `vst3-`)

### Core host

`ping` · `features-status` · `host-status` · `host-init` / `host-terminate` · `scan` / `scan-folder` / `default-scan-folders` · `settings-get` / `settings-set` · `load` / `unload` / `list-instances` · `setup-audio-filter` / `audio-filter-detach` · `note-on` / `note-off` / `note-off-all` · `send-cc` / `send-pc` / `send-pitch` / `send-aftertouch` / `send-midi1` · `validate` / `verify-platforms` · `activity-read` / `activity-clear` / `activity-enable` · `diagnostics-read` · `sync-midi-define` · `event-sink-setup` · `animator-setup` · `dsp-midi-schedule` · `vs-register`

### Parameters / presets

`params-list` · `param-get` / `param-set` · `programs-list` / `set-program` · `state-get` / `state-set` · `preset-create-asset` / `preset-capture` / `preset-apply` / `preset-list` / `preset-ab` · `parameter-panel-setup`

### MIDI Plugin wiring (`Mcp.Midi`)

`midi-adapter-setup` · `smf-link` · `cc-mapping-create` / `list` / `edit` · `midi-learn` · `channel-routes` · `filter-link` · `route-sync`

Device I/O, inject, and SMF transport stay on MIDI MCP (`midi-*` / `smf-*`). These tools only wire Adapter / SmfLink / mappings to the VST host.

### Audio Graph

`graph-status` · `graph-clear` · `graph-build-parallel` / `serial` / `send` / `sidechain` · `graph-set` / `graph-connect` · `graph-bypass` · `graph-set-node-gain` / `graph-set-edge-gain` · `graph-channel-routes` · `graph-arm`

Prefer **Build\*** helpers over raw `graph-set`.

### Optional integrations

| Tools | Condition |
|-------|-----------|
| `timeline-param-track` / `program-marker` | Timeline |
| `inputsystem-bridge` | Input System |
| `sa-generator-setup` | Scriptable Audio (Unity 6000.3+) |
| `sa-midi-bridge` | SA + MIDI |
| `chuck-effect` / `chuck-event-midi-link` | Chunity |
| `network-midi-link` | Network MIDI |

## Resources / prompts

| Resources | `vst3://features`, `scanned`, `instances`, `activity/recent`, `diagnostics`, `settings`, `params/{pluginId}`, `graph/{object}` |
| Prompts | `vst3-quick-start-note`, `vst3-validate-troubleshoot`, `vst3-params-preset-smoke`, `vst3-midi-adapter-smoke`, `vst3-midi-learn-smoke`, `vst3-graph-parallel-smoke`, `vst3-sa-dsp-midi-smoke` |

## Boundary with Unity MIDI Plugin MCP

| MIDI MCP | This package |
|----------|--------------|
| `midi-init` / devices / send / monitor / virtual inject | Adapter input path verification |
| `smf-player-control` | `vst3-smf-link` wires output |
| Docs / `midi-features-status` detection | All `vst3-*` host tools |

Do not implement `vst3-*` inside the MIDI package.

## Editor menu ↔ tools

| Window → VST3 Host | MCP |
|--------------------|-----|
| Plugin Browser | `vst3-scan` / `load` / note / `settings-*` |
| Preset Browser | `vst3-preset-*` / `programs-*` / `preset-ab` |
| Activity Monitor | `vst3-activity-read` / `enable` |
| Audio Graph | `vst3-graph-status` / `build-*` / bypass / gain |
| Virtual Controller | `vst3-note-*` / `send-cc` / `note-off-all` |
| Sync MIDI Plugin Define | `vst3-sync-midi-define` |
| Verify Plugin Platforms | `vst3-verify-platforms` |
| Visual Scripting / Register Nodes | `vst3-vs-register` |
| Project Settings → VST3 Host | `vst3-settings-get` / `set` |

## Verification

Platform / MIDI ON-OFF / IL2CPP checks follow the existing matrix in
[verification.md](verification.md). MCP does not replace Standalone Build Verify;
use `vst3-verify-platforms` plus manual or CI verify menus as today.

## Out of scope

- Plugin-native GUI (`IPlugView`)
- Bundling Unity-MCP or third-party `.vst3`
- Calling native `Process` from the audio thread via MCP (MainThread host API / components only)
- Implementing `vst3-*` inside Unity MIDI Plugin
