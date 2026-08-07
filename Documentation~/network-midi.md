# Network MIDI → VST

Remote MIDI (UDP hub/client, or Mirror / Netcode / WSNet2 bridges) injects into a local virtual device. `VstHostMidiAdapter` already listens to Unity MIDI Plugin events — wire the adapter to that device.

Requires: **`FEATURE_MIDI_PLUGIN`** + **`FEATURE_MIDI_NETWORK`**.

## Quick wire

1. Host: `MidiNetworkHub` (+ optional `MidiPlaybackSync` for SMF sync).
2. Peer: `MidiNetworkClient` (default virtual device `network:remote`).
3. Local VST: load instrument, `VstHostAudioFilter` / `VstAudioGraph`, `VstHostMidiAdapter.TargetPluginId`.
4. Add **`VstHostNetworkMidiLink`** (same GameObject or linked refs) → filters the adapter to the client virtual device.

```text
Remote controller / peer
        │  UDP / Mirror / NGO / WSNet2
        ▼
MidiNetworkClient → virtual device "network:remote"
        │
        ▼
VstHostMidiAdapter (filtered) → VST NoteOn / CC…
        │
        ▼
VstHostAudioFilter / VstAudioGraph
```

## Multiplayer jam

- Several peers can inject into the same local adapter (same or different virtual devices).
- Use adapter **Channel Routes** for multi-timbral VST instances.
- SMF sync: host `MidiPlaybackSync` + client following playback time; sound still comes from each peer's local VST (or a designated host renderer).

## Without the link component

Manually call `adapter.SetAllowedDeviceIds("network:remote")` after the client registers the virtual device.
