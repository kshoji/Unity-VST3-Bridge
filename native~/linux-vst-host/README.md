# VstHostNative — Linux bridge

Builds **`VstHostNative.so`** (x86_64) for Unity `DllImport("VstHostNative")`.
Shares C++ sources and the VST3 SDK checkout with `../windows-vst-host/`.

## Prerequisites

- Linux x86_64 (Ubuntu 22.04+ / WSL2 Ubuntu recommended for day-to-day builds)
- CMake 3.25+
- g++ or clang with C++17
- Ninja (optional; falls back to Unix Makefiles)
- VST3 SDK fetched under `../windows-vst-host/vst3sdk`:

```bash
# From repository root
cd native~/windows-vst-host
chmod +x ./Fetch-Vst3Sdk.sh && ./Fetch-Vst3Sdk.sh
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

To build Steinberg **AGain Sample Accurate** for smoke (no VSTGUI / no sudo packages).
Use a **persistent** build dir under `$HOME` (survives WSL reboots; `/tmp` does not):

```bash
# From repository root
BUILD="$HOME/vst3sdk-build"
cmake -S native~/windows-vst-host/vst3sdk -B "$BUILD" \
  -DCMAKE_BUILD_TYPE=Release \
  -DSMTG_ENABLE_VSTGUI_SUPPORT=OFF \
  -DSMTG_ENABLE_VST3_HOSTING_EXAMPLES=OFF
cmake --build "$BUILD" --target again-sample-accurate --parallel
mkdir -p ~/.vst3
# SMTG may already symlink into ~/.vst3; cp is only needed if that step was skipped.
cp -a "$BUILD/VST3/Release/again-sample-accurate.vst3" ~/.vst3/ 2>/dev/null || true
```

For **Unity Editor audio** (Note On), build SDK **mda-vst3** (includes **mda DX10**
and other instruments). Effects alone stay silent without an audio input:

```bash
BUILD="$HOME/vst3sdk-build"
cmake --build "$BUILD" --target mda-vst3 --parallel
cp -a "$BUILD/VST3/Release/mda-vst3.vst3" ~/.vst3/ 2>/dev/null || true
# Confirm Contents/x86_64-linux/mda-vst3.so exists before copying to a VM
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

| Step | Environment |
|------|-------------|
| Build `.so` + native smoke | **WSL2** (or native Linux) |
| Unity Editor / Player audio (mda DX10, Audio Graph) | **Full Linux desktop** (e.g. VirtualBox Ubuntu) |

WSL2 is enough to produce `Plugins/Linux/x86_64/VstHostNative.so`. Copy the
package and `~/.vst3` plugins into the VM for Editor checks. See
[Documentation~/verification.md](../../Documentation~/verification.md)
(Linux Editor section).

VST® is a registered trademark of Steinberg Media Technologies GmbH.
See repository root [NOTICE.md](../../NOTICE.md).
