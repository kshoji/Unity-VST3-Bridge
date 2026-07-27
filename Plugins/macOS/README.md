# Plugins/macOS — VstHostNative.bundle (Phase M)

Empty scaffold for **macOS** native bridge. Binary is built on a Mac
(`native/macos-vst-host/`, not yet present). Windows machines keep this folder
so UPM layout and `.meta` policy stay consistent.

## Target layout

```text
Plugins/macOS/
  README.md                 ← this file
  VstHostNative.bundle/     ← install product (Phase M)
  VstHostNative.bundle.meta ← PluginImporter settings (create with the bundle)
```

`DllImport("VstHostNative")` is unchanged from Windows. Unity loads
`VstHostNative.bundle` on Editor OSX / Standalone OSX.

Prefer a **Universal** binary (arm64 + x86_64) so Editor and player share one
asset.

## .meta policy (when the bundle exists)

Create `VstHostNative.bundle.meta` as a **PluginImporter** (Unity treats
`.bundle` as a plugin folder). Required settings:

| Setting | Value |
|---------|--------|
| Compatible With Any Platform | **No** (`enabled: 0` on Any) |
| Editor | **Yes**, `OS: OSX`, `CPU: AnyCPU` (or matching Universal) |
| Standalone OSXUniversal | **Yes**, `CPU: AnyCPU` |
| Standalone Win / Win64 / Windows ARM64 | **No** (`CPU: None` / exclude) |
| Linux64 / Android / iOS / WebGL / WSA | **No** |

Reference shape (abbreviated; Unity may rewrite on import):

```yaml
PluginImporter:
  platformData:
  - first: { : Any }
    second:
      enabled: 0
      settings:
        Exclude Editor: 0
        Exclude OSXUniversal: 0
        Exclude Win: 1
        Exclude Win64: 1
        Exclude Windows ARM64: 1
        # …exclude other non-OSX platforms…
  - first: { Editor: Editor }
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        OS: OSX
  - first: { Standalone: OSXUniversal }
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
```

Do **not** enable Windows platforms on this asset (mirror how
`Plugins/Windows/.../VstHostNative.dll.meta` excludes OSX).

After first import on macOS Editor, open the Inspector and confirm the table
above; commit the `.meta` Unity writes if it differs slightly by version.

## Git ignore

`.gitignore` in this folder keeps the directory tracked while allowing a
committed `.bundle` once built. Until then only this README / ignore / folder
`.meta` need exist.

## Related

- Plan: `Documentation~/vst3-native-host-plan.md` → **Phase M**
- Windows-only inventory: `Documentation~/macos-portability.md`
- Windows plugins: `Plugins/Windows/`
