# VST3 Host Sample

Windows x64 Editor / Standalone sample for **Unity Plugin Host for VST3**.

## Import

Package Manager → this package → Samples → **VST3 Host Sample** → Import.

Open `Scenes/VstHostSampleScene`.

## Requirements

- Windows x64
- At least one `.vst3` under the standard VST3 folders (e.g. `C:\Program Files\Common Files\VST3`)
- Built `Plugins/Windows/{x86_64,ARM64}/VstHostNative.dll` (`native~/windows-vst-host/Build.ps1 -Install`)

## Play mode

1. Enter Play Mode.
2. The sample scans VST3 folders and tries to load `mda DX10` (or another Instrument).
3. Press **Note On** / **Note Off**, or use a MIDI device when Verification B is set up.
4. Use the parameter window to change gain / timbre.

Full verification matrix: `Documentation~/verification.md`.
