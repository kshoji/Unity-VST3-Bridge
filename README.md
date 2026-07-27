# Unity Plugin Host for VST3

A Unity package for hosting VST3 instruments and effects through a native bridge, with optional MIDI plugin integration.

## Install from Git URL

Add this package via Unity Package Manager > Add package from Git URL:

```
https://github.com/kshoji/Unity-VST3-Bridge.git
```

No `?path=` suffix is needed — `package.json` is at the repository root.

## Package Layout

- `Runtime/` — runtime C# code
- `Editor/` — editor-only code
- `Plugins/Windows/x86_64/` — native bridge DLL output
- `Samples~/` — importable samples
- `Documentation~/` — package documentation
- `Tests/` — optional package tests
- `native/` — (future) native C++ source for `VstHostNative.dll`

## Status

Phases 0–4 are implemented on branch `feature/vst3-native-host-20260727`:
native scan/load, C# manager, lock-free MIDI queue, optional MIDI Plugin adapter.

## Optional MIDI Plugin integration

This package does **not** depend on Unity MIDI Plugin. When both are in the same
project, Editor auto-syncs the `FEATURE_MIDI_PLUGIN` define
(`Window/VST3 Host/Sync MIDI Plugin Define`). That enables assembly
`jp.kshoji.unity.vst3nativehost.Midi` and component `VstHostMidiAdapter`.

Without MIDI Plugin, use manual APIs on `VstHostManager`:

```csharp
VstHostManager.Instance.Initialize();
var id = VstHostManager.Instance.CreateInstance(path, uid);
VstHostManager.Instance.NoteOn(id, channel: 0, note: 60, velocity: 100);
```

Audio return to Unity (`OnAudioFilterRead`) is Phase 5.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.
