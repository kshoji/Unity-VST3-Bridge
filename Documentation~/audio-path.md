# Audio path (B)

Unity audio thread → `VstHostAudioFilter.OnAudioFilterRead` → `VstHost_Process` → VST3 `IAudioProcessor::process`.

## Setup

1. Call `VstHostManager.InitializeFromAudioSettings()` so sample rate / max block size match DSP settings (`numFrames ≤ BlockSize`).
2. Load a plugin with `CreateInstance`.
3. Add `VstHostAudioFilter` + `AudioSource` (silent looping clip is created automatically).
4. Set `PluginId` and `Mode` (`Instrument` or `Effect`).
5. Send MIDI via `NoteOn` / `VstHostMidiAdapter`.

## Real-time rules

- Do not call Unity APIs from `OnAudioFilterRead`.
- MIDI arrives on the audio thread through the native lock-free queue only.
- Prefer `InitializeFromAudioSettings` before creating instances.

## Instrument vs effect

| Mode | Input | Output |
|------|-------|--------|
| Instrument | silence | VST out replaces filter buffer |
| Effect | Unity filter input | processed stereo written back |
