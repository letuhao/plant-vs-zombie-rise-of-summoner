# Todo: `live-probe`

**Plan:** [live-probe-plan.md](live-probe-plan.md) · **Map:**
[../docs/architecture/live-probe-map.md](../docs/architecture/live-probe-map.md)

---

## Phase 1a — `debug-scope-guard` (Worker A, parallel with Phase 1b)

### Task 1: Write `scripts/guard-debug-scope.ps1`

**Description:** The guard itself — brace-depth handler-body isolation (mirroring
`guard-test-substrate.ps1`'s `Find-SwallowedDelete`), a special case for bare `MapPost(g, "path",
"cmd")` calls (Game-Injector-Debug by construction, no body scan), and the corrected classification
rule: any `Send(hub, inbox, "<anything>", ...)` call anywhere in a route's body makes it
Game-Injector-Debug-shaped, regardless of what else the body does; no relay + a real `store.*`/`ua.*`
call makes it RPG-Server-Debug-shaped; neither is flagged for manual review.

**Acceptance criteria:**
- [ ] Runs standalone, exits 0 against the current `DebugEndpoints.cs`, zero exemptions needed.
- [ ] Correctly classifies the 7 known mixed-shape routes (`/lawn/quick-start`, `/scenario/{id}`,
      `/effect/grant`, `/effect/withdraw`, `/effect/clear`, `/effects/reload`,
      `AcceptDebugSpawnExtra`'s two callers) as Game-Injector-Debug-shaped, not violations.
- [ ] Correctly classifies `reforge-world` and `derived-audit-actor` as RPG-Server-Debug-shaped.
- [ ] Handles the `MapPost(g, path, cmdName)` shared-helper call sites without a body scan.

**Verification:**
- [ ] `.\scripts\guard-debug-scope.ps1` → exit 0
- [ ] Manual spot-check: run with a temporary synthetic violation (a route with a real write and NO
      relay, injected into a scratch copy) and confirm it's classified RPG-Server-Debug (correct), and
      a route with a relay and a real write classified Game-Injector-Debug (correct, not a violation)

**Dependencies:** None
**Files:** `scripts/guard-debug-scope.ps1`
**Estimated scope:** M

---

### Task 2: `tests/FusionRpg.Guard.Tests/DebugScopeGuardTests.cs`

**Description:** Regression fixtures so a future edit to the guard's own logic is CI-caught, not
discovered live.

**Acceptance criteria:**
- [ ] Fixture: relay-only body → Game Injector Debug.
- [ ] Fixture: real-method-only body, no relay → RPG Server Debug.
- [ ] Fixture: BOTH a real method call and a relay in one body → Game Injector Debug (the corrected
      rule's actual regression case — the original wrong rule would have flagged this).
- [ ] Fixture: bare `MapPost(g, "/x", "cmd")` call → Game Injector Debug without a body scan.
- [ ] Fixture: relay with a non-`"debug.*"` command name (e.g. `"cheat.toggle"`) → still recognized as
      a relay.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"` → all green

**Dependencies:** Task 1
**Files:** `tests/FusionRpg.Guard.Tests/DebugScopeGuardTests.cs`
**Estimated scope:** S

---

### Task 3: Wire the guard in; scope-banner comments; doc updates

**Description:** `DebugEndpoints.cs` gets `// Game Injector Debug` / `// RPG Server Debug` banner
comments above each route grouping (human-readable, not what the guard checks); the guard joins
`deploy-play.ps1`'s guard set; `DESIGN-GATE.md`'s live-probe row and `live-probe-standard.md` §7 are
updated to name the guard now that it exists (both currently say "no automated guard enforces this
today").

**Acceptance criteria:**
- [ ] `deploy-play.ps1` runs `guard-debug-scope.ps1` alongside the other guards.
- [ ] `DebugEndpoints.cs` has a banner comment above every route grouping.
- [ ] `DESIGN-GATE.md` and `live-probe-standard.md` §7 both name `guard-debug-scope.ps1`.

**Verification:**
- [ ] `.\scripts\deploy-play.ps1 -NoServer` (or the guard subset it runs) completes with the new guard
      included and green

**Dependencies:** Task 1
**Files:** `src/FusionRpg.Server/DebugEndpoints.cs`, `scripts/deploy-play.ps1`, `docs/DESIGN-GATE.md`,
`docs/contributing/live-probe-standard.md`
**Estimated scope:** S

---

## Checkpoint 1a — `debug-scope-guard` complete

- [ ] Guard green against real `DebugEndpoints.cs`, zero exemptions
- [ ] `Guard.Tests` regression fixtures pass, including the corrected-rule regression case
- [ ] Wired into `deploy-play.ps1`; docs updated
- [ ] `deploy-play.ps1`'s full guard set still runs clean end to end with the new guard added — not
      just the new guard in isolation (a wiring mistake in Task 3 could break the whole script's
      execution order, not only fail to add the new check)
- [ ] `git status` on `scripts/deploy-play.ps1`/`docs/DESIGN-GATE.md` checked clean of any OTHER
      session's concurrent edits before Task 3 starts (both are shared, high-traffic files this repo
      has already seen concurrent-session collisions on this session)
