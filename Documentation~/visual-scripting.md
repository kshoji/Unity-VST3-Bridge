# Visual Scripting

Optional nodes under category **VST3 Host** (requires `FEATURE_USE_VISUALSCRIPTING`).

## Setup

1. Install **Visual Scripting** (`com.unity.visualscripting`).
2. Ensure scripting define **`FEATURE_USE_VISUALSCRIPTING`**.
3. Menu **Window → VST3 Host → Visual Scripting → Register Nodes** (adds the assembly to the Node Library and regenerates).
4. For event units, add **`VstVisualScriptingBridge`** on the same GameObject as the Script Machine.

## Action units

| Unit | Role |
|------|------|
| VST Initialize Host | `InitializeFromAudioSettings` |
| VST Load Plugin | `CreateInstance` → plugin id |
| VST Unload Plugin | `DestroyInstance` |
| VST Note On / Note Off | Manual MIDI to instance |
| VST Set Parameter | Normalized parameter |
| VST Set Program | Host program index |
| VST Get State / Set State | Base64 plugin state |

## Event units

| Unit | Requires |
|------|----------|
| On VST Parameter Changed | `VstVisualScriptingBridge` (parameter activity) |
| On VST Host Activity | Bridge with **Forward All Activity** enabled |

Combine with MIDI Visual Scripting nodes (`Send MIDI Note On`, etc.) and `VstHostMidiAdapter` when using Unity MIDI Plugin.
