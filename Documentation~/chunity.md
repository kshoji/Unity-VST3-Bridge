# Chunity (ChucK) + VST

Hybrid paths between [Chunity](https://chuck.stanford.edu/chunity/) and the VST host.

Requires: **`FEATURE_MIDI_PLUGIN`** + **`FEATURE_CHUNITY`** (Chunity installed + `Chunity.Runtime.asmdef`).

## A. ChucK Event → VST instrument

1. `MidiChuckEventToMidi` fires MIDI to a virtual device (default `virtual:chuck-midi-out`).
2. Load a VST instrument + `VstHostMidiAdapter`.
3. Add **`VstHostChuckEventMidiLink`** to filter the adapter to that device.

```text
ChucK Event → MidiChuckEventToMidi → virtual device
        → VstHostMidiAdapter → VSTi
```

## B. ChucK audio → VST effect

Unity runs `OnAudioFilterRead` in **Inspector component order**. Put Chuck first, VST effect second on the same GameObject / AudioSource.

### Option 1 — single effect filter

1. ChuckMainInstance / ChuckSubInstance on the AudioSource.
2. Add **`VstHostChuckEffectBridge`** (`Audio Filter Effect`), set **Effect Plugin Id**.
3. Bridge enables `VstHostAudioFilter` in Effect mode.

### Option 2 — effect chain with external input

1. Enable **`VstPluginChain.MixExternalInput`** (or use the bridge target **Plugin Chain External Input**).
2. Fill Effect slots (EQ → Delay → Reverb, etc.).
3. Upstream Chuck (or any prior filter) seeds the chain bus.

Incomplete Chunity audio-filter patches may still compile; audio tap requires Chuck's filter callback to write into the shared buffer before the VST stage.

## C. MIDI → ChucK → VST

`MidiChuckBridge` (MIDI → ChucK globals) + path B for ChucK → VST reverb/delay.

## Notes

- Do not place VST scripts under `Assets/MIDI`.
- Chunity runtime is not bundled with either package.
