# Animator and Input System

## Animator ↔ VST parameters

No changes to MIDI `MidiAnimatorMapping`. Use VST-side bindings (keeps this package usable without MIDI):

1. Add `VstParameterTarget` + `VstAnimatorDriver`.
2. Assign Animator and either inline bindings or a **VST3 Host / Animator Mapping** asset.
3. Direction:
   - **VstToAnimator** — poll `GetParameterNormalized` → `Animator.SetFloat`
   - **AnimatorToVst** — read Animator float → `SetParameterNormalized`

Optional invert, response curve, and smoothing time per binding.

## Input System → VST

Requires the **Input System** package. Assembly
`jp.kshoji.unity.vst3nativehost.InputSystem` enables via `versionDefines` →
**`FEATURE_INPUT_SYSTEM`** (same symbol name as Unity MIDI Plugin Input System integration; assembly-scoped).

### Direct bridge

`InputSystemToVstBridge` maps Input Actions to:

| Message type | Behaviour |
|--------------|-----------|
| NoteOnOff | performed → NoteOn (velocity from action or fixed), canceled → NoteOff |
| ParameterNormalized | analog 0–1 → SetParameterNormalized |
| ControlChange / PitchBend / ProgramChange | via `VstHostManager` MIDI helpers |

Assign `VstParameterTarget` and an `InputActionAsset`.

### Via MIDI Plugin

1. Use MIDI `InputSystemToMidiBridge` to a virtual output device.
2. Filter `VstHostMidiAdapter` (or `VstHostSmfLink`-style device allow-list) to that device.
3. Optionally map CC with `VstHostMidiParameterMapper`.

Use the direct bridge when MIDI Plugin is not present; use the MIDI path when you
already route game input through MidiManager.

### UnityEvent sink

`VstHostEventSink` exposes `NoteOn` / `NoteOff` / `SetParameter` / `SetProgram` / CC / PitchBend
for Inspector UnityEvents (including `MidiInputRouter` bindings).
