# Plugin chain

`VstPluginChain` renders multiple VST3 instances on one `AudioSource` via `OnAudioFilterRead`.

Disable any `VstHostAudioFilter` on the same GameObject when using a chain.

## Roles

| Role | Behavior |
|------|----------|
| Instrument | Generates audio (null input to `Process`) |
| Effect | Processes the current mix (stereo in → out) |

## Mix modes

- **ParallelInstrumentsThenSerialEffects** (default): sum all instruments, then run effects in list order.
- **StrictSerial**: process slots in list order (instruments add into the bus; effects replace the bus).

## Setup

1. `VstHostManager.InitializeFromAudioSettings()` and `CreateInstance` for each plugin.
2. Add `VstPluginChain` + `AudioSource` (silent loop is created automatically).
3. Fill **Slots** with plugin ids, roles, and optional gain / bypass.
4. Optional **Channel Routes**: MIDI channel → slot index (instrument). Used by `VstHostDspMidiOutBridge` and `ResolveInstrumentPluginId`.

```text
Instruments (parallel) ──► mix ──► Effect1 ──► Effect2 ──► output
```

## DSP-timed MIDI

When **Flush Dsp Midi Queue** is enabled (default), the chain drains `VstHostDspMidiQueue.Shared` before processing each block. Pair with `VstHostDspMidiOutBridge` + `MidiDspSequenceScheduler.extraTimedMidiOutput` for sample-accurate notes (Unity 6.3+ Scriptable Audio). See [scriptable-audio.md](scriptable-audio.md).

## Channel routing vs adapter

- Runtime MIDI from `VstHostMidiAdapter` **Channel Routes** still targets plugin ids directly (adapter → `SendMidi1`).
- Chain **Channel Routes** only affect helpers that call `ResolveInstrumentPluginId` (DSP bridge / tooling). Keep both consistent if you use both paths.

## Sample

`Samples~/VstHostSample` → **Plugin Chain** mode builds Instrument + Effect slots, with effect bypass for dry/wet A/B.
