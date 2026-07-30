# VST3 Host Sample

Editor / Standalone sample for **Unity Plugin Host for VST3** (Windows + macOS).

## Import

Package Manager → this package → Samples → **VST3 Host Sample** → Import.

Open `Scenes/VstHostSampleScene`.

## Requirements

- Windows (x64 Editor / Win64 or Windows ARM64 player) **or** macOS (Editor OSX / OSXUniversal)
- At least one `.vst3` under the OS-standard VST3 folders
  - Windows: e.g. `C:\Program Files\Common Files\VST3`
  - macOS: e.g. `~/Library/Audio/Plug-Ins/VST3`
- Native bridge already under `Plugins/` (rebuild if needed):
  - Windows: `native~/windows-vst-host/Build.ps1 -Install`
  - macOS: `native~/macos-vst-host/Build.sh --Install`

## Play mode

1. Enter Play Mode.
2. The sample scans VST3 folders and tries to load `mda DX10` (or another Instrument).
3. Press **Note On** / **Note Off**, or use a MIDI device when MIDI + VST setup is ready.
4. Use the parameter window to change gain / timbre.

### Plugin Chain

1. Switch the toolbar to **Plugin Chain**.
2. Pick an **Instrument** in the first list and an **Effect** in the second (defaults try `AGain` / `Delay`).
3. Press **Build Chain** — audio path switches from `VstHostAudioFilter` to `VstPluginChain` (`Instrument → Effect`).
4. Press **Note On** and toggle **Bypass effect** to A/B dry vs wet.
5. Use **Edit Instrument params** / **Edit Effect params** to switch what **VST Host Parameters** shows (one plugin id at a time).
6. Optional: **Parallel→Serial** vs **Strict Serial** mix mode.

Full verification matrix: [Documentation~/verification.md](../../Documentation~/verification.md).
Chain details: [Documentation~/plugin-chain.md](../../Documentation~/plugin-chain.md).
