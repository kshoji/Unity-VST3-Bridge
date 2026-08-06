# Plugins/Linux/x86_64 — VstHostNative.so

Linux x86_64 native bridge for Unity `DllImport("VstHostNative")`.

## Layout

```text
Plugins/Linux/x86_64/
  README.md
  VstHostNative.so          ← shared library
  VstHostNative.so.meta     ← PluginImporter (Editor Linux + Standalone Linux64)
```

Build / refresh:

```bash
cd native~/linux-vst-host
./Build.sh --Install
```

## .meta policy

`VstHostNative.so.meta` is a **PluginImporter**.

| Setting | Value |
|---------|--------|
| Compatible With Any Platform | **No** |
| Editor | **Yes**, `OS: Linux`, `CPU: x86_64` |
| Standalone Linux64 | **Yes**, `CPU: x86_64` |
| Standalone Win / Win64 / Windows ARM64 / OSX / Android / iOS | **No** |

After first import, confirm Inspector matches the table; commit Unity’s
rewritten `.meta` if it differs slightly by Editor version.

## Related

- Native sources: `native~/linux-vst-host/`
- Package docs: [Documentation~/usage.md](../../../Documentation~/usage.md),
  [Documentation~/verification.md](../../../Documentation~/verification.md)
  (Linux Editor / WSL2 workflow)
