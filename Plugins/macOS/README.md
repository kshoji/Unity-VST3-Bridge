# Plugins/macOS — VstHostNative.bundle

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

After first import, confirm Inspector matches the table; commit Unity’s
rewritten `.meta` if it differs slightly by Editor version. Windows DLL
`.meta` files under `Plugins/Windows/{x86_64,ARM64}/` follow the same
“commit PluginImporter” rule.

Nested `Contents/*.meta` files may appear after Unity import — commit them if
the Editor creates them (same pattern as Unity MIDI Plugin’s `MIDIPlugin.bundle`).

## Related

- Native sources: `native~/macos-vst-host/`
- Package docs: `Documentation~/usage.md`, `Documentation~/package-excludes.md`
