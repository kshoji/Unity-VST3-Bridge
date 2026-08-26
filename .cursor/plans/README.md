# Cursor plans (dev-only)

Active / in-progress development plans for Plan mode and Agent handoff.

- **Not** part of Asset Store / product documentation.
- Do **not** link here from `Assets/MIDI/documents/` or package READMEs.
- Completed archives → move to [`docs/dev/`](../../docs/dev/).

## Workflow

1. Plan mode → **copy** [`_template.md`](_template.md) to a new `{topic}-plan.md` in this folder (do not invent a layout from scratch).
2. Implement against the template’s **External contract** section only when touching release docs / READMEs / comments.
3. Keep phase labels inside the plan file only (see `.cursor/rules/dev-plans-vs-release-docs.mdc`).
4. When done, archive under `docs/dev/` (not under `Assets/MIDI/documents/`) and optionally leave a one-line note here.
5. Never leave **Moved:** stubs under `documents/`; update `docs/dev/README.md` if an archive path must be discoverable.

## Naming

- `{topic}-plan.md` — phased plan
- `{topic}-outline.md` — optional release-doc outline (no Phase N)

## Active

- [`unity-mcp-runtime-plan.md`](unity-mcp-runtime-plan.md) — Unity-VST3-Bridge `vst3-*` MCP 実機対応 — **Phase 0–1 landed**
- [`midi-mcp-player-runtime-plan.md`](midi-mcp-player-runtime-plan.md) — MIDI MCP の Player（実機）制御専用ランタイム分割（コア → MPTK → Chunity；配線は Editor）
- [`mptk-2.21-followup-plan.md`](mptk-2.21-followup-plan.md) — Maestro / MPTK 2.15→2.21 差分のプラグイン追従（Phase 0–6 complete）

## Archives

Completed Unity-MCP plan: [`docs/dev/unity-mcp-integration-plan.md`](../../docs/dev/unity-mcp-integration-plan.md).
