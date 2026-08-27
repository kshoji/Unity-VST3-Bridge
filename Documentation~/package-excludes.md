# Why `native~/` (trailing tilde)

Unity’s AssetDatabase **ignores** folders whose names end with `~` (same rule as
`Documentation~` / `Samples~`).

| Install method | Does `.npmignore` apply? | How `native` sources stay out of import |
|----------------|--------------------------|-----------------------------------------|
| npm / scoped registry publish | Yes | `.npmignore` excludes `native~/` |
| **Git URL** | **No** | Folder must be named **`native~`** |
| **`file:` local path** | **No** | Same — **`native~`** |

If the folder is named plain `native/`, Unity generates thousands of `.meta`
files (SDK sources, headers, samples) and may treat build outputs as plugins.

Do **not** rename back to `native/` without an alternate exclusion strategy.

## VST3 SDK is not a git submodule

`native~/windows-vst-host/vst3sdk` is a **developer-only** local clone (see
`Fetch-Vst3Sdk.ps1` / `Fetch-Vst3Sdk.sh`). It is listed in `.gitignore`.

Unity Package Manager always passes `--recurse-submodules` on Git URL installs.
Steinberg’s nested SDK trees include very long paths; under
`Library/PackageCache/.tmp-…/clone/…` those exceed Windows’ classic path limit
and fail package resolve. Keeping the SDK out of `.gitmodules` avoids that for
consumers who only need prebuilt `Plugins/`.

