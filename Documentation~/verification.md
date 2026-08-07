# Verification

## Prerequisites

- Unity 2022.3+ (or Unity 6)
- Native bridge present for your Editor OS:
  - **Windows:** `.\native~\windows-vst-host\Build.ps1 -Install`
  - **macOS:** `./native~/macos-vst-host/Build.sh --Install`
  - **Linux:** `./native~/linux-vst-host/Build.sh --Install` (WSL2 OK for native smoke; use a full Linux desktop for Unity audio verify)
- Local `.vst3` plugins (do **not** redistribute third-party plugins)

## VST only (manual notes)

1. Create an empty Unity project **or** use Package Manager Git URL:
   `https://github.com/kshoji/Unity-VST3-Bridge.git`
2. Import sample **VST3 Host Sample**.
3. Open `VstHostSampleScene`, Enter Play Mode.
4. Confirm scan lists plugins, Load succeeds, **Note On** produces audio.
5. Optional chain check: switch to **Plugin Chain**, pick Instrument + Effect, **Build Chain**, **Note On**, toggle **Bypass effect**.
6. Optional feature demos (right panel): **Presets** A/B, **Mapping** CC simulation, **Routes** channel→slot.
7. Confirm `Assets/MIDI` is **not** required.

Expected: instrument sound through `VstHostAudioFilter` / `AudioSource` (or `VstPluginChain` in chain mode).

For load/unload crash investigation, add scripting define **`VSTHOST_DEBUG`**
(Player Settings) to restore `[VstHost] CreateInstance` / `DestroyInstance` traces.
Leave it unset for normal use.

### Instrument vs Effect

| Category | Example | How to hear sound |
|----------|---------|-------------------|
| **Instrument** | mda DX10, mda Piano | `Mode = Instrument`, then **Note On** |
| **Effect** | AGain, ADelay | Needs audio **input** (`Mode = Effect` + playing `AudioSource` clip / upstream signal). Note On alone is silent. |

### Unknown / commercial plugins

Compatibility with arbitrary commercial `.vst3` plugins is **not guaranteed**
([limitations.md](limitations.md)). For a plugin you have not used with this host
before, validate first on **Windows** (where some native faults inside `process`
may return `ProcessFailed` instead of killing the process), or in a **dedicated
throwaway Unity project**. On **macOS** and **Linux**, a fatal fault in the plugin can terminate
the Editor / Player with no recovery — do not assume an SEH-style catch exists.
Prefer known-good free/SDK samples (e.g. AGain, mda DX10) for day-to-day smoke tests.

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

```bash
# Linux (WSL2 or native)
./native~/linux-vst-host/Build.sh
./native~/linux-vst-host/build/bin/VstHostSmokeTest
# optional explicit folder:
VSTHOST_SMOKE_FOLDER="$HOME/.vst3" ./native~/linux-vst-host/build/bin/VstHostSmokeTest
```

Expects AGain / `again.vst3` / `again-sample-accurate.vst3` (or another free
sample) under `~/.vst3` or the SDK default paths. For instrument coverage in
the same smoke binary, also install SDK `mda-vst3` (see below).

### Sidechain Process (AGain SideChain)

Native ABI `VstHost_ProcessWithSidechain` feeds the plugin’s second audio input
bus (typically `kAux`) with real L/R planar buffers. Existing `VstHost_Process`
keeps Aux silent (crash-safe binding unchanged).

**Manual / smoke check** (requires classic SDK `again.vst3` with **AGain SideChain**
in the name — not `again-sample-accurate` alone):

1. Rebuild the native bridge (`Build.ps1` / `Build.sh`) and run `VstHostSmokeTest`.
2. Expect log lines similar to:
   - `Process AGain SideChain ok` (silent-Aux regression still passes)
   - `ProcessWithSidechain AGain SideChain energy silentAux=… withSc=…`
   - `withSc` energy must be **greater** than `silentAux` (AGain SideChain adds aux into the output)
3. If SideChain is missing: `WARN: AGain SideChain not found; skipped` (smoke still OK).
4. Unity / C#: `VstHostManager.ProcessWithSidechain(...)` mirrors the native call.
   Plugins without an Aux bus ignore the sidechain buffers (main-only process).

On Linux, classic `again` (with SideChain) needs VSTGUI; see
[linux-vst-host README](../native~/linux-vst-host/README.md). Windows / macOS SDK
builds with VSTGUI typically include SideChain.

### Linux SDK samples (no VSTGUI)

From the repository root (WSL2 or native Linux):

```bash
cmake -S native~/windows-vst-host/vst3sdk -B /tmp/vst3sdk-build \
  -DCMAKE_BUILD_TYPE=Release \
  -DSMTG_ENABLE_VSTGUI_SUPPORT=OFF \
  -DSMTG_ENABLE_VST3_HOSTING_EXAMPLES=OFF
cmake --build /tmp/vst3sdk-build --target again-sample-accurate --parallel
cmake --build /tmp/vst3sdk-build --target mda-vst3 --parallel
mkdir -p ~/.vst3
cp -a /tmp/vst3sdk-build/VST3/Release/again-sample-accurate.vst3 ~/.vst3/
cp -a /tmp/vst3sdk-build/VST3/Release/mda-vst3.vst3 ~/.vst3/
```

Confirm `~/.vst3/mda-vst3.vst3/Contents/x86_64-linux/mda-vst3.so` exists (an
empty bundle shell without the `.so` will scan as missing plugins).

## Linux Editor (Unity audio)

Recommended workflow for **1.2.0**:

1. **Build** `VstHostNative.so` on WSL2 (`./native~/linux-vst-host/Build.sh --Install`).
2. Run native **smoke** on WSL2 (optional but fast).
3. Copy the package (or shared folder) plus `~/.vst3/*.vst3` onto a **full Linux
   desktop** (VirtualBox Ubuntu, dual-boot, etc.). WSL2 alone is a weak Unity
   audio / GPU environment.
4. Open the sample scene in **Unity Linux Editor**.
5. Load **mda DX10** (`Instrument`), Enter Play Mode, **Note On** — expect synth audio.
6. Optional: **Plugin Chain** with Instrument + Effect, **Build Chain**, Note On,
   toggle Bypass.

Verified path for 1.2.0: Ubuntu under VirtualBox, mda DX10 Note On + Plugin Chain.

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

## Standalone Linux64

1. Menu **Window → VST3 Host → Verify Plugin Platforms** (includes Linux `.so` flags).
2. Menu **Window → VST3 Host → Build Standalone Linux64 (Verify)** (IL2CPP).
3. Confirm the player contains `VstHostNative.so`.

Prefer a full Linux desktop (VirtualBox / dual-boot) over WSL2 for Player and
Editor audio checks. Plugin platform flags alone do not replace the manual
Linux Editor steps above.

When linking the repo via `file:` / Git, `native~/**/build*` outputs may appear — use **Window → VST3 Host → Sanitize Extra Native Plugins**.

## MIDI-only build must not contain VST

```powershell
.\native~\windows-vst-host\Verify-MidiIsolation.ps1 -MidiRepoRoot "<Unity-MIDI-Plugin>"
```

```bash
./native~/macos-vst-host/Verify-MidiIsolation.sh "<Unity-MIDI-Plugin>"
```

Fails if `VstHostNative` (`.dll` / `.bundle` / `.dylib` / `.so`), VST3 SDK trees, or VST Runtime scripts appear under the MIDI repo `Assets` / `native` / `Packages` (markdown docs are allowed).
