# VST3 Host Sample

Editor / Standalone sample for **Unity Plugin Host for VST3** (Windows + macOS + Linux).

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
  - Linux: `native~/linux-vst-host/Build.sh --Install`

Unity MIDI Plugin is **optional**. The sample runs VST-only with manual notes.

## Play mode — left panel (`VstHostSampleController`)

1. Enter Play Mode.
2. The sample scans VST3 folders and tries to load `mda DX10` (or another Instrument).
3. Press **Note On** / **Note Off**, or use a MIDI device when MIDI + VST setup is ready.
4. Use the parameter window to change gain / timbre.

### Plugin Chain

1. Switch the toolbar to **Plugin Chain**.
2. **Top list = Instrument** (synth such as `mda DX10` / NoiseMaker — category should include `Instrument`).
   **Bottom list = Effect** (e.g. `AGain` / Delay — `Fx` / Effect). Swapping these usually yields silence.
3. Press **Build Chain** — status should look like `Chain: [1] … → [2] …`. Audio path switches to `VstPluginChain`.
4. Press **Note On**. Toggle **Bypass effect: ON/OFF** (button under Parallel→Serial / Strict Serial) for dry vs wet.
5. Use **Edit Instrument params** / **Edit Effect params** to switch what **VST Host Parameters** shows.
6. Optional: **Parallel→Serial** vs **Strict Serial** mix mode.

If the Game view is short, scroll is not available — make the Game view taller so the Bypass / Note On row is visible.

## Play mode — right panel (`VstHostSampleFeatureDemos`)

Added automatically next to the controller. Works **without** MIDI Plugin:

| Tab | Demo |
|-----|------|
| Guide | Package overview, EventSink NoteOn/Off |
| Presets | Host `SetProgram`, Capture/Apply/Toggle A/B state |
| Mapping | Simulate CC / Pitch Bend → parameter (optional `VstMidiParameterMapping` asset) |
| Routes | Edit `VstPluginChain` channel→slot routes (multi-timbral helper) |

## Optional packages

| Package | Define (asmdef versionDefines) | What to try |
|---------|--------------------------------|-------------|
| Timeline | `FEATURE_USE_TIMELINE` | `VstParameterTrack` / `VstProgramChangeMarker` — see `Documentation~/timeline.md` |
| Input System | `FEATURE_INPUT_SYSTEM` | `InputSystemToVstBridge` — see `Documentation~/animator-input.md` |
| Visual Scripting | `FEATURE_USE_VISUALSCRIPTING` | VST3 Host units — see `Documentation~/visual-scripting.md` |
| Unity MIDI Plugin | `FEATURE_MIDI_PLUGIN` (Sync menu) | Adapter / SMF / Network / Chunity helpers |

Full verification matrix: [Documentation~/verification.md](../../Documentation~/verification.md).
Chain details: [Documentation~/plugin-chain.md](../../Documentation~/plugin-chain.md).
