# NOTICE — Unity Plugin Host for VST3

This package hosts VST3 instruments and effects through a native bridge
(`VstHostNative` — Windows `.dll` / macOS `.bundle` / Linux `.so`), with optional integration
to Unity MIDI Plugin.

## Package license

This repository’s own source (C# package code and native bridge sources
authored for this project) is under the MIT License. See `LICENSE`.

## Trademark

VST® is a registered trademark of Steinberg Media Technologies GmbH.

If you refer to the VST trademark or use the VST Compatible Logo in product
materials, follow Steinberg’s [VST3 Usage Guidelines](https://github.com/steinbergmedia/vst3sdk/blob/master/VST3_Usage_Guidelines.pdf)
(`native~/windows-vst-host/vst3sdk/VST3_Usage_Guidelines.pdf` when the SDK
submodule is present).

## VST3 SDK (build-time dependency)

### Location and acquisition

| Item | Detail |
|------|--------|
| Upstream | [steinbergmedia/vst3sdk](https://github.com/steinbergmedia/vst3sdk) |
| In this repo | Git submodule at `native~/windows-vst-host/vst3sdk/` |
| Required submodules | `pluginterfaces`, `base`, `public.sdk`, `cmake` |
| Not required for this host | `vstgui4`, `doc`, `tutorials` |

```powershell
# After cloning Unity-VST3-Bridge
git submodule update --init --recursive -- native~/windows-vst-host/vst3sdk
# Windows:
cd native~/windows-vst-host
.\Build.ps1 -Install
```

```bash
# macOS (same SDK submodule path):
cd native~/macos-vst-host
./Build.sh --Install
```

```bash
# Linux (same SDK submodule path; WSL2 OK for build):
cd native~/linux-vst-host
./Build.sh --Install
```

UPM / registry packages **do not** include `native~/` (see `.npmignore`).
Git URL and `file:` installs also skip AssetDatabase import because the folder
name ends with `~` (see [Documentation~/package-excludes.md](Documentation~/package-excludes.md)).
End users who only consume the prebuilt `Plugins/.../VstHostNative`
(`.dll` / `.bundle` / `.so`) do not need a local SDK checkout. Rebuilders and
contributors do.

### SDK license (summary)

The VST3 SDK used here is distributed by Steinberg under the **MIT License**
(copyright Steinberg Media Technologies GmbH). Full text:

[native~/windows-vst-host/vst3sdk/LICENSE.txt](native~/windows-vst-host/vst3sdk/LICENSE.txt)

(same MIT terms under `base/`, `pluginterfaces/`, `public.sdk/`, `cmake/`).

Official license notes: [steinberg.net/sdklicenses_vst3](https://www.steinberg.net/sdklicenses_vst3).

When redistributing this package or a product that embeds code derived from
the SDK, retain the MIT copyright notice and permission text as required by
that license.

### What is / is not redistributed

| Redistributed with this UPM package | Not redistributed |
|-------------------------------------|-------------------|
| Prebuilt `VstHostNative.dll` (Windows) | Full VST3 SDK source tree (`native~/`) |
| Prebuilt `VstHostNative.bundle` (macOS Universal) | Third-party commercial `.vst3` plugins |
| Prebuilt `VstHostNative.so` (Linux x86_64) | Sample `.vst3` binaries used only on developer machines |
| Runtime / Editor C# | |
| Documentation under `Documentation~/` | |

## Distribution boundary (MIDI)

- Do **not** ship the VST3 SDK, `VstHostNative` (`.dll` / `.bundle` / `.so`), or VST host C# inside
  Unity MIDI Plugin releases.
- Do **not** place VST host implementation under `Assets/MIDI`.
- Keep VST host code, native binaries, trademarks notices, and SDK
  attribution in **this** package/repository.
- Unity MIDI Plugin may link here from docs only; implementation stays here.

## Third-party `.vst3` plugins

This package scans and loads plugins from the **user’s machine** (standard
VST3 folders or a path you pass to `Scan`). It does **not** bundle or
redistribute third-party or commercial `.vst3` files. Compatibility with
any given commercial plugin is **not guaranteed**.
