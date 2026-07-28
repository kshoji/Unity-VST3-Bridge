# Changelog

## [Unreleased]

### Fixed
- Commit Windows `VstHostNative.dll.meta` (x86_64 + ARM64) and stop ignoring
  them in `Plugins/Windows/**/.gitignore` (macOS already tracked `.meta`).
- `VstHostBuildVerify` now requires Windows ARM64 to be explicitly enabled and
  rejects cross-OS platform flags on each plugin binary.
- Sample README / plan “現状” updated for Windows + macOS (no longer Windows-only).

### Added
- **Phase M (macOS):** `native~/macos-vst-host/` (CMake + `Build.sh`), Universal
  `Plugins/macOS/VstHostNative.bundle`, macOS scan paths, Editor OSX verify /
  isolation helpers, documentation updates.
- Renamed `native/` → **`native~/`** so Git URL / `file:` installs do not import
  SDK sources (`.npmignore` alone does not apply). See `Documentation~/package-excludes.md`.
- Initial package skeleton (Runtime, Editor, Tests, Samples~, Documentation~, Plugins).
- Phase 8 docs: `NOTICE.md` SDK/trademark details, `Documentation~/usage.md`,
  `Documentation~/limitations.md`; plan canonical at `Documentation~/vst3-native-host-plan.md`.
- Phase M prep: macOS placement in plan, `Plugins/macOS/` scaffold, `Documentation~/macos-portability.md`.
