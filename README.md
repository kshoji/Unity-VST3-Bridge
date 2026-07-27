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

Phases 0–6 are implemented on branch `feature/vst3-native-host-20260727`:
native host, MIDI, audio return, parameters / state / simple IMGUI panel.
Plugin-native GUI is not hosted.

## Audio (Path B)

1. `VstHostManager.Instance.InitializeFromAudioSettings()`
2. `CreateInstance(...)` → assign id to `VstHostAudioFilter.PluginId`
3. Mode: `Instrument` (VSTi) or `Effect`
4. Ensure an `AudioSource` is playing (component auto-creates a silent loop clip)

```csharp
var host = VstHostManager.Instance;
host.InitializeFromAudioSettings();
int id = host.CreateInstance(path, uid);
var filter = GetComponent<VstHostAudioFilter>();
filter.PluginId = id;
filter.Mode = VstHostAudioFilter.ProcessMode.Instrument;
host.NoteOn(id, 0, 60, 100);
```

## Optional MIDI Plugin integration

This package does **not** depend on Unity MIDI Plugin. When both are in the same
project, Editor auto-syncs the `FEATURE_MIDI_PLUGIN` define
(`Window/VST3 Host/Sync MIDI Plugin Define`). That enables assembly
`jp.kshoji.unity.vst3nativehost.Midi` and component `VstHostMidiAdapter`.

Without MIDI Plugin, use manual APIs on `VstHostManager`:

```csharp
VstHostManager.Instance.InitializeFromAudioSettings();
var id = VstHostManager.Instance.CreateInstance(path, uid);
var filter = gameObject.AddComponent<VstHostAudioFilter>();
filter.PluginId = id;
filter.Mode = VstHostAudioFilter.ProcessMode.Instrument;
VstHostManager.Instance.NoteOn(id, channel: 0, note: 60, velocity: 100);
```

Audio returns via Path B: native `process` called from `OnAudioFilterRead`.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.
