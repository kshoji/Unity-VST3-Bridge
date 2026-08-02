# Scriptable Audio (Unity 6.3+)

Optional path for DSP-clocked VST3 playback using Unity’s Scriptable Audio Pipeline (`IAudioGenerator`).

Requires:

- Unity **6000.3+** (non-WebGL)
- For MIDI scheduling: Unity MIDI Plugin with **`FEATURE_SCRIPTABLE_AUDIO`** and **`FEATURE_MIDI_PLUGIN`**

## `VstHostGenerator`

Attaches to an `AudioSource` as a Scriptable Generator. Each Process block:

1. Optionally flushes due events from `VstHostDspMidiQueue.Shared`
2. Calls native `VstHost_Process` for the configured plugin id
3. Writes stereo (or mono mix) into the generator buffer

```csharp
var gen = gameObject.AddComponent<VstHostGenerator>();
gen.PluginId = pluginId;
gen.SetupAudioSource();
```

Do not also run `VstHostAudioFilter` / `VstPluginChain` on the same output path for the same instance.

## DSP MIDI queue

`VstHostDspMidiQueue` holds timed MIDI 1.0 short messages (`DspSample` + plugin id). Audio consumers (`VstHostGenerator`, `VstHostAudioFilter`, `VstPluginChain`) flush events with `DspSample <= blockEnd` before `Process`.

Schedule from the main / control thread only via the queue API (or `VstHostDspMidiOutBridge`). `Schedule*` methods return `false` when the queue is full (event dropped). Overflows are counted atomically; audio consumers' `LateUpdate` call `PumpMainThreadDiagnostics` and emit a rate-limited warning on the main thread (no logging from the audio thread).

`Process` failures and generator `frames > bufferCapacity` clips are reported the same way via `VstHostAudioDiagnostics` (also pumped from `LateUpdate`).

## `VstHostDspMidiOutBridge`

Implements MIDI Plugin’s `IMidiDspTimedMidiOutput`. Assign the component to:

- `MidiDspSequenceScheduler.extraTimedMidiOutput`, or
- `MidiDspUmpSequenceScheduler.extraTimedMidiOutput` (notes / MIDI 1.0 voice mirrored as MIDI 1.0)

Wire **Target Plugin Id**, optional `VstParameterTarget`, or `VstPluginChain` for channel → instrument routing.

```text
MidiDspSequenceScheduler
        │  extraTimedMidiOutput
        ▼
VstHostDspMidiOutBridge → VstHostDspMidiQueue
        │
        ▼ (audio thread FlushDue)
VstHostGenerator / VstHostAudioFilter / VstPluginChain → Process
```

## Compared to `OnAudioFilterRead`

| Path | Component | Timing |
|------|-----------|--------|
| Classic | `VstHostAudioFilter` / `VstPluginChain` | MIDI often main-thread; optional DSP queue flush |
| Scriptable Audio | `VstHostGenerator` | Same DSP queue; generator Process aligns with DSP clock |

Use Scriptable Audio when sequences are already scheduled on the DSP clock (`MidiDspSequenceScheduler`).
