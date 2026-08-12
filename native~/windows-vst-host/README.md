# VstHostNative — Native Bridge for Unity Plugin Host for VST3

C++ shared library that wraps the VST3 SDK hosting API behind a flat C ABI
consumed by Unity via P/Invoke (`DllImport("VstHostNative")`).

## Prerequisites

- **Windows 10/11** (x64 host; ARM64 cross-compile supported)
- **Visual Studio 2022+** (or Build Tools) with C++ desktop workload **and ARM64 tools**
- **CMake 3.25+**

## Building

```powershell
# From this directory
.\Build.ps1                     # Release x64 + ARM64
.\Build.ps1 -Install            # Build both and copy to Plugins/Windows/{x86_64,ARM64}/
.\Build.ps1 -Platform x64 -Install
.\Build.ps1 -Platform ARM64 -Install
.\Build.ps1 -Configuration Debug
```

| Arch | Output | Unity Plugin settings |
|------|--------|------------------------|
| x64 | `Plugins/Windows/x86_64/VstHostNative.dll` (+ `.meta`) | Editor (Windows x86_64) + Standalone Win64 |
| ARM64 | `Plugins/Windows/ARM64/VstHostNative.dll` (+ `.meta`) | Standalone Windows ARM64 only |

PluginImporter `.meta` files are committed next to each DLL (same policy as
`Plugins/macOS/VstHostNative.bundle.meta`). After `-Install`, do not delete them.


## Exported Functions

| Function | Description |
|----------|-------------|
| `VstHost_Initialize` | Set sample rate and block size; install host context |
| `VstHost_Terminate` | Release all instances and shut down (2s timeout → `kVstHostErrorBusy`, host left initialized) |
| `VstHost_ScanFolder` | Enumerate VST3 plugins (null/empty = SDK standard folders) |
| `VstHost_Load` | Load a .vst3 and create an instance (optional class UID) |
| `VstHost_Unload` | Destroy a plugin instance (2s timeout → `kVstHostErrorBusy`, instance kept for retry) |
| `VstHost_SendMidi1` | Enqueue MIDI 1.0 short message (mutex-serialized MPSC ring; drop-oldest on full) |
| `VstHost_Process` | Drain MIDI → VST `process` → planar stereo float buffers (Aux silent) |
| `VstHost_ProcessWithSidechain` | Same, plus feed Aux input bus 0 from sidechain L/R |
| `VstHost_GetParameterCount` / `GetParameterInfo` | Enumerate controller parameters |
| `VstHost_Get/SetParameterNormalized` | Read/write normalized [0,1] (queues audio-thread change) |
| `VstHost_GetProgramCount` / `GetProgramName` / `SetProgram` | Best-effort presets |
| `VstHost_GetState` / `SetState` | Component + controller state blob |

## Smoke test

```powershell
.\Build.ps1 -Platform x64
.\build-x64\bin\Release\VstHostSmokeTest.exe
```

Expects SDK sample `again.vst3` under the platform default VST3 folder, or set
`VSTHOST_SMOKE_FOLDER` to an explicit scan directory.

## VST3 SDK

The SDK is included as a Git submodule at `vst3sdk/`.
After cloning, run:

```bash
git submodule update --init --recursive
```

Only the `pluginterfaces`, `base`, `public.sdk`, and `cmake` sub-submodules are
required for building. `vstgui4`, `doc`, and `tutorials` are not needed.

VST® is a registered trademark of Steinberg Media Technologies GmbH.

License and acquisition details for the SDK: repository root [NOTICE.md](../../NOTICE.md).
