# Plugins/macOS — VstHostNative.bundle (Phase M)

macOS native bridge for Unity `DllImport("VstHostNative")`.

## Layout

```text
Plugins/macOS/
  README.md
  VstHostNative.bundle/          ← Universal (arm64 + x86_64)
  VstHostNative.bundle.meta      ← PluginImporter (Editor OSX + OSXUniversal)
```

Build / refresh:

```bash
cd native~/macos-vst-host
./Build.sh --Install
```

## .meta policy

`VstHostNative.bundle.meta` is a **PluginImporter** (`folderAsset: yes`).

| Setting | Value |
|---------|--------|
| Compatible With Any Platform | **No** |
| Editor | **Yes**, `OS: OSX`, `CPU: AnyCPU` |
| Standalone OSXUniversal | **Yes**, `CPU: AnyCPU` |
| Standalone Win / Win64 / Windows ARM64 / Linux / Android / iOS | **No** |

After first import on macOS Editor, confirm Inspector matches the table; commit
Unity’s rewritten `.meta` if it differs slightly by Editor version.

Nested `Contents/*.meta` files may appear after Unity import — commit them if
the Editor creates them (same pattern as Unity MIDI Plugin’s `MIDIPlugin.bundle`).

## Related

- Plan: `Documentation~/vst3-native-host-plan.md` → **Phase M**
- Portability inventory: `Documentation~/macos-portability.md`
- Native sources: `native~/macos-vst-host/`
