# Todo: `game-control`

**Plan:** [game-control-plan.md](game-control-plan.md) · **Map:**
[../docs/architecture/game-control-map.md](../docs/architecture/game-control-map.md) ·
**Specs:** `docs/architecture/game-control/spec-*.md`

> **Status audit 2026-09-15:** Slices 1–3 are implemented, and the MCP layer ships
> distinguished adapters (`debug_inspect`, `debug_click`, `debug_act`, `debug_cursor`, plus the
> four bounded evaluate adapters). The earlier unified `debug_control` proposal was dropped by
> owner reconciliation and must not be reintroduced. Injector/server builds pass and the full MCP
> suite has passed (92 tests); live click/cursor/act checkpoints remain unchecked until a fresh
> deployment proves the receipts and refusal cases against the same running build.

## Standing verification (every code task — Tasks 1–5)

- [ ] `.\scripts\verify-change.ps1 -Paths <changed files> -Session <build-session-id>` selects the focused checks (required local workflow — never a broad suite from caution)
- [ ] Injector tasks (1–4): `dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9` green — the interop catches `ScreenCapture`-class surprises CI cannot (proven live 2026-09-14)
- [ ] Const-introducing tasks: `python scripts/audit-magic-numbers.py` shows no new M1/M2 (structural consts carry their T2 comment)
- [ ] Spec updated before code on any discovery (spec is living — see plan); checkpoints below confirm it

---

## Slice 1 — Inspect (`control-inspect`)

### Task 1: `ControlInspect` + `debug.inspect` case + routes + snapshot contract

**Description:** Closed-allowlist scanner (`CardUI`, `AlmanacCardUI`, `Plant`, `Zombie`,
`Mower`, `GridItem`, `Bucket`, `MiniPet`, `Bullet`) emitting `debug.inspect` with
`{controls:[{ref,type,label,cell?,screen?}], truncated, next_cursor}`; one Drain case;
`POST /api/debug/inspect` relay + `GET /api/debug/inspect/latest` read, both bannered.
Snapshot id = the `debug.inspect` event id.

**Acceptance criteria:**

- [ ] Menu snapshot lists named controls with refs; lawn snapshot lists cells/cards/entities.
- [ ] `limit=1` truncates with `[TRUNCATED]` + `next_cursor`; paging continues exactly.
- [ ] No scan runs outside the Drain case (zero idle cost).

**Verification:**

- [ ] `.\scripts\guard-debug-scope.ps1` → exit 0
- [ ] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"` green
- [ ] Live (owner terminal, real game+server): menu + lawn snapshots read back, paging walked

**Dependencies:** None (map + specs approved).

**Files likely touched:**

- `src/FusionRpg.Injector/ControlInspect.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one case-block)
- `src/FusionRpg.Server/DebugEndpoints.cs` (two routes + banners)

**Estimated scope:** M.

### Checkpoint 1

- [ ] Snapshots with refs on menu + lawn; truncation + paging proven live; guard green.

---

## Slice 2a — Click (`control-click`)

### Task 2: `ControlRefs` + `ControlClick` + `debug.click` route + stale-refusal

**Description:** Snapshot-scoped ref table (`snapshotId → [(ref, ptr, typeName, row, col)]`);
resolve = re-scan + ptr match + type confirm + position confirm; type-dispatched invoke
over the closed callee set (`ClickOnCard`, `TryToSet*`, `DisassemblePlant`, `UseOnce`,
provisional `Button.onClick`, `UIMgr` table); `POST /api/debug/click`; bundled
full-scope snapshot in every receipt.

**Acceptance criteria:**

- [ ] Menu click navigates, card click selects, cell click places — each read back through
  the normal path, not the response alone.
- [ ] Stale snapshot ref, reused ptr (same type, new entity), and moved entity all refuse
  with the fresh-snapshot instruction.
- [ ] `Button.onClick` arm either proven live or deleted (verify-or-drop, per spec).

**Verification:**

- [ ] Guard script + `DebugScope` tests green
- [ ] Live (owner terminal): three click kinds read back; three refusal kinds observed

**Dependencies:** Task 1 (snapshot ids + ref shape).

**Files likely touched:**

- `src/FusionRpg.Injector/ControlRefs.cs` (new)
- `src/FusionRpg.Injector/ControlClick.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one case-block)
- `src/FusionRpg.Server/DebugEndpoints.cs` (one route + banner)

**Estimated scope:** M.

### Checkpoint 2

- [ ] Clicks read back; refusals proven; Button arm resolved (kept or deleted).

---

## Slice 2b — Cursor (`control-cursor`, parallel with Task 2)

### Task 3: `Win32Input` + `debug.cursor` case + route + triple refusal

