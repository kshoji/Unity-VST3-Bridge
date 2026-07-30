# MIDI optional integration

`Unity Plugin Host for VST3` works without Unity MIDI Plugin.

## Without MIDI Plugin

```csharp
var host = VstHostManager.Instance;
host.Initialize(AudioSettings.outputSampleRate, 512);
int id = host.CreateInstance(@"C:\Program Files\Common Files\VST3\again.vst3");
host.NoteOn(id, channel: 0, note: 60, velocity: 100);
// Audio: VstHostAudioFilter (OnAudioFilterRead → Process)
host.NoteOff(id, channel: 0, note: 60);
```

See also [audio-path.md](audio-path.md).

## With MIDI Plugin

1. Add Unity MIDI Plugin to the same project (`Assets/MIDI` or equivalent).
2. Open `Window/VST3 Host/Sync MIDI Plugin Define` (or wait for auto-sync) so `FEATURE_MIDI_PLUGIN` is set.
3. Add component `VstHostMidiAdapter` to a GameObject.
4. After `CreateInstance`, set `TargetPluginId` on the adapter.
5. Ensure `MidiManager` is initialized and the adapter is registered (default: OnEnable).

MIDI 2.0 channel voice is down-converted to MIDI 1.0. SysEx and per-note messages are skipped (logged once).

Do not put VST routing into MIDI core `midi2Plugins`; keep routing in this package.

Also see [usage.md](usage.md) for scan paths and install overview.

## CC mapping, SMF playback, and presets

### CC → VST3 parameter mapping

1. Create a **VST3 Host / MIDI Parameter Mapping** asset (`VstMidiParameterMapping`).
2. Add `VstHostMidiParameterMapper`, assign the asset and `TargetPluginId`.
3. Optional MIDI Learn:
   - Move a slider on `VstHostParameterPanel` (marks the last touched parameter).
   - Enable **Midi Learn** on the mapper, then send a CC or pitch bend from your controller.
4. For high resolution, use **FourteenBitControlChange** bindings (MSB + LSB).
5. If the same CC should not also go to the plugin as MIDI CC, disable **Forward Control Change** on `VstHostMidiAdapter`.

### SMF player → VST3 instrument

`SmfPlayer` sends to `outputDeviceId`. Virtual devices inject into the MIDI event pipeline, which `VstHostMidiAdapter` receives.

1. Load a VST instrument, set `VstHostMidiAdapter.TargetPluginId`, attach `VstHostAudioFilter`.
2. Add `VstHostSmfLink` (same GameObject or linked references).
3. Assign `SmfPlayer` + adapter. On Awake, the link registers virtual device `vst3:smf`, sets `SmfPlayer.outputDeviceId`, and filters the adapter to that device.
4. Play the SMF.

#### Multi-timbral channel routing

On `VstHostMidiAdapter`, fill **Channel Routes** (`channel` → `pluginId`). Unlisted channels use `TargetPluginId`. Attach one `VstHostAudioFilter` per loaded instance (or mix externally).

```text
SmfPlayer.outputDeviceId = "vst3:smf"
        │
        ▼
Virtual device inject → VstHostMidiAdapter
        │
        ├─ ch0 → plugin A
        ├─ ch1 → plugin B
        └─ other → TargetPluginId
```

### Program Change → host programs

Enable **Map Program Change To Host Program** on the adapter to call `VstHostManager.SetProgram` when MIDI Program Change arrives (in addition to, or instead of, forwarding MIDI PC via **Forward Program Change**).

Preset assets and the editor browser are documented in [parameters.md](parameters.md).
