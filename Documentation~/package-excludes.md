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
