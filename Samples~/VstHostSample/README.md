# VST3 Host Sample

Editor / Standalone sample for **Unity Plugin Host for VST3** (Windows + macOS + Linux).

## Import

Package Manager → this package → Samples → **VST3 Host Sample** → Import.

Open `Scenes/VstHostSampleScene`.

## Requirements

- Windows (x64 Editor / Win64 or Windows ARM64 player), **or** macOS (Editor OSX /
  OSXUniversal), **or** Linux (Editor Linux / Standalone Linux64)
- At least one `.vst3` under the OS-standard VST3 folders
  - Windows: e.g. `C:\Program Files\Common Files\VST3`
  - macOS: e.g. `~/Library/Audio/Plug-Ins/VST3`
  - Linux: e.g. `~/.vst3` (prefer an **Instrument** such as SDK **mda DX10** for Note On)
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

### Audio Graph (multi-plugin)

1. Switch the toolbar to **Audio Graph**.
2. Pick a demo row: **Parallel→Serial** / **Send/Return** / **Sidechain**.
3. **Top list = Instrument** (synth such as `mda DX10` — category should include `Instrument`).
   **Bottom list = Effect** (e.g. `AGain` / Delay — `Fx` / Effect). Swapping these usually yields silence.
4. Press **Build Graph**.
5. Press **Note On**. Toggle **Bypass effect: ON/OFF** for dry vs wet.
   The sample sets effect **Gain≈0.25** after load (AGain default≈unity makes Bypass inaudible otherwise). Bypass ON should sound louder/fuller than OFF.

| Demo | What to check |
|------|----------------|
| Parallel→Serial | Instrument → mix → effect → out (`BuildParallel…`). Optional **Mix ExternalIn**. Bypass effect for dry. |
| Send/Return | Dry path + send to effect → mix. Move **Send gain**; wet should rise with gain. AGain is fine if no reverb. |
| Sidechain | Prefer effect named **AGain SideChain**. Single instrument feeds main + Aux via `BuildSidechainFromSingleSource`. |

DSP MIDI queue flush is enabled on `VstAudioGraph`. Channel routes: right panel **Routes** tab (node id).

If the Game view is short, make it taller so Bypass / Note On remain visible.

## Play mode — right panel (`VstHostSampleFeatureDemos`)

Added automatically next to the controller. Works **without** MIDI Plugin:

| Tab | Demo |
|-----|------|
| Guide | Package overview, EventSink NoteOn/Off, Graph path hints |
| Presets | Host `SetProgram`, Capture/Apply/Toggle A/B state |
| Mapping | Simulate CC / Pitch Bend → parameter (optional `VstMidiParameterMapping` asset) |
| Routes | Graph instrument node routes (channel → node id) |

## Optional packages

| Package | Define (asmdef versionDefines) | What to try |
|---------|--------------------------------|-------------|
| Timeline | `FEATURE_USE_TIMELINE` | `VstParameterTrack` / `VstProgramChangeMarker` — see `Documentation~/timeline.md` |
| Input System | `FEATURE_INPUT_SYSTEM` | `InputSystemToVstBridge` — see `Documentation~/animator-input.md` |
| Visual Scripting | `FEATURE_USE_VISUALSCRIPTING` | VST3 Host units — see `Documentation~/visual-scripting.md` |
| Unity MIDI Plugin | `FEATURE_MIDI_PLUGIN` (Sync menu) | Adapter / SMF / Network / Chunity helpers |

Full verification matrix: [Documentation~/verification.md](../../Documentation~/verification.md).  
Graph: [Documentation~/audio-graph.md](../../Documentation~/audio-graph.md).
