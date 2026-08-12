# Editor tools

Menus under **Window → VST3 Host**:

| Window | Purpose |
|--------|---------|
| Plugin Browser | Scan default / extra folders, filter by name, **category**, and **vendor/tag**, Load Selected + note preview (`VstHostAudioFilter` is created automatically in Play Mode for audible preview) |
| Preset Browser | Host programs + project `VstPresetAsset` apply/capture |
| Activity Monitor | Live log of host MIDI / SetParameter / SetProgram (VST-side equivalent of a “MIDI Monitor VST column”) |
| Audio Graph | Read-only topology for the selected (or scene) `VstAudioGraph`: nodes, edges (Main / Sidechain / Send), ChannelRoutes, arm status |
| Virtual Controller | Play Mode keyboard + CC → loaded plugin (`VstHostManager`). Pick a loaded instance (e.g. after Plugin Browser Load Selected). Holds notes while keys are pressed. |
| Sync MIDI Plugin Define | Enable `FEATURE_MIDI_PLUGIN` when `jp.kshoji.midi` is present |

Project Settings → **VST3 Host**:

- Auto initialize on Play Mode
- Preferred plugin name filter
- Extra scan folders (used by Plugin Browser “Scan Extra Folders”)

## MIDI Monitor vs Activity Monitor

Device-level MIDI I/O remains in **Window → MIDI → Monitor** (Unity MIDI Plugin).
Host-side traffic (what the VST actually received / which parameters changed) is shown in
**Window → VST3 Host → Activity Monitor**. MIDI sends from `SendMidi1` are queued off-thread and
delivered on the main thread via `VstHostActivity.PumpMainThread` (device callbacks never call
`Debug.Log` / `Time.*` directly).

Virtual MIDI Controller (**Window → MIDI → Virtual Controller**) still works when
routing through `VstHostMidiAdapter`; the VST Virtual Controller talks to the host API directly
and does not require the MIDI package.
