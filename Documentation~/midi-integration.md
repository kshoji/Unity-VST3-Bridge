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
