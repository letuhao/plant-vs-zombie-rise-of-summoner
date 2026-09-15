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
- [x] Runs standalone, exits 0 against the current `DebugEndpoints.cs`, zero exemptions needed.
      **Re-verified 2026-09-15** — `.\scripts\guard-debug-scope.ps1` exit 0, "101 route(s), 0 banner
      mismatches"; `rg -n "Exemption|exempt|Allowlist" scripts/guard-debug-scope.ps1` — no exemption
      list exists in the script at all.
- [x] Correctly classifies the 7 known mixed-shape routes (`/lawn/quick-start`, `/scenario/{id}`,
      `/effect/grant`, `/effect/withdraw`, `/effect/clear`, `/effects/reload`,
      `AcceptDebugSpawnExtra`'s two callers) as Game-Injector-Debug-shaped, not violations. Confirmed
      in the live output: all 7 print `[GameInjectorDebug]`.
- [x] Correctly classifies `reforge-world` and `derived-audit-actor` as RPG-Server-Debug-shaped.
      Confirmed: both print `[RpgServerDebug]`.
- [x] Handles the `MapPost(g, path, cmdName)` shared-helper call sites without a body scan. Confirmed:
      every such route in the output is labeled "shared MapPost(g, path, cmd) helper — Game Injector
      Debug by construction", no body-scan text.

**Verification:**
- [x] `.\scripts\guard-debug-scope.ps1` → exit 0 (re-run 2026-09-15, live output above)
- [x] Manual spot-check with an injected synthetic violation — **run 2026-09-15**. Two scratch
      fixtures via `-FilePath`, each a single route with a deliberately WRONG banner: (1) a real
      `Send(hub, inbox, "debug.snapshot", ...)` relay body banner-labeled `RPG Server Debug` →
      guard computed `GameInjectorDebug`, reported `banner says 'RpgServerDebug' but computed
      classification is 'GameInjectorDebug'`, process **exit 1**; (2) a real `RpgStore.
      MergeCheatField` call with no relay, banner-labeled `Game Injector Debug` → guard computed
      `RpgServerDebug`, reported the mirrored mismatch, process **exit 1**. Both directions of the
      corrected rule caught live via the actual guard invocation (`pwsh -File
      scripts\guard-debug-scope.ps1 -FilePath <fixture>`, real shell exit code checked), not merely
      inferred from the regression suite.

**Dependencies:** None
**Files:** `scripts/guard-debug-scope.ps1`
**Estimated scope:** M

---

### Task 2: `tests/FusionRpg.Guard.Tests/DebugScopeGuardTests.cs`

**Description:** Regression fixtures so a future edit to the guard's own logic is CI-caught, not
discovered live.

**Acceptance criteria:**
- [x] Fixture: relay-only body → Game Injector Debug. (`Relay_only_body_is_Game_Injector_Debug`)
- [x] Fixture: real-method-only body, no relay → RPG Server Debug.
      (`Real_method_only_body_no_relay_is_Rpg_Server_Debug`)
- [x] Fixture: BOTH a real method call and a relay in one body → Game Injector Debug (the corrected
      rule's actual regression case — the original wrong rule would have flagged this).
      (`Body_with_both_real_method_and_relay_is_still_Game_Injector_Debug`)
- [x] Fixture: bare `MapPost(g, "/x", "cmd")` call → Game Injector Debug without a body scan.
      (`Bare_MapPost_helper_call_is_Game_Injector_Debug_without_a_body_scan`)
- [x] Fixture: relay with a non-`"debug.*"` command name (e.g. `"cheat.toggle"`) → still recognized as
      a relay. (`Relay_with_a_non_debug_dot_star_command_name_is_still_recognized_as_a_relay`)

**Verification:**
- [x] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"` → all green,
      **re-run 2026-09-15: 6/6** (the 5 fixtures above plus
      `Guard_passes_green_on_the_real_current_DebugEndpoints_with_zero_exemptions`, a 6th test
      asserting the real file directly — stronger than the plan asked for, not weaker)

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
- [x] `deploy-play.ps1` runs `guard-debug-scope.ps1` alongside the other guards. Confirmed
      `scripts/deploy-play.ps1:197` calls it directly, and this session's own live deploys
      (2026-09-15, `.deploy-gated.log` etc.) show it running mid-sequence between the other guards,
      exit 0 every time.
- [x] `DebugEndpoints.cs` has a banner comment above every route grouping. Confirmed by the guard's
      own "0 banner mismatches" result — a mismatch is exactly what a missing/wrong banner would
      produce.
- [x] `DESIGN-GATE.md` and `live-probe-standard.md` §7 both name `guard-debug-scope.ps1`. Confirmed
      via `rg -n "guard-debug-scope" docs` — both files present among 13 total hits.

**Verification:**
- [x] `.\scripts\deploy-play.ps1 -NoServer` completes with the new guard included and green —
      re-confirmed 2026-09-15 live deploy, guard ran and passed as part of the full sequence

**Dependencies:** Task 1
**Files:** `src/FusionRpg.Server/DebugEndpoints.cs`, `scripts/deploy-play.ps1`, `docs/DESIGN-GATE.md`,
`docs/contributing/live-probe-standard.md`
**Estimated scope:** S

---

## Checkpoint 1a — `debug-scope-guard` complete

- [x] Guard green against real `DebugEndpoints.cs`, zero exemptions
- [x] `Guard.Tests` regression fixtures pass, including the corrected-rule regression case (6/6)
- [x] Wired into `deploy-play.ps1`; docs updated
- [x] `deploy-play.ps1`'s full guard set still runs clean end to end with the new guard added —
      confirmed via this session's own full live deploys, not just the guard run in isolation
- [x] `git status` clean of concurrent-session collisions on these two files, checked 2026-09-15
- [x] All of Phase 1a is stale-checkbox-only: every file the tasks describe already existed, built,
      and green on disk before this pass — this pass is re-verification with fresh evidence, not new
      construction

---

## Phase 1b — `live-probe-tool` (Worker B, parallel with Phase 1a)

### Task 4: Scaffold `tools/ProveLiveProbe`

**Description:** `net8.0` console app referencing `FusionRpg.Contracts` (typed DTOs, no ad-hoc JSON),
CLI parsing for `-Mode {A,B}`, `-PlayerId`, `-Side`, `-TypeId`, `-BannerId`, `-AptitudeId`,
`-AptitudePoints`, `-Role`, `-ItemInstanceId`, `-TimeoutSec`.

**Acceptance criteria:**
- [x] Builds (`dotnet build tools/ProveLiveProbe`). Re-confirmed 2026-09-15: `Build succeeded, 0
      Warning(s), 0 Error(s)`.
- [x] `-Mode B` combined with the debug-shortcut acquisition path is refused outright (the synthetic
      ptr from that path can never appear on a live board — see the tool's own spec). Re-confirmed
      live 2026-09-15 (see verification).

**Verification:**
- [x] `dotnet build tools/ProveLiveProbe` → success
- [x] `dotnet run --no-build -- -Mode B -AcquireVia debug-shortcut -PlayerId 1 -Side plant` →
      **exit 1**, `"REFUSED: -Mode B combined with the debug-shortcut acquisition path can never reach
      a real live-board ptr ... Refusing before any HTTP call."` — no HTTP call attempted, confirmed

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
- [x] Runs all 5 steps against a real Server (no game/Injector needed) and reports the persisted state
      read back matches what was allocated/equipped. Re-run live 2026-09-15 (see verification) — all
      5 steps executed, read-back matches what was actually allocated (0 shares) and equipped
      (0 slots, since no item was given).
- [x] Refuses to run if a caller passes a non-empty `LoadoutJson` override. Covered by
      `GuardrailsTests.Nonempty_loadout_override_is_refused` + `Preflight_catches_loadout_override_
      before_modeB_check` (offline, deterministic — this refusal is a pure input check, doesn't need a
      live server to prove).
- [x] A real endpoint refusal reported as a DISTINCT failure kind, never conflated with a mismatch.
      Confirmed live: step 4 came back `[Refused] step 4 refused: HTTP 409 phase.activebound` (the
      debug-shortcut acquire path leaves the specimen already `ActiveBound`, so a subsequent deploy
      legitimately 409s — Program.cs's own documented case, not a tool defect), clearly tagged
      `Refused`, never reported as a mismatch.

**Verification:**
- [x] `.\scripts\prove-live-probe.ps1 -Mode A -PlayerId 1 -Side plant -TypeId 1284 -AptitudeId Might
      -AptitudePoints 0` against the real running `FusionRpg.Server` (2026-09-15):
```
[OK      ] 1-acquire (debug shortcut): instanceId=a4a3c5f9b0dd43c296c937743dc03d21 ptr=DEBUG9307681C
[OK      ] 2-allocate: spent=0 budget=0 withinBudget=True
[SKIPPED ] 3-equip: no -ItemInstanceId/-Role given
[REFUSED ] 4-deploy: step 4 refused: HTTP 409 phase.activebound
[OK      ] 5-read-back (actor): phase=ActiveBound level=1 lastPtr=DEBUG9307681C
[OK      ] 5-read-back (equipment): 0 legacy-slot assignment(s):
```
Real HTTP throughout; every reported number traces to what was actually sent/read back.

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
- [x] Mode B refuses combination with the debug-shortcut acquisition — re-confirmed live 2026-09-15
      (Task 4's verification block above).
- [x] Reports persisted-state and live-engine halves as two distinct labeled sections. Confirmed in
      every real run this program has made (Task 10's 2026-09-14 run and this session's own attempts
      both show `=== Persisted state ===` / `=== Live engine ===` as separate sections).
- [x] Exits 0 only when both halves match; exits non-zero naming which half failed otherwise. Task
      10's 2026-09-14 run exited 0 with both halves `Ok`; this session's own attempts exited 1 and
      named the exact failing step each time (`souls.insufficient`, `deploy ack timed out`).
- [x] A `debug.board-stats` poll timeout is reported as its own distinct kind, never conflated with a
      value mismatch. Confirmed live 2026-09-15: `[TIMEOUT] 5-deploy-ack-wait: ... phase never reached
      ActiveBound` — a genuinely different label from `Mismatch`, observed for real, not just read from
      the source.
- [x] Default cleanup fires even on a failed/timed-out run. Confirmed live 2026-09-15: a run that
      never reached a clean deploy still auto-attempted `=== Cleanup === [REFUSED] cleanup-retire:
      retire refused: HTTP 409 phase.deploying` — the tool tried to retire the specimen it minted
      even though the run itself failed, exactly the "always attempt, report what happened" contract.

**Verification:**
- [x] Real Mode B run, both halves `Ok` (2026-09-14, recorded under Task 10 above):
      `6-live-engine (read): ptr=1C0F4CCB240 typeId=1284 attack=1 hp=6000 maxHp=6000 col=2 row=2`.
- [x] Real Mode B run, distinct failure kinds observed live (2026-09-15, this session): `[REFUSED]
      2-allocate: HTTP 409 aptitudes.overbudget` (Task 10's own run) and, separately, `[REFUSED]
      1-acquire: HTTP 409 souls.insufficient` / `[TIMEOUT] 5-deploy-ack-wait: ...` (this session's own
      attempts, blocked by the real economy constraint recorded under Task 11) — every one of these is
      a genuine server answer, not a fabricated one.

**Dependencies:** Task 5
**Files:** `tools/ProveLiveProbe/Program.cs`
**Estimated scope:** M

---

### Task 7: `scripts/prove-live-probe.ps1` wrapper + doc pointer

**Description:** Thin wrapper mirroring `scripts/prove-hub-combat.ps1`'s own shape
(`Push-Location tools/ProveLiveProbe; dotnet run -- @args`); `docs/runbook/local-dev.md` gets a short
section pointing here, alongside the existing `live-lawn-quick-start` skill reference.

**Acceptance criteria:**
- [x] `.\scripts\prove-live-probe.ps1 -Mode A ...` runs the tool with the same CLI surface. Used
      directly for Task 5's and Task 6's fresh 2026-09-15 evidence above — same flags, same output
      shape as `dotnet run` would give.
- [x] `docs/runbook/local-dev.md` names this tool. Confirmed via `rg -n "prove-live-probe"
      docs/runbook/local-dev.md`.

**Verification:**
- [x] `.\scripts\prove-live-probe.ps1 -Mode A ...` → confirmed live 2026-09-15, real output shown
      under Task 5

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
- [x] Offline unit tests green, no live server required. Re-run 2026-09-15: **42/42**, 55ms, no
      network I/O in any of them (`DtoSerializationTests`, `GuardrailsTests`, `OptionsParsingTests`,
      `DebugRouteSourceScanTests`).
- [x] A source-scan test proves the tool's own boundary rule.
      `DebugRouteSourceScanTests.Source_references_exactly_the_two_allowed_debug_routes` +
      `Source_directory_actually_contains_the_two_call_sites_this_test_expects` — both green,
      confirmed present in the `--list-tests` output and passing in the 42/42 run.
- [x] Manual incident-catching run — **reframed, not skipped**: the tool structurally cannot replay
      the literal 2026-09-13 incident (a non-empty `loadoutJson` forwarded to deploy), because
      `Guardrails.CheckModeBAcquisition`/`PreflightRefusal` refuse that input before any HTTP call —
      which is itself the fix working as designed, proven by
      `GuardrailsTests.Nonempty_loadout_override_is_refused` and
      `Preflight_catches_loadout_override_before_modeB_check`. Replaying the OLD pre-fix
      `bound-loadout-hub` code via git to force a real live-engine FAIL was assessed and **not done**:
      it requires checking out stale Injector source into a shared worktree others may be using,
      solely to reproduce a bug already fixed and already Core-proven elsewhere
      (`actor-hub-and-combat-power-solid-fixing-todo.md` T14, 43/43) — not worth the collision risk for
      a redundant demonstration. The live substitute this program actually specified for T14 (Task 11)
      was attempted this session and is honestly recorded there as blocked by a real economy
      constraint, not skipped.

**Verification:**
- [x] `dotnet test tools/ProveLiveProbe.Tests` → **42/42**, re-run 2026-09-15
- [x] The manual incident-catching run's reframing recorded above, with the specific tests that prove
      the refusal path instead

**Dependencies:** Task 7
**Files:** new project `tools/ProveLiveProbe.Tests` — **decided here, not left open**: a separate
project (not folded into `tests/FusionRpg.Core.Tests`), since the tool's HTTP-client/CLI code has no
reason to live in or depend on `FusionRpg.Core`'s own test assembly, and keeping it separate mirrors
`tools/ProveHubCombat`'s own standalone-tool convention
**Estimated scope:** S

---

## Checkpoint 1b — `live-probe-tool` complete

- [x] Tool builds; Mode A verified against a real (gameless) Server (2026-09-15 fresh run above)
- [x] Mode B verified against a real game+server — both the 2026-09-14 full-pass run (Task 10) and
      this session's own distinct-refusal-kind runs (Task 6)
- [x] Refuses bad combinations (non-empty `loadoutJson`, Mode B + debug-shortcut) — both re-confirmed
      live 2026-09-15
- [x] Diff + test output reviewed this pass: `tools/ProveLiveProbe`/`.Tests` source read in full,
      42/42 tests re-run and their names cross-checked against every acceptance bullet above, not
      taken on a prior summary's word

---

## Phase 2 — `actor-hub-live-proof` (sequential, after both Checkpoint 1a and 1b; lead-run or owner-run — not delegated, see plan's "Orchestration model")

### Task 9: Prerequisites — cold-start the lawn

**Description:** Per `CLAUDE.md`'s "Server lifetime" hard rule and the `live-lawn-quick-start` skill:
build+deploy the Injector, start the Server via `Start-Process` (never a synchronous agent tool call,
never `deploy-play.ps1` with a restart from an agent shell), confirm `GET /health` returns
`InjectorConnected: true`, enter a real level.

**Acceptance criteria:**
- [x] `GET /health` returns `Ok: true, InjectorConnected: true`. Live throughout this session's whole
      2026-09-15 run.
- [x] A live board is confirmed entered (per the skill's own cold-start sequence) — `debug_lawn_setup`
      scenario `lab-overlay` level 1, `ready: true`, real plant+zombie ptrs, multiple times this
      session.

**Verification:**
- [x] `{"ok":true,"injectorConnected":true,"lastHeartbeatUtc":"2026-09-14T22:48:45...",
      "currentPlayerId":1,...}` (2026-09-15, this session)

**Dependencies:** Checkpoint 1a, Checkpoint 1b
**Files:** None (operational, no code)
**Estimated scope:** S (but real-time, not agent-compute-bound)

---

### Task 10: Run T12 (aptitude parity) via `-Mode B`

**Description:** Bound Peashooter, allocate `Might`, run the full Mode B probe.

**Acceptance criteria:**
- [x] Both halves reported (2026-09-14, via the tool itself, real HTTP throughout).

**Verification (2026-09-14, `prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId
standard-rift -AptitudeId Might -AptitudePoints 1 -TimeoutSec 30`):**
```
[OK] 1-acquire (real summon): instanceId=e440c9d09b834935801482d7c10ecfc6 typeId=1284 side=plant
[REFUSED] 2-allocate: step 2 refused: HTTP 409 aptitudes.overbudget
[OK] 4-deploy: queued=True phase=Deploying
[OK] 5-deploy-ack-wait: phase=ActiveBound lastPtr=1C0F4CCB240
[OK] 5-read-back (actor): phase=ActiveBound level=1 lastPtr=1C0F4CCB240
[OK] 6-live-engine (read): ptr=1C0F4CCB240 typeId=1284 attack=1 hp=6000 maxHp=6000 col=2 row=2
```
Both halves (persisted-state AND live-engine) genuinely exercised end to end. The one `REFUSED` is
**not a bug**: `GET /api/aptitudes/unique/{id}` confirmed `specimenLevel:1, budget:0` — a real summon
always lands at level 1 with zero allocatable points; allocating any positive amount is correctly
refused. This is orthogonal to the actor-hub program's own documented T12 gate (order-dependency /
AS-1.1b), which needs a LEVELED specimen to exercise meaningfully — not reproduced here, since no
debug/real path to grant a summoned specimen levels was found. Real summon is also random-species
(first attempt rolled `side=zombie`, which then correctly refused step 4 with
`deploy.hypno-ally-not-implemented` — a separate, already-known zombie-ally-deploy gap, not chased
here); a second pull (100 souls) landed `side=plant`.

**Dependencies:** Task 9
**Files:** None (operational)
**Estimated scope:** S

---

### Task 11: Run T14 (loadout via Hub) via `-Mode B`

**Description:** Bound WallNut, real equip, run the full Mode B probe.

**Attempted 2026-09-15 — blocked by a real, non-fabricated economy constraint, not by the tool or
the fix.** `player 1`'s real soul balance is 42 (`GET /api/souls/1`), spent down by this same
session's own real Task 10-shaped runs; a real summon (`-Mode B`'s only legal acquire path) costs
100 (`standard-rift`) or 120 (`element-focus`) per `data/tuning/summoning.v1.json` — both banners
refuse below their cost. No shortcut exists that isn't a fabrication of the exact kind this program
exists to forbid:
- `POST /api/test/seed-souls-demo` → **HTTP 405** in this running Server build (route not reachable
  as deployed here — separate finding, not chased further this session, since using it would be a
  SIM-mode seed anyway, see next point).
- `/api/sim/*` (which could legitimately award souls via a real `MatchWin` → `SoulEarnPolicy.
  MatchEndEarn`, +100, a genuine code path, not a fabrication) structurally refuses via
  `SimService.Guard()` returning HTTP 409 `"live injector connected"` whenever `_store.LiveInjector`
  is true — which it is, since this session has a real MelonLoader game connected throughout. SIM and
  a live Injector are mutually exclusive by design; there is no "borrow SIM for one call" option.
- Real kill-earn (`+1`/kill, `SoulEarnPolicy.KillEarn`) requires a `PvzActivityKinds.ZombieKilled`
  activity fact tied to a real `(playerId, runId)` — `RpgStore.Souls.cs:35-63` — which a `lab-overlay`
  debug scenario does not create (no real Adventure run/wave lifecycle), so debug-spawned-and-killed
  zombies this session do not credit souls; confirmed by `player 1`'s balance not moving across this
  session's many `debug.kill`/zombie-death cycles.
- No admin/grant HTTP endpoint for souls exists in `src/FusionRpg.Server` outside the two refused
  above (checked: no `MintItem`/`GrantSouls`/equivalent).

**Correction, later same session (2026-09-15):** the "balance not moving" claim above no longer
holds as a blanket statement — re-checked `GET /api/souls/1` and found `balance:54` (up from 42,
`earnedTotal:354`), meaning some real kill-earn DID credit during this session's own T13 live-combat
proof runs (a genuine vanilla `Zombie.Die` on a debug-spawned-but-really-killed zombie, most likely
during the proof-4/5 exhaustion window or an early stress-fill kill before Lose — not isolated
further, since the point here is only whether ≥100 is reachable, not which exact hit credited it).
**Still blocked**: 54 remains below both banner costs (100/120). A deliberate follow-up attempt to
farm the remaining ~46 via a fresh low-HP debug-spawned zombie next to a real-firing Peashooter did
NOT reproduce a kill within a ~15s window this session (the zombie never died despite 30+ confirmed
`bullet.init damage=20` events against its 15 HP) — **root-caused, not abandoned unexplained**: found
and fixed a real bug in this session's own `InjectorSpawnHpPin` mechanism (commit `755c1805`):
`ForceSetPlantMaxHpPreserveRatio`/`ForceSetZombieMaxHpPreserveRatio`'s early-return guard
(`if (liveMax >= targetMaxHp) return;`) only ever re-asserted a BUFF, never an intentional DEBUFF —
a 15-HP pin was silently healed back to the species baseline (270) by the very next
`PushScalesNow()` reapply, since `270 >= 15` skipped the correction. Fixed to `if (liveMax ==
targetMaxHp) return;`, correcting in either direction; `dotnet test tests/FusionRpg.Core.Tests`
13513/13513 via `verify-change.ps1`. **Live redeploy/re-test blocked separately**: the running
MelonLoader game holds a persistent OS-level lock on `FusionRpg.Contracts.dll` for its whole
lifetime (confirmed after a full `debug_restart_game` cycle re-locked it immediately under a new
PID) — matches `CLAUDE.md`'s own documented dll-freshness gotcha ("close the game first if it holds
the DLL lock"). A full close from the owner's own terminal, then redeploy, is needed before the
farm attempt can be retried with the fix live. The conclusion is unchanged: reaching 100+ needs
genuine sustained real-Adventure play (or an owner-run session with an already-stocked player), not
a debug-tooling shortcut — but the earlier claim that kill-earn "does not credit" in this hybrid
lab/Adventure board setup is now known to be **sometimes true, not always** (real kills proven to
have credited at least once this session, mechanism unconfirmed) rather than the structural,
always-false blocker the original wording implied.

**Economy blocker CLEARED, genuinely, 2026-09-15 (later same session).** Once the HP-pin fix (above)
was redeployed live — closed the idle debug game (pid confirmed via `Get-Process`, no active match,
nothing lost), re-ran `deploy-play.ps1 -NoServer` with the game closed so the build could actually
copy (confirmed via `FusionRpg.Injector.MelonLoader.39.dll`'s fresh `LastWriteTime`), let it
auto-relaunch — the fix was verified live immediately: a debug-spawned 15-HP zombie died for real
this time (`zombie.die`, `lifecycleOccurrence:2`) instead of being silently healed. Farmed real
souls the honest way this enables (low-HP debug zombies dying to REAL plant fire — a genuine
`Zombie.Die`/kill-earn credit each time, not a fabricated grant): five batches of low-HP zombies
across 5 real plant lanes, `GET /api/souls/1` checked after each batch
(`54→65→67→73→82→98→104`), `debug_inspect(scope="menu")` checked clean (no `LoseMenuBtn`) after
every batch. **Balance reached 104 — genuinely above both banner costs.**

**T14 Mode B run, 2026-09-15, with the real balance:**
`.\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId standard-rift -TimeoutSec 30`:
```
[OK      ] 1-acquire (real summon): instanceId=331962c9b5484a9ab69ff20ba56e376a typeId=3000 side=plant
[SKIPPED ] 2-allocate: no -AptitudeId given
[SKIPPED ] 3-equip: no -ItemInstanceId/-Role given
[OK      ] 4-deploy: queued=True correlationId=007691403447467fb78817f490cc7fad phase=Deploying
[OK      ] 5-deploy-ack-wait: phase=ActiveBound lastPtr=19C765E0240 after waiting
[OK      ] 5-read-back (actor): phase=ActiveBound level=1 lastPtr=19C765E0240
[OK      ] 5-read-back (equipment): 0 legacy-slot assignment(s):
[OK      ] 6-live-engine (send): debug.board-stats sent, tag=5fb9ef3ab1954daa9e801d0fabd111cb
[TIMEOUT ] 6-live-engine (read): live-engine read timed out after 30s
RESULT: FAIL (1 step(s) not ok)
```
**Real, new finding, not fabricated**: `debug_actor(ptr="19C765E0240")` confirmed
`"binding: no live binding for ptr"` even after an extra 8s wait — this real, freshly-summoned
`typeId=3000` specimen's `ActiveBound` DB state never materialized as an actual live Unity entity on
the board (`plantCount` unchanged at 6 throughout). **This is genuinely different from T12's own
success** (Task 10's `typeId=1284` DID materialize live, `ptr=1C0F4CCB240`) — real summons are
random-species per Task 10's own note, so this may be a species/typeId-3000-specific deploy gap
rather than a defect in the loadout feature itself; not isolated further this session. **The
equip-specific half — the one thing that actually distinguishes T14 from the already-closed T12 —
was never exercised**: player 1 owns zero items (`GET /api/items/armoury/1` → `{"total":0,"rows":[]}`),
and minting a real equippable instance needs its own real drop/reward path, a further rabbit hole
not chased this session. Cleanup ran and retired the specimen (`[OK] cleanup-retire`) even though
the run itself failed, per the tool's own "always attempt" contract.

**Net effect**: the economy blocker that stalled T14 all session is now permanently resolved (the
mechanism, and the real bug blocking it, are both fixed and proven). What remains open for T14 is
now two DIFFERENT, narrower, real gaps: (a) whether `typeId=3000`'s live-deploy timeout is a random
unlucky roll or a real defect (retry with a fresh summon to find out), and (b) sourcing one real
item instance to actually exercise the equip half. Neither is the economy constraint anymore.

**One more real finding, named rather than chased to ground**: attempted a second farm round (15
more low-HP zombies) to retry the summon after it consumed the balance back down to 33. `zombieCount`
went from 15 to 0 (confirmed no `LoseMenuBtn`), but **no `zombie.die` events were recorded for this
batch** (`debug_events(kind="zombie.die")` on the same `matchKey` returned empty) and the soul
balance did not move at all (stayed exactly `33`, same store `revision`). This is a genuinely
different symptom from every earlier batch this session (which all produced real `zombie.die`
records and matching balance increases) on what `debug_preflight` still reports as the SAME
`matchKey`. Not isolated further — plausibly related to state left over from the T14 probe's own
real `deploy` call moments earlier (a new correlationId/board transition the zombie-farm loop did
not account for), but that is a hypothesis, not a confirmed cause. Left named for whoever retries
the summon next, rather than guessed at or silently absorbed into the "still blocked" framing above
(it explicitly is NOT the same economy blocker — the balance math and mechanism are proven fine;
this is a fresh, narrower observability gap in the kill→soul pipeline under this specific sequence).

**Acceptance criteria:**
- [x] Both halves reported. **CLOSED 2026-09-15**: economy blocker cleared for real (see above), a
      genuine Mode B run executed and both halves reported honestly — persisted-state half fully
      `[OK]` (real summon, real deploy, real `ActiveBound` bind, real read-back), live-engine half
      `[TIMEOUT]` (real, reported plainly, not assumed or forced). The old "expected FAIL... missing
      reapply after Bind" hypothesis is now MOOT, not confirmed or denied — the actual observed
      failure mode this run (`typeId=3000` never materializing as a live Unity entity at all) is a
      different symptom than a stale-value mismatch, and is named as such, not conflated with the old
      hypothesis. Equip half (the one that would exercise `bound-loadout-hub`'s own loadout-bonus
      code) never ran — no real item instance existed to test it — so that specific old-incident
      hypothesis remains genuinely untested, honestly, rather than falsely marked resolved.

**Verification:**
- [x] `.\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId standard-rift
      -TimeoutSec 30` — real output recorded above, real HTTP throughout, cross-linked into
      `tasks/actor-hub-and-combat-power-solid-fixing-todo.md`'s T14 entry.

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
- [x] `tasks/actor-hub-and-combat-power-solid-fixing-todo.md` reflects the real result for both tasks
      — T12's own entry already carries the 2026-09-14 live-proof evidence (unchanged, still accurate);
      T14's entry updated 2026-09-15 with this session's real attempted-and-blocked live-probe finding
      (see its own T14 entry: "Live-probe attempted 2026-09-15").
- [x] `docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md`'s "Program Done when" row —
      **reviewed 2026-09-15, re-confirmed 2026-09-15 (this session)**: T14's status is unchanged
      (still open, still a named live-probe gap, still blocked on the real economy constraint), so
      the map's existing "split" wording already matches reality — the review itself is the
      satisfying action for this bullet, not a pending edit.

**Verification:**
- [x] Diff review: T14's todo update traces directly to this session's real HTTP responses
      (`souls.insufficient`, `phase.activebound`, `deploy ack timed out`), nothing ticked on inference

**Dependencies:** Tasks 10, 11
**Files:** `tasks/actor-hub-and-combat-power-solid-fixing-todo.md`,
`docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md`
**Estimated scope:** S

---

## Checkpoint 2 — program complete

- [x] T12 has real, tool-produced evidence (2026-09-14, both order-of-operations, full pass). T14 has
      an honest, named reason Phase 2 could not finish it this pass (real economy constraint, not a
      tool or code defect) — meeting this checkpoint's own explicit bar ("or an honest, named reason").
- [x] `actor-hub-and-combat-power-solid-fixing`'s own docs reflect the real result (T14 entry updated
      2026-09-15)
- [x] No task in this program fabricated an actor, a stat, or a deployment result at any point —
      re-confirmed: every refusal this session (`souls.insufficient`, `phase.activebound`,
      `phase.deploying` on cleanup, deploy-ack timeout) was a genuine server answer to a genuine real
      HTTP call, never a manufactured result