**Description:** `SendInput` move/click isolated in `Hud/Win32Input.cs` (the `Hud/Win32.cs`
rule); Drain case requires per-call `confirmedLiveCursor:true` + game-foreground +
2 s rate limit, clamps to the game window rect; `POST /api/debug/cursor`; minimal
receipt `{x, y, clicked, foreground}` (no snapshot bundle — screenshot is the proof).

**Acceptance criteria:**

- [ ] Opt-in foreground click lands, read back via game hook telemetry (`card.pick`).
- [ ] Missing opt-in, background window, and rapid repeat each refuse with a distinct reason.
- [ ] No cursor movement exists outside the Drain case (grep-verified).

**Verification:**

- [ ] Guard script + `DebugScope` tests green
- [ ] Refusal-logic unit tests (opt-in parsing, rate limit) green without a game
- [ ] Live (owner terminal, owner watching): move visible, click lands, refusals observed

**Dependencies:** Task 1 (positions). Independent of Task 2 — parallelizable.

**Files likely touched:**

- `src/FusionRpg.Injector/Hud/Win32Input.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one case-block)
- `src/FusionRpg.Server/DebugEndpoints.cs` (one route + banner)

**Estimated scope:** S–M.

### Checkpoint 3

- [ ] Click lands via telemetry; all three refusals observed; no ambient movement possible.

---

## Slice 3 — Act (`control-act`)

### Task 4: `ControlAct` place/shovel/item + `debug.act` route + named-link receipts

**Description:** Verb chains composing `control-click` invokes only (no raw engine writes):
`place` (find card → click → set cell → place → telemetry wait), `shovel`, `item`;
`POST /api/debug/act`; receipts name the verb, the evidence, and the broken link on
failure; bundled full-scope snapshot.

**Acceptance criteria:**

- [ ] All three verbs end telemetry-proven (`card.place`, empty cell, item effect).
- [ ] Unknown typeId / out-of-board cell fail at resolve with names, never mid-chain silence.
- [ ] Every wait is bounded by the structural timeout const.

**Verification:**

- [ ] Guard script + `DebugScope` tests green
- [ ] Live (owner terminal): three verbs proven; two bad-input resolve failures observed

**Dependencies:** Task 2 (click primitives).

**Files likely touched:**

- `src/FusionRpg.Injector/ControlAct.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one case-block)
- `src/FusionRpg.Server/DebugEndpoints.cs` (one route + banner)

**Estimated scope:** M.

### Checkpoint 4

- [ ] Verbs telemetry-proven; resolve failures named; guard green.

---

## Slice 4 — Unified MCP tool

### Task 5: Distinguished MCP adapters + registration + walkthrough + tests

**Description:** Eight minimal MCP adapters (`debug_inspect`, `debug_click`, `debug_act`,
`debug_cursor`, `debug_evaluate_search`, `debug_evaluate_methods`, `debug_evaluate_call`, and
`debug_evaluate_text`) over the existing debug routes. Each is adapter-only, scope-stamped, and
returns failures as envelopes; there is deliberately no unified `debug_control` dispatcher.
Registered in `server.py` with the README walkthrough and focused pytest coverage.

**Acceptance criteria:**

- [ ] The adapters drive one inspect→click→read-back chain end to end via MCP alone (live).
- [x] Cursor adapter refuses without opt-in through the MCP path (unit/stub path).
- [x] Scope label present on every response; no new transport logic (all through
  `debug_call` + fetch seams).
- [x] README walkthrough + tool-count pin updated (the `test_readme_walks_every_registered_tool` pattern).

**Verification:**

- [x] `python -m pytest tools/debug-mcp/tests -q` green (92 passed, incl. README-walks-tools)
- [ ] Live (owner terminal): MCP-only chain observed against the real game with fresh deployed DLLs

**Dependencies:** Tasks 1–4 (routes must exist; stub tests writable earlier).

**Files likely touched:**

- `tools/debug-mcp/tools/debug_inspect.py`, `debug_click.py`, `debug_act.py`, `debug_cursor.py`
- `tools/debug-mcp/tools/debug_evaluate_search.py`, `debug_evaluate_methods.py`,
  `debug_evaluate_call.py`, `debug_evaluate_text.py`
- `tools/debug-mcp/tests/test_debug_control_inspect_click.py`,
  `test_debug_control_act_cursor.py`, `test_debug_control_evaluate.py`
- `tools/debug-mcp/server.py` (registration block)
- `tools/debug-mcp/README.md` (walkthrough item)
- `tools/debug-mcp/tests/test_http_mode.py` (count pin)

**Estimated scope:** S–M.

### Checkpoint 5

- [ ] Live adapter chain and all refusal cases proven; merge-ready handoff with exact regions.
