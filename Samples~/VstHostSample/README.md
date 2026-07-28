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
3. Press **Note On** / **Note Off**, or use a MIDI device when Verification B is set up.
4. Use the parameter window to change gain / timbre.

Full verification matrix: `Documentation~/verification.md`.
