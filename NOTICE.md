# NOTICE — Unity Plugin Host for VST3

A Unity package for hosting VST3 instruments and effects through a native bridge, with optional MIDI plugin integration.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.

## SDK and Third-Party Components

- The VST3 SDK must be obtained, stored, and referenced only from the dedicated VST host repository/native build area.
- This Unity package must not redistribute third-party commercial `.vst3` plugins.
- Additional license and attribution text for bundled or build-time dependencies must be added before release.

## Distribution Boundary

- Do not ship the VST3 SDK inside the Unity MIDI package.
- Do not place VST host implementation files under `Assets/MIDI`.
- Keep VST host code, native binaries, and documentation in this package/repository.
