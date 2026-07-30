# Editor tools

Menus under **Window → VST3 Host**:

| Window | Purpose |
|--------|---------|
| Plugin Browser | Scan default / extra folders, filter by name & category, Load Selected + note preview (`VstHostAudioFilter` is created automatically in Play Mode for audible preview) |
| Preset Browser | Host programs + project `VstPresetAsset` apply/capture |
| Activity Monitor | Live log of host MIDI / SetParameter / SetProgram |
| Virtual Controller | Play Mode keyboard + CC → loaded plugin (`VstHostManager`). Pick a loaded instance (e.g. after Plugin Browser Load Selected). Holds notes while keys are pressed. |
| Sync MIDI Plugin Define | Enable `FEATURE_MIDI_PLUGIN` when `jp.kshoji.midi` is present |

Project Settings → **VST3 Host**:

- Auto initialize on Play Mode
- Preferred plugin name filter
- Extra scan folders (used by Plugin Browser “Scan Extra Folders”)

## MIDI Monitor

Device-level MIDI I/O remains in **Window → MIDI → Monitor** (Unity MIDI Plugin).
Use **VST Activity** for host-side parameter and SendMidi1 traffic.
Virtual MIDI Controller (**Window → MIDI → Virtual Controller**) still works when
routing through `VstHostMidiAdapter`; the VST Virtual Controller talks to the host API directly.
