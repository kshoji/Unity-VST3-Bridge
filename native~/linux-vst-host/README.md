# VstHostNative — Linux bridge

Builds **`VstHostNative.so`** (x86_64) for Unity `DllImport("VstHostNative")`.
Shares C++ sources and the VST3 SDK submodule with `../windows-vst-host/`.

## Prerequisites

- Linux x86_64 (Ubuntu 22.04+ / WSL2 Ubuntu recommended for day-to-day builds)
- CMake 3.25+
- g++ or clang with C++17
- Ninja (optional; falls back to Unix Makefiles)
- VST3 SDK submodule initialized:

```bash
# From repository root
git submodule update --init --recursive
```

## Building

```bash
chmod +x ./Build.sh
./Build.sh                  # Release x86_64
./Build.sh --Install        # Build and copy to Plugins/Linux/x86_64/
./Build.sh --Debug
```

| Output | Unity Plugin settings |
|--------|------------------------|
| `Plugins/Linux/x86_64/VstHostNative.so` | Editor (OS = Linux) + Standalone Linux64 |

`.meta` policy: see [Plugins/Linux/x86_64/README.md](../../Plugins/Linux/x86_64/README.md).

## Smoke test

```bash
./Build.sh
# Needs a free/SDK sample such as again.vst3 / again-sample-accurate.vst3
./build/bin/VstHostSmokeTest
# or with an explicit folder:
VSTHOST_SMOKE_FOLDER="$HOME/.vst3" ./build/bin/VstHostSmokeTest
```

Default scan uses SDK `getModulePaths()` (`~/.vst3`, `/usr/lib/vst3`,
`/usr/local/lib/vst3`, plus app-local `vst3`).

To build Steinberg **AGain Sample Accurate** for smoke (no VSTGUI / no sudo packages):

```bash
# From repository root
cmake -S native~/windows-vst-host/vst3sdk -B /tmp/vst3sdk-build \
  -DCMAKE_BUILD_TYPE=Release \
  -DSMTG_ENABLE_VSTGUI_SUPPORT=OFF \
  -DSMTG_ENABLE_VST3_HOSTING_EXAMPLES=OFF
cmake --build /tmp/vst3sdk-build --target again-sample-accurate --parallel
mkdir -p ~/.vst3
cp -a /tmp/vst3sdk-build/VST3/Release/again-sample-accurate.vst3 ~/.vst3/
```

Classic `again` (with SideChain) needs `SMTG_ENABLE_VSTGUI_SUPPORT=ON` and Linux
GUI deps (cairo / X11 / gtkmm).

## Design notes

- Loader: SDK `module_linux.cpp` (`dlopen`), not `LoadLibraryW` / CFBundle
- Thread checker: `threadchecker_linux.cpp`
- Load/Unload lifecycle matches Windows/macOS (`g_audioLifecycleMutex`, module cache)
- C ABI / export names unchanged (`visibility("default")` in `VstHostNative.h`)
- **Fault handling:** same as macOS — no SEH; a plugin crash can exit Unity.
  See [Documentation~/limitations.md](../../Documentation~/limitations.md).

## WSL2 vs full Linux VM

WSL2 is enough to **build** `.so` and run the native smoke test. Use a real
Linux desktop (e.g. VirtualBox Ubuntu) or CI for Unity Editor / Player audio
verification.

VST® is a registered trademark of Steinberg Media Technologies GmbH.
See repository root [NOTICE.md](../../NOTICE.md).
