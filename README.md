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

This repository currently contains the package skeleton and planning documents.
Native hosting implementation and Unity-side bridge code are added in later phases.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.
