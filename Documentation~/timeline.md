# Timeline automation

Requires the **Timeline** package (`com.unity.timeline`). The optional assembly
`jp.kshoji.unity.vst3nativehost.Timeline` enables automatically via asmdef
`versionDefines`.

## Parameter track

1. Add `VstParameterTarget` next to `VstHostAudioFilter` (or assign the filter reference).
2. Set the target plugin id (or rely on the audio filter’s id).
3. In a Timeline, add track **Vst Parameter Track** and bind it to the target.
4. Add clips; set `parameterId` and the normalized `curve` (X = clip time 0–1, Y = value 0–1).

Overlapping clips blend by Timeline weight.

Place a MIDI Plugin `MidiPlaybackTrack` on the same Timeline to sync SMF playback
with parameter automation.

## Program change markers

1. Add `VstTimelineNotificationReceiver` on the PlayableDirector GameObject.
2. Assign a default `VstParameterTarget`.
3. Add **Vst Program Change Marker** on a track; set `programIndex`.

Alternatively, with Unity MIDI Plugin: `MidiMarker` / `MidiSignalEmitter` Program Change
→ virtual device → `VstHostMidiAdapter` (enable **Map Program Change To Host Program**
when you want `SetProgram`).

## See also

- [midi-integration.md](midi-integration.md) — MIDI adapter / SMF
- [parameters.md](parameters.md) — parameters / presets
