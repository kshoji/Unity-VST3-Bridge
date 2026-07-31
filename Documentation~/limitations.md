# Known limitations

Initial scope for **Unity Plugin Host for VST3** (method V1). These are
intentional product boundaries, not temporary bugs.

| Area | Limitation |
|------|------------|
| Plugin formats | **VST3 only**. VST2 and VST4 are out of scope. |
| Host role | Unity is the **host**. This package is not shipped as a DAW-targeted VST. |
| Platforms | **Windows** (Editor + Standalone x64/ARM64) and **macOS** (Editor + Standalone OSXUniversal via `Plugins/macOS/VstHostNative.bundle`). Linux / UWP not implemented. |
| Plugin GUI | Plugin-native editors (`IPlugView` / HWND·NSView embedding) are **not** supported and **not planned**. Use `VstHostParameterPanel` or your own UI on host parameters. See below. |
| MIDI 2.0 | UMP channel voice is **down-converted** to MIDI 1.0. High-resolution / per-note / SysEx are skipped (logged). |
| Commercial plugins | Compatibility with arbitrary commercial `.vst3` plugins is **not guaranteed**. Smoke-tested with free/SDK samples (e.g. AGain, mda DX10). |
| Redistribution | Third-party `.vst3` binaries are **not** bundled. Users install plugins on their own machines. |
| MIDI package | Unity MIDI Plugin does **not** include this host, `VstHostNative` (`.dll` / `.bundle`), or the VST3 SDK. |
| Process model | In-process host only. Separate-process isolation / IPC is out of scope for V1. |
| Audio path | Default: `OnAudioFilterRead` → native `process`. Optional Unity 6.3+ `VstHostGenerator` (`IAudioGenerator`). Native Audio Plugin Mixer path and ASIO/WASAPI bypass are not implemented. |
| Latency / threading | Buffer size and latency follow Unity DSP settings; realtime rules are documented in [audio-path.md](audio-path.md). |

## Plugin-native GUI (not planned)

Hosting a plugin’s own editor requires OS window/view control (`IPlugView`
`attached` to HWND on Windows, NSView on macOS), plus focus, DPI, idle/timers,
and Editor vs Player differences. Windows and macOS need largely separate
implementations, and edge cases (fullscreen, multiple displays, plugin-specific
drawing stacks) tend to produce hard-to-reproduce defects.

Therefore plugin-native GUI is an intentional product boundary with **no
current plan to implement**. Control plugins via host parameters, presets /
state, or a custom Unity UI (`VstHostParameterPanel` as a starting point).

## Trademark reminder

VST® is a registered trademark of Steinberg Media Technologies GmbH.
See [NOTICE.md](../NOTICE.md) for SDK and trademark guidance.
