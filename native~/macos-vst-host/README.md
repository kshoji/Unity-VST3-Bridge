# VstHostNative — macOS bridge

Builds **`VstHostNative.bundle`** (Universal arm64 + x86_64 by default) for
Unity `DllImport("VstHostNative")`. Shares C++ sources and the VST3 SDK submodule
with `../windows-vst-host/`.

## Prerequisites

- macOS with Xcode Command Line Tools
- CMake 3.25+
- Ninja (optional; falls back to Unix Makefiles)
- VST3 SDK submodule initialized:

```bash
# From repository root
git submodule update --init --recursive
```

## Building

```bash
chmod +x ./Build.sh
./Build.sh                  # Release Universal
./Build.sh --Install        # Build and copy to Plugins/macOS/
./Build.sh --Arch arm64 --Install
./Build.sh --Debug
```

| Output | Unity Plugin settings |
|--------|------------------------|
| `Plugins/macOS/VstHostNative.bundle` | Editor (OS = OSX) + Standalone OSXUniversal |

`.meta` policy: see [Plugins/macOS/README.md](../../Plugins/macOS/README.md).

## Smoke test

```bash
./Build.sh
./build/bin/VstHostSmokeTest
# or with an explicit folder:
VSTHOST_SMOKE_FOLDER="$HOME/Library/Audio/Plug-Ins/VST3" ./build/bin/VstHostSmokeTest
```

Default scan uses SDK `getModulePaths()` (user + system Library VST3 folders).
Expects a free/SDK sample such as `again.vst3` / AGain on the machine (not redistributed).

## Design notes

- Loader: SDK `module_mac.mm` (CFBundle), not `LoadLibraryW`
- Thread checker: `threadchecker_mac.mm`
- Load/Unload lifecycle matches Windows (`g_audioLifecycleMutex`, module cache held until Terminate)
- C ABI / export names unchanged (`visibility("default")` in `VstHostNative.h`)

VST® is a registered trademark of Steinberg Media Technologies GmbH.
See repository root [NOTICE.md](../../NOTICE.md).