- [ ] Lead reviewed the actual diff + test output, not just Worker A's summary

---

## Phase 1b — `live-probe-tool` (Worker B, parallel with Phase 1a)

### Task 4: Scaffold `tools/ProveLiveProbe`

**Description:** `net8.0` console app referencing `FusionRpg.Contracts` (typed DTOs, no ad-hoc JSON),
CLI parsing for `-Mode {A,B}`, `-PlayerId`, `-Side`, `-TypeId`, `-BannerId`, `-AptitudeId`,
`-AptitudePoints`, `-Role`, `-ItemInstanceId`, `-TimeoutSec`.

**Acceptance criteria:**
- [ ] Builds (`dotnet build tools/ProveLiveProbe`).
- [ ] `-Mode B` combined with the debug-shortcut acquisition path is refused outright (the synthetic
      ptr from that path can never appear on a live board — see the tool's own spec).

**Verification:**
- [ ] `dotnet build tools/ProveLiveProbe` → success
- [ ] `dotnet run --project tools/ProveLiveProbe -- -Mode B -AcquireVia debug-shortcut ...` (or
      equivalent) → refuses with a clear error, does not attempt the HTTP calls

**Dependencies:** None
**Files:** `tools/ProveLiveProbe/Program.cs`, `tools/ProveLiveProbe/ProveLiveProbe.csproj`
**Estimated scope:** M

---

### Task 5: Mode A — steps 1-5 (persisted-state only)

**Description:** Real HTTP against `POST /api/debug/spawn-unique-actor` (Mode A's acquire),
`POST /api/aptitudes/unique/allocate` (translating `-AptitudeId`/`-AptitudePoints` into a `Shares`
dict), `POST /api/items/equip` (with `SpecimenId`, `InstanceId`, `Role` all populated), `POST
/api/unique/actors/{id}/deploy` (`LoadoutJson` always empty), then `GET /api/unique/actors/{id}` +
`.../equipment` for persisted-state read-back.

**Acceptance criteria:**
- [ ] Runs all 5 steps against a real Server (no game/Injector needed) and reports the persisted state
      read back matches what was allocated/equipped.
- [ ] Refuses to run if a caller passes a non-empty `LoadoutJson` override.
- [ ] A real endpoint refusal (any 4xx — insufficient souls, over-budget aptitude, unknown item, etc.)
      at any step is reported as a DISTINCT failure kind ("step N refused: <server's own reason>"),
      never conflated with an assertion mismatch (persisted value != expected). An implementer or CI
      run must be able to tell "the server said no" from "the server said yes but the numbers are
      wrong" at a glance.

**Verification:**
- [ ] `dotnet run --project tools/ProveLiveProbe -- -Mode A ...` against a real running
      `FusionRpg.Server` (no game needed) → reports persisted-state pass with real numbers

**Dependencies:** Task 4
**Files:** `tools/ProveLiveProbe/Program.cs` (or a split `HttpSteps.cs`)
**Estimated scope:** M

---

### Task 6: Mode B — step 6 (live-engine read, separate)

**Description:** Extends Mode A: acquisition MUST be real summon (`POST /api/creatures/summon`), not
the debug shortcut; after step 5, send `debug.board-stats` for the deployed ptr and poll `GET
/api/debug/events?kinds=debug.board-stats` (bounded timeout, not a fixed sleep — mirrors
`DebugEndpoints.cs`'s own `PollForKind` pattern) until the live values arrive; report the two halves
(persisted, live-engine) separately, never merged into one boolean.

**Acceptance criteria:**
- [ ] Mode B refuses combination with the debug-shortcut acquisition (Task 4's own refusal, exercised
      here against the real acquire step).
- [ ] Reports persisted-state and live-engine halves as two distinct labeled sections.
- [ ] Exits 0 only when both halves match; exits non-zero naming which half failed otherwise.
- [ ] A `debug.board-stats` poll that times out (no live board, wrong ptr, injector disconnected
      mid-run) is reported as its own distinct failure kind ("live-engine read timed out after Ns"),
      never silently read as "live-engine half FAILED to match" — those are different facts (no
      answer vs. a wrong answer) and must not collapse into one message.
- [ ] After a Mode B run (pass or fail), the tool offers/performs cleanup: `POST /api/unique/actors/
      {id}/retire` for the specimen it minted — real summon/allocate/equip cycles otherwise
      accumulate permanent roster junk on whatever player account runs this repeatedly. A `-NoCleanup`
      flag may skip it for a deliberate follow-up inspection, but cleanup is the default.

**Verification:**
- [ ] `dotnet run --project tools/ProveLiveProbe -- -Mode B ...` against a real running Server + real
      game/Injector connected → both halves reported with real numbers

**Dependencies:** Task 5
**Files:** `tools/ProveLiveProbe/Program.cs`
**Estimated scope:** M

---

### Task 7: `scripts/prove-live-probe.ps1` wrapper + doc pointer

**Description:** Thin wrapper mirroring `scripts/prove-hub-combat.ps1`'s own shape
(`Push-Location tools/ProveLiveProbe; dotnet run -- @args`); `docs/runbook/local-dev.md` gets a short
section pointing here, alongside the existing `live-lawn-quick-start` skill reference.

**Acceptance criteria:**
- [ ] `.\scripts\prove-live-probe.ps1 -Mode A ...` runs the tool with the same CLI surface.
- [ ] `docs/runbook/local-dev.md` names this tool.

**Verification:**
- [ ] `.\scripts\prove-live-probe.ps1 -Mode A ...` → same result as calling `dotnet run` directly

**Dependencies:** Task 6
**Files:** `scripts/prove-live-probe.ps1`, `docs/runbook/local-dev.md`
**Estimated scope:** S

---

### Task 8: Tests + the incident-catching proof

**Description:** Offline unit tests (DTO (de)serialization, the `Shares`-dict translation, the
Mode-B+debug-shortcut refusal) need no live server. Separately, a recorded MANUAL run (not CI-
automatable) proving the tool would have caught the 2026-09-13 incident: run Mode B against a
specimen with a deliberately non-empty `loadoutJson` (or the pre-fix `bound-loadout-hub` state via git
if still reachable) and confirm a live-engine-half FAIL is reported, not a pass.

**Acceptance criteria:**
- [ ] Offline unit tests green, no live server required.
- [ ] A source-scan test proves the tool's own boundary rule: the ONLY `/api/debug/*` routes
      referenced anywhere in `tools/ProveLiveProbe`'s source are the identity-only acquire shortcut
      and `debug.board-stats`'s read — the same class of guard `debug-scope-guard` applies to
      `DebugEndpoints.cs` itself, applied here to this tool's own client code so a later change can't
      quietly add a fabrication-shaped debug call without a test catching it.
- [ ] Manual incident-catching run recorded in this file (what was run, what it reported) once
      performed — this specific check needs a live game+server, so it may land at Checkpoint 1b as an
      honest "not yet run" if no live session is available yet, never silently skipped.

**Verification:**
- [ ] `dotnet test tools/ProveLiveProbe.Tests` → green
- [ ] The manual incident-catching run's output, pasted or summarized in this file

**Dependencies:** Task 7
**Files:** new project `tools/ProveLiveProbe.Tests` — **decided here, not left open**: a separate
project (not folded into `tests/FusionRpg.Core.Tests`), since the tool's HTTP-client/CLI code has no
reason to live in or depend on `FusionRpg.Core`'s own test assembly, and keeping it separate mirrors
`tools/ProveHubCombat`'s own standalone-tool convention
**Estimated scope:** S

---

## Checkpoint 1b — `live-probe-tool` complete

- [ ] Tool builds; Mode A verified against a real (gameless) Server
- [ ] Mode B verified against a real game+server, OR honestly marked "not yet live-verified, offline
      logic only" if no live session was available during this phase
- [ ] Refuses bad combinations (non-empty `loadoutJson`, Mode B + debug-shortcut)
- [ ] Lead reviewed the actual diff + test output, not just Worker B's summary

---

## Phase 2 — `actor-hub-live-proof` (sequential, after both Checkpoint 1a and 1b; lead-run or owner-run — not delegated, see plan's "Orchestration model")

### Task 9: Prerequisites — cold-start the lawn

**Description:** Per `CLAUDE.md`'s "Server lifetime" hard rule and the `live-lawn-quick-start` skill:
build+deploy the Injector, start the Server via `Start-Process` (never a synchronous agent tool call,
never `deploy-play.ps1` with a restart from an agent shell), confirm `GET /health` returns
`InjectorConnected: true`, enter a real level.

**Acceptance criteria:**
- [ ] `GET /health` returns `Ok: true, InjectorConnected: true`.
- [ ] A live board is confirmed entered (per the skill's own cold-start sequence).

**Verification:**
- [ ] `GET /health` response, pasted into the evidence record below

**Dependencies:** Checkpoint 1a, Checkpoint 1b
**Files:** None (operational, no code)
**Estimated scope:** S (but real-time, not agent-compute-bound)

---

### Task 10: Run T12 (aptitude parity) via `-Mode B`

**Description:** Bound Peashooter, allocate `Might`, run the full Mode B probe.

**Acceptance criteria:**
- [ ] Both halves reported; ideally both pass (T12 already passed once manually on 2026-09-13 — this
      run reproduces that through the tool itself, not by citing the earlier manual result).

**Verification:**
- [ ] `.\scripts\prove-live-probe.ps1 -Mode B ...` output, recorded in
      `tasks/actor-hub-and-combat-power-solid-fixing-todo.md`'s T12 entry

**Dependencies:** Task 9
**Files:** None (operational)
**Estimated scope:** S

---

### Task 11: Run T14 (loadout via Hub) via `-Mode B`

**Description:** Bound WallNut, real equip, run the full Mode B probe.

**Acceptance criteria:**
- [ ] Both halves reported. **Expected to still FAIL the live-engine half** per the known,
      unconfirmed-root-cause incident — report the real observed numbers plainly; do not assume the
      earlier hypothesis (a missing reapply after Bind) is confirmed without checking, and do not fix
      `bound-loadout-hub` here (out of scope; belongs to that program once the cause is confirmed).

**Verification:**
- [ ] `.\scripts\prove-live-probe.ps1 -Mode B ...` output, recorded in
      `tasks/actor-hub-and-combat-power-solid-fixing-todo.md`'s T14 entry

**Dependencies:** Task 9 (parallel-safe with Task 10 only if two specimens can coexist on the same
board without interference — otherwise sequential; check the board state before assuming both fit)
**Files:** None (operational)
**Estimated scope:** S

---

### Task 12: Update `actor-hub-and-combat-power-solid-fixing`'s own docs with real evidence

**Description:** Tick or leave-open T12/T14's remaining checkboxes in that program's own todo, with
the real evidence from Tasks 10-11; update its map's "Program Done when" row once both close (or stay
honestly split if T14 is still failing).

**Acceptance criteria:**
- [ ] `tasks/actor-hub-and-combat-power-solid-fixing-todo.md` reflects the real result for both tasks.
- [ ] `docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md`'s "Program Done when" row
      updated to match.

**Verification:**
- [ ] Diff review: every checkbox change traces to a Task 10/11 evidence line, nothing ticked on
      inference

**Dependencies:** Tasks 10, 11
**Files:** `tasks/actor-hub-and-combat-power-solid-fixing-todo.md`,
`docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md`
**Estimated scope:** S

---

## Checkpoint 2 — program complete

- [ ] T12 and T14 each have real, tool-produced evidence (or an honest, named reason if Phase 2 has
      not run yet — this checkpoint does not require Phase 2 to have happened, only that Phase 1 is
      solid; Phase 2 can complete later on its own schedule per the plan's own risk note)
- [ ] `actor-hub-and-combat-power-solid-fixing`'s own docs reflect the real result
- [ ] No task in this program fabricated an actor, a stat, or a deployment result at any point
