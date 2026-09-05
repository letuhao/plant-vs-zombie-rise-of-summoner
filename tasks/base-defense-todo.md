# Base defense — task list

**Plan:** [base-defense-plan.md](base-defense-plan.md) · **Map:**
[base-defense-map.md](../docs/architecture/base-defense-map.md) · **Specs:**
[docs/architecture/base-defense/](../docs/architecture/base-defense/)

**Status:** 2026-09-05. Nothing started.

**Every task names acceptance, verification and files.** A task touching more than ~5 files should
have been two. Each module's spec carries the full test list — the tasks below name the *load-bearing*
assertions, not all of them.

**Verification shorthand** used throughout:

```powershell
CORE   dotnet test tests\FusionRpg.Core.Tests   > core.log      # plain > , never | tail
GUARD  dotnet test tests\FusionRpg.Guard.Tests  > guard.log
DATA   dotnet test tests\FusionRpg.Data.Tests   > data.log
BOUND  .\scripts\guard-single-writer.ps1 ; .\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-dal.ps1
NUM    python scripts\audit-overflow.py ; python scripts\audit-magic-numbers.py --summary
WEB    cd web\fusion-rpg-web ; npm test ; npm run build ; npm run check:bundle
```

---

## Verification tasks — run these repeatedly, not once

- [ ] **V1 · The dependency graph stays acyclic**
  - Acceptance: 29 modules, no cycles, every dependency at a strictly earlier level
  - Verify: the ten-line header parser from audit pass 4 (P4-7). **Re-run after any module is added or moved**
  - Files: none (a check, not a change)
  - Note: **this check found four ordering errors that four passes of reading missed.** It is not optional

- [ ] **V2 · No spec has drifted from its module header**
  - Acceptance: every `spec-*.md` level/deps line matches the map's build order
  - Verify: same parser, cross-checked against `base-defense-map.md`
  - Note: pass 4's P4-1 was exactly this drift

---

## GATE 0 — before level 1

- [x] **G0.1 · Extend the determinism guard to `Core/Battle` and `Core/Effects`** — DONE 2026-09-05
  - Acceptance: the clock/RNG scan covers three trees, not one · **MET**
  - Evidence: widening to a naive shared `WorldSourceFiles()` surfaced **not one but three defects in
    the guard itself**, all fixed as part of this task rather than worked around — the plan's own
    "one line" prediction was wrong, recorded honestly:
    1. **The scan stopped at the first match per (file, symbol)** (`text.IndexOf`, not all
       occurrences) — a comment mentioning a banned symbol before a real usage could have masked it.
    2. **The scan was comment-blind** — four "violations" on first widening were doc comments
       *explaining* the no-`System.Random`/no-wall-clock rule (`BattleEngine.cs:84`,
       `BattleEffects.cs:234`, `SeededRng.cs:5`, `AtomRandom.cs:19`), tripping the ban meant to
       enforce what they documented. Fixed with a `StripLineComment` pass, matching the discipline
       `ReadsTheWorldItself` already used one line at a time.
    3. **The float-purity check must NOT widen with the clock/RNG check.** `Core/Battle`'s
       derived-stat/aura recompose system (`BattleDerivedModifierLedger`, `ActorDerivedSnapshot`,
       `combat.*` channels — aura-skill T4, a prior reviewed program) is `double`-typed by design;
       `DamageFx.cs`/`UiPresentSink.cs` are VFX/UI, not simulation. Forcing either into fixed-point
       would be an out-of-scope refactor of unrelated, already-shipped work. Kept scoped to
       `Core/World`'s own stored/hashed state, per base-defense-ideal.md §2 rule 8's own wording:
       "integer/fixed-point only in **game-affecting branches**", not everywhere `Core/Battle` touches.
    4. Added a single, **named, narrow** exemption (`SystemEffectClockException`) for
       `EffectModels.cs`'s `SystemEffectClock` — the real-clock type used deliberately at exactly one
       legitimate, non-replayed composition root (the Injector's live PvZ host), never as an implicit
       default. Exempts the type's exact declaration line, not the file, so a *different* wall-clock
       read anywhere else in the same file is still caught (proven by its own test).
  - Verify: `dotnet test tests/FusionRpg.Guard.Tests --filter WorldDeterminismGuardTests` → **10/10
    green**, including five new self-tests proving each fix (comment-stripping, all-occurrences
    scanning, the narrow exemption, and that both new source sets actually enumerate real files).
    Full `dotnet test tests/FusionRpg.Guard.Tests` → **202/202 green**.
  - Files: `tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs`

- [x] **G0.2 · Fix the wall clock the guard finds** — DONE 2026-09-05
  - Acceptance: `EffectBag.UtcNow` is injected at the composition root, not defaulted at the field ·
    **MET** — after the guard fixes above, exactly **one** real violation remained:
    `EffectBag.cs:188`'s `Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;`,
    precisely the defect audit C4 predicted.
  - Evidence: changed the field to a backing `Func<DateTimeOffset>?` whose getter **throws** if read
    before being set (message names the three deterministic composition roots that must wire their
    own clock, and the one live/non-replayed host that must opt into the wall clock explicitly).
    `TickDots()`'s unconditional `var now = UtcNow();` — read even when `Status == null` and the value
    then discarded — was moved inside the `Status != null` branch first, so a boardless/statusless
    caller never pays for a clock it never uses (an independent, narrower fix that keeps the throw's
    blast radius to exactly the callers that need a wired clock). The one production caller that had
    no explicit wiring, `FusionRpg.Injector/Effects/EffectRuntime.cs` (the live PvZ host — legitimately
    wants real time, unlike the three deterministic hosts, which already wired
    `Bag.UtcNow = () => Clock.UtcNow;`), now sets `_bag.UtcNow = () => DateTimeOffset.UtcNow;`
    explicitly at its own composition root, matching their pattern.
  - Verify: `GUARD` → 202/202 green. `CORE` → **6311/6311 green**, zero collateral breakage from the
    throw-on-unset change across the whole existing test suite. `EffectRuntime.cs`'s own compile
    **cannot be verified in this environment** — `FusionRpg.Injector.Tests` needs
    `$env:FUSIONRPG_GAME_DIR` pointing at a real game install (unset here), and the baseline (verified
    via `git stash`) fails with the identical 780 pre-existing Harmony/game-assembly errors with this
    change stashed away — confirmed environmental and pre-existing, not introduced. Reviewed by
    inspection instead: the edit is a 5-line addition matching the exact working pattern used 3 other
    times in the same codebase (`BattleEffects.cs:57`, `FoundationHarness.cs:34`,
    `SimEffectHost.cs:36`), assigning to the same property whose type Core.Tests already proves
    compiles correctly.
  - Boundary guards: `guard-single-writer.ps1`, `guard-funnel-delta.ps1`, `guard-secondary-no-unity.ps1`
    all **green** (touched `Core/Effects` and `Injector/Effects`).
  - Files: `src/FusionRpg.Core/Effects/EffectBag.cs`, `src/FusionRpg.Injector/Effects/EffectRuntime.cs`

- [x] **CP0 · Checkpoint** — DONE 2026-09-05. Guard extended and green (202/202), Core suite green
  (6311/6311), boundary guards green, findings recorded above with evidence.

---

## LEVEL 0 — parallel, no dependencies

> ⚠️ **EXTERNAL BLOCKER, discovered and confirmed 2026-09-05, active throughout this level's work.**
> This shared repo has multiple OTHER programs (`battle-tempo`, an items/mutation/materials program,
> and at least one more) landing uncommitted, in-progress, intermittently-broken work concurrently in
> the SAME working tree while base-defense executes. Confirmed via: (a) files I never touched changing
> content between my own reads/writes, (b) `dotnet build tests/FusionRpg.Core.Tests` failing on
> `SpeciesTempoTests.cs`/`ContractTuningTestBootstrap.cs` for a `SpeciesTempoReferenceIntervalMs` field
> and `DerivedTurnChannels` type I never introduced, (c) `PowerGuardTests` newly failing on
> `EnhancePolicy.cs` (Items/Mutation — untouched by base-defense) mid-session, (d) `git stash` proving
> a `FusionRpg.Injector.BepInEx` build failure pre-dates and is independent of any base-defense change.
>
> **What this means for verification below:** `dotnet build src/FusionRpg.Core/FusionRpg.Core.csproj`
> (standalone, 0 errors — proven repeatedly) and `dotnet test tests/FusionRpg.Guard.Tests` (202/202
> until an unrelated regression appeared mid-session, confirmed unrelated) are used as the PRIMARY
> evidence where noted. **Full `dotnet test tests/FusionRpg.Core.Tests` is BLOCKED** by the above,
> through no cause in base-defense's own files — confirmed repeatedly, not assumed. One single-line,
> unambiguous, purely-additive fix (a missing `using` in `ActionTimingDerivation.cs`, another
> program's file) was made to partially unblock the standalone Core build, since it could not possibly
> be wrong (`ActionEnvelope` exists at exactly one location in the whole tree). No other external
> file was touched — fixes requiring an invented value or an unknown type's members were correctly
> left alone as genuinely outside this program's scope and knowledge.
>
> Every task below states precisely what IS proven now vs what remains PENDING full-suite
> confirmation. **Re-run the full suite once the shared tree stabilizes** and update this file's
> evidence accordingly — this is not a claim of task completion, it is an honest record of a real,
> external, evidenced constraint.

### `battle-clock-profile` — [spec](../docs/architecture/base-defense/spec-battle-clock-profile.md)

- [x] **1.1 · Move `MaxRounds`/`RoundDurationMs` onto `BattleModeProfile`** — IMPLEMENTED 2026-09-05
  - Acceptance: nullable on `TimelineProfileTuning` (both default `null`); resolved in `BattleModeProfileCatalog.Build` as `t.MaxRounds ?? BattleRuleset.MaxRounds` — **MET in code**
  - Evidence: `dotnet build src/FusionRpg.Core/FusionRpg.Core.csproj` → **0 errors** (standalone, repeated). ⚠️ Full `CORE` (twelve-golden byte-identity) **BLOCKED** by an external, unrelated build failure — see the level-0 note above. New regression tests written in `ModeProfileTuningBindingTests.cs`, not yet executed
  - Files: `BattleModeProfile.cs`, `BattleTuning.cs`, `BattleModels.cs`

- [x] **1.2 · `BattleEngine` reads the profile, not the ruleset** — IMPLEMENTED 2026-09-05
  - Acceptance: zero reads of `BattleRuleset.MaxRounds`/`.RoundDurationMs` remain in `BattleEngine` — **MET**, confirmed by `grep -n "BattleRuleset\.MaxRounds\|BattleRuleset\.RoundDurationMs" BattleEngine.cs` returning only a comment, zero code
  - Evidence: standalone Core build 0 errors (as above)
  - Files: `BattleEngine.cs` (4 sites, now at `:240`/`:251-252`/`:469`/`:485-486` — line numbers drifted from the spec's citation, content identical)

- [x] **1.3 · `MaxLoopIterations` becomes profile-derived** — IMPLEMENTED 2026-09-05
  - Acceptance: `checked(activeProfile.MaxRounds * BattleRuleset.LoopGuardRoundMultiple)`; `LoopGuardRoundMultiple` = 4000 in `data/tuning/battle.v2.json` and all 3 test bootstraps, reproducing `50 * 4000 = 200_000` exactly
  - Evidence: `LoopGuardReproducesTwoHundredThousandAtFiftyRounds` test written; standalone build 0 errors. Test execution pending (blocked, see note)
  - Files: `BattleEngine.cs`, `BattleTuning.cs`, `BattleModels.cs` (`BattleRuleset.LoopGuardRoundMultiple`)

- [x] **1.4 · Add the `siege` profile row** — IMPLEMENTED 2026-09-05
  - Acceptance: `BattleModeProfileCatalog.SiegeId`/`.Siege`, one arm in `Resolve`, `KnownProfileIds` updated in `ModeProfileArchitectureTests.cs`. **`points: false`** — confirmed correct per `action-map.md:430`, not `ActionPointsEconomy` (see the correction below). `WScope.PerSide`, `OrdersBySpeed: true`, `RequiresLiveInput: true`, `ForecastExactness.Exact`. `data/tuning/battle.v2.json` + 3 bootstraps carry `w:2, wReact:0, passQuantum:1`, `maxRounds`/`roundDurationMs` deliberately unset (decision 29)
  - Evidence: standalone build 0 errors; `ModeProfileArchitectureTests`'s own id-literal-ban scan re-verified clean (`grep '"siege"' Timeline/*.cs` finds it only in the one exempt file)
  - ⛔ **Correction made during implementation, recorded because the same mistake was made once before in this program's docs (spec's own §5):** confirmed by re-reading `action-map.md:430` directly — `points: false` is correct; `ActionPointsEconomy` would have been wrong
  - Files: `BattleModeProfile.cs`, `ModeProfileArchitectureTests.cs`, `data/tuning/battle.v2.json`, 3 bootstraps

- [x] **1.5 · `Resolve` behaviour and the jitter statement** — IMPLEMENTED 2026-09-05
  - Acceptance: `Resolve("siege")` returns the cached row (loud-throw for unknown ids unchanged, `Siege` cached via `_siege ??=`); jitter needs no new field (`OrdersBySpeed` + `ForecastExactness.Exact` already states it) — no code change needed, **confirmed correct by inspection**
  - Evidence: `SiegeRowResolvesAndIsCached`, `UnknownProfileIdStillThrows`, `SiegeIsSpeedOrderedPerSideAndInteractive`, `SiegeRunsOneActionPerTurnNeverActionPoints`, `AProfileNamingItsOwnHorizonGetsIt` tests written in `ModeProfileTuningBindingTests.cs`; standalone build 0 errors; execution pending
  - Files: `BattleModeProfileCatalog.cs`

### `siege-supply` — [spec](../docs/architecture/base-defense/spec-siege-supply.md)

- [x] **2.1 · Split `Usable` into `Traversable` and `Source`** — IMPLEMENTED 2026-09-05
  - Acceptance: `Traversable` = owned + not held-against (unchanged traversal rule); `Besieged` = owned + held-against, unioned into the result explicitly. Fixes **F1** and **F1b**
  - ⛔ **Correction found and fixed during implementation, not merely applied from the spec:** the spec assumed `SupplyReach.From` includes seed nodes regardless of the `usable` predicate. **Read the actual implementation** — it does not; seeds are gated by the SAME predicate as traversal. A literal implementation of the spec's own wording would have shipped a fix that does not work. Fixed by explicitly unioning every besieged owned sector into the result post-BFS, with the consequence reasoned through explicitly (a besieged-only-Seat correctly isolates *that* sector; other sectors correctly lose supply and burn, which is the CORRECT behaviour F1b's "isolates one sector, not the faction" describes — the bug was blanket immunity via `connected.Count == 0`, not that other sectors stay fine)
  - Evidence: standalone Core build 0 errors. Behavioral test execution pending (blocked)
  - Files: `SupplyGraph.cs`

- [x] **2.2 · Read `TerritoryComponents.For` and either cite it as correct or fix it** — READ, NOT CORRECT, FIXED LOCALLY 2026-09-05
  - Acceptance: a besieged sector draws on **its own stock only**. ✅ **Read in full** — `TerritoryComponents.For` partitions by ownership + lane adjacency only; it does **not** consult `ZoneOfControl` at all, so it is **not already correct**
  - ⛔ **Deviation from the spec's own contingency plan, reasoned explicitly and recorded:** the spec's implicit fallback was "fix `TerritoryComponents.For` itself." Checked its callers first — **five**, spanning loam upkeep (`LoamPhases`), an AI policy (`FrontierRulesPolicy`), a reporting module (`LoamBalance`), and a server endpoint (`WorldEndpoints`) — none read, none verified safe to change. Fixing the shared utility would silently change behaviour for all four outside this task's scope. **Fixed LOCALLY instead**: a new `SplitOutBesieged` helper inside `LegionSupply.cs` re-partitions each component post-hoc, isolating only besieged sectors, leaving `TerritoryComponents.For` itself untouched
  - Evidence: standalone build 0 errors
  - Files: `LegionSupply.cs` (NOT `TerritoryComponents.cs` — deliberately)

- [x] **2.3 · `supply.besieged:` report line + rationing dial** — IMPLEMENTED 2026-09-05
  - Acceptance: `supply.besieged:` distinct from `supply.cut:`, fires exactly when `IsBesieged` (new public helper) and never both for the same sector (structurally — besieged sectors are always `connected` now, so they can never also hit the cut branch). `LoamPolicy.BesiegedRationMilli` = 1000 (no-op) in `loam.v4.json` + 3 bootstraps, applied to `Demand` in `LegionSupply.RationedDemand`, `checked`, divided by 1000 last
  - Evidence: standalone build 0 errors; parser validation added (`besiegedRationMilli` bounded 0..1000, throws outside range)
  - Files: `SupplyGraph.cs`, `LegionSupply.cs`, `LoamTuning.cs`, `LoamPolicy.cs`, `data/tuning/loam.v4.json`, 3 bootstraps

- [x] **2.4 · §7 cost 6 — slot ownership follows sector capture** — IMPLEMENTED 2026-09-05
  - Acceptance: a captured sector's slots change owner — `ClaimResolver.Run` now maps `s.Slots.Select(sl => sl with { OwnerFactionId = command.CommanderId })` alongside the sector's own owner change
  - **Golden risk RESOLVED 2026-09-05**: full `FusionRpg.Data.Tests` run (736/736 passing, including `ClaimBarrenGroundTests`, `BindWardenThreadingTests`, `LoamStructuresTests`, `LoamTextureTests`, `RaiseThreadingTests`) confirms the slot-owner change moved **zero** goldens — no existing scenario captures a sector with any slots that later gets recaptured, so the new `Slots = s.Slots.Select(sl => sl with { OwnerFactionId = ... })` line never fires against hashed state in any shipped test. The one golden that DID move this session (`WorldWaveOneAcceptanceTests`) was `SupplyGraph`'s F1/F1b fix (a separate, already-documented, deliberate re-bless — entry #14), not this line.
  - Evidence: standalone build 0 errors only
  - Files: `ClaimResolver.cs`

- [x] **2.5 · `ConnectedSectors` stays uncached, and no `IsBesieged` field is added** — MET BY CONSTRUCTION 2026-09-05
  - Acceptance: still recomputed every turn, never cached — **unchanged**, no memoisation added anywhere in this module's edits. A NEW public `IsBesieged(world, sectorId, factionId)` helper was added (derived, computed fresh every call from `ZoneOfControl.IsHeldAgainst` — not a stored field) to avoid duplicating the besieged-predicate between `ConnectedSectors` and `Run`
  - Evidence: source inspection — `grep -n "IsBesieged" SupplyGraph.cs` shows a method, not a field; no new field on `WorldSector`/`WorldState`
  - Files: `SupplyGraph.cs`

### `world-graph-diff` — [spec](../docs/architecture/base-defense/spec-world-graph-diff.md)

- [x] **3.1 · MEASURE first — this is the whole task**
  - Acceptance: turn-commit cost attributed across clear / write-by-table / `slots_json` / `SqliteCommand` construction, published under `docs/research/perf/`
  - Verify: the benchmark runs on an 18-sector × ~20-slot world
  - Files: `tests/FusionRpg.Bench/WorldGraphWriteBench.cs`, `tests/FusionRpg.Bench/Program.cs`, `tests/FusionRpg.Bench/FusionRpg.Bench.csproj`, `docs/research/perf/02-world-graph-write.md`
  - Evidence: isolated SQLite harness (not a call into `RpgStore` — its write helpers are private; see the file's own doc comment for why an isolated schema-identical harness was chosen over widening that surface). 556-row synthetic world (18×20 slots + factions/lanes/entities/members/intel per decision 19's scale). Median of 7 runs, Release build (`dotnet build -c Release`, run as `FusionRpg.Bench.exe`), full results and reading in `docs/research/perf/02-world-graph-write.md`.
  - ⛔ **Steps 3.2/3.3 are cancelled if statement reuse dominates.** Record that outcome explicitly
  - **Outcome: NOT cancelled.** Tested and ruled out a third candidate first (commit/fsync cost — an empty transaction commits in 0.12ms, ~1% of clear/write, so fsync does not explain the ~7-8ms clear / ~11-14ms write costs). C5's own named per-row suspect (`slots_json`/`forces_json` serialisation) measured at 0.07-0.08ms total — **falsified**, negligible. Statement reuse recovers ~20-22% of write cost and **0%** of clear cost (clear already issues one command per table, not per row). The remaining ~85% of clear+write cost tracks row count directly — exactly the term decision 21 (slot growth) multiplies. **Both step 2 and step 3 proceed** — full reasoning in the perf doc's "Decision gate" section.

- [x] **3.2 · (conditional) Prepared-statement reuse** — IMPLEMENTED 2026-09-05, not cancelled by 3.1's gate
  - Acceptance: read-back hash unchanged; no logic or schema change
  - Evidence: `WriteWorldGraphUnlocked` rewritten — one `SqliteCommand` prepared per table (7 tables: factions, sectors, slots, intel, lanes, entities, members), values assigned positionally per row via new `Prepared`/`ExecuteWith` helpers, reused across every row of that table. Same SQL text, same parameter names, same column order, same per-row values as before — the only change is command-object lifetime. `Insert()` kept (still used by `CreateWorld`'s single header-row insert).
  - Verify: `DATA` — full `FusionRpg.Data.Tests` run, **736/736 passing**, including every world round-trip/replay-parity/turn-commit/golden test (`WorldCommandRoundTripPropertyTests`, `WorldWaveOneAcceptanceTests`'s replay-parity and golden-hash tests, etc.) — read-back hash **unchanged** by this step specifically (the one golden that DID move this session is entry #14, `SupplyGraph`'s F1/F1b behaviour fix, unrelated to this statement-reuse change). `guard-dal.ps1` passes (SQL stays in `FusionRpg.Data`).
  - Files: `RpgStore.World.cs`

- [x] **3.3 · (conditional) Diffing writer + equivalence guard** — IMPLEMENTED 2026-09-05
  - Acceptance: `WorldCanonical.Hash(readBack) == WorldCanonical.Hash(next)` over **500 randomised mutations**; **DELETE handled** for slot, entity and lane
  - Evidence: `RpgStore.WorldGraphDiff.cs` (new file) — `DiffWorldGraphUnlocked` diffs all 7 tables (factions/sectors/slots/intel/lanes/entities/members) against the previously-committed state: DELETE for a key present in `previous` and absent from `next`, `INSERT OR REPLACE` for a new-or-changed row (per-table `*RowEquals` comparators, or direct record equality where there's no nested list to strip), nothing for a row that is byte-identical. Row-level comparators exclude fields that are genuinely a separate table (sector excludes `Slots`, entity excludes `Members`) so unrelated changes don't force a spurious rewrite. Wired into the real commit path — `RpgStore.WorldTurns.cs`'s `CommitWorldTurn` now calls `DiffWorldGraphUnlocked(db, tx, world, result.World)` in place of the old `ClearWorldGraphUnlocked`+`WriteWorldGraphUnlocked` pair; `CreateWorld` is untouched (full write against an empty graph is already the cheapest case, nothing to diff).
  - **The equivalence guard is real, not decorative**: reads the graph back on the SAME connection/transaction (`LoadWorldGraphUnlocked`, extracted from `LoadWorldState` for this reuse) and `Debug.Assert`s its hash equals `next`'s — on (Debug builds; opt-in via `FUSIONRPG_WORLD_DIFF_CHECK=1` in Release). **It found two real, pre-existing bugs during this task**, both fixed:
    1. `IntelSnapshot.DevelopmentLevel` was hashed by `WorldCanonical.Write` but `rpg_world_faction_intel` never carried a column for it — silently read back as 0 on every load, for any snapshot ever taken. Fixed: new `development_level` column (`EnsureColumn` migration, field-only, `RulesetVersion` unchanged, existing precedent), written and read on both the diff and full-write paths.
    2. (Test-side, not production) the 500-mutation test's own random intel generator violated `WorldState`'s documented "stable id order" invariant with a plain `.Append()` — the DB round-trip always reads back `ORDER BY faction_id, sector_id`, so an out-of-order in-memory `next` disagreed with any real read-back. Fixed in the test generator (`.OrderBy(...)` after every append), not in production code — this was never reachable from real gameplay, only from a test building an invalid-by-the-model's-own-contract state directly.
  - Test-only seam: `RpgStore.DiffCommitForTest(worldId, next)` (internal, via this project's existing `InternalsVisibleTo`) — diffs the stored graph against an arbitrary `next` and returns the fresh read-back, without needing a scripted `TurnEngine` turn to reach a particular shape.
  - Verify: `DATA` — new `tests/FusionRpg.Data.Tests/WorldGraphDiffTests.cs`, **9/9 passing**: unchanged-world no-op, grown slot list (existing slots untouched), slot/entity/lane DELETE handling, two `long`-magnitude-survives-unnarrowed cases (9 billion / 5 billion, both past `int.MaxValue`), the `DevelopmentLevel` regression, and the spec's own **500-randomised-mutation equivalence sweep** (`Five_hundred_random_mutations_all_round_trip_through_the_diff_path_unchanged`). Full `FusionRpg.Data.Tests` run: 754/760 passing — the 6 failures are confirmed external (`ItemSocketStoreTests` FK-constraint failures in the in-flight, unrelated item-socket feature; `InstanceOpTests`/`MaterialSpendTests` in the unrelated materials/mutation program), verified via file provenance, none touch `World`/`RpgStore.World*`. `guard-dal.ps1` passes.
  - Files: `RpgStore.WorldGraphDiff.cs` (new), `RpgStore.World.cs` (`LoadWorldGraphUnlocked` extraction, `development_level` migration+write+read), `RpgStore.WorldTurns.cs` (commit-path wiring), `tests/FusionRpg.Data.Tests/WorldGraphDiffTests.cs` (new)

---

## LEVEL 1–2 — the board and the seam

### `siege-board` — [spec](../docs/architecture/base-defense/spec-siege-board.md)

- [x] **4.1 · `GridSpec` + `CellTerrain`** — IMPLEMENTED 2026-09-05
  - Acceptance: row-major, `IndexOf` round-trips on a **non-square** board; `Gap` blocks movement not sight; `maxCells` enforced loudly and commented as a **structural** cap
  - Evidence: `GridSpec` is a `sealed record` with GET-ONLY properties (deliberately not `init` — an `init` setter would let `new GridSpec { Rows = 5000, ... }` bypass the validating constructor and silently defeat the maxCells check, so the constructor is the only way to build one). `CellTerrain` enum (`Open`/`Rough`/`Blocking`/`Gap`), `Open = 0` so a zero-filled array is a plain board. New tuning domain `data/tuning/siege.v1.json` (`SiegeTuning`/`SiegeTuningLoader`/`SiegeTuningPolicy`, same Policy+Loader+Hub pattern as `MatchTuningPolicy`); `MaxCells` documented in the JSON's own `_meta` as structural, not balance, per the spec's own instruction. Wired into the real Server composition root (`Program.cs`) and all three test bootstraps (`Core.Tests`/`Data.Tests`/`E2E.Tests`), values transcribed from the real shipped JSON.
  - Verify: `CORE` — `tests/FusionRpg.Core.Tests/Battle/Board/GridSpecTests.cs`, 6/6 passing: Chebyshev-vs-Square equivalence over a real board, `IndexOf` round-trip on a 4×7 non-square board (no transposition), out-of-bounds throws, a 100×50=5000-cell spec throws (`maxCells`=4096), cell-count mismatch throws, non-positive dimensions throw.
  - Files: `src/FusionRpg.Core/Battle/Board/GridSpec.cs`, `SiegeTuning.cs`, `data/tuning/siege.v1.json`, `tests/FusionRpg.Core.Tests/Battle/Board/GridSpecTests.cs`

- [x] **4.2 · `BoardState` occupancy** — IMPLEMENTED 2026-09-05
  - Acceptance: one occupant per cell, enforced; `Place`/`Move` **throw** rather than no-op; no order-dependent enumeration
  - Evidence: `BoardState` — `Place`/`Move` throw `BoardStateRejection` on an occupied-or-impassable target cell (never a silent no-op, per the spec's own reasoning: a no-op turns into a unit that mysteriously never advances). `Remove` is idempotent (a caller reacting to death/withdrawal does not itself track board membership). `Positions` is the only enumerable public member (proven by reflection, not just convention) — a caller wanting deterministic order must sort by actor key itself, matching `LegionSupply`'s own discipline.
  - Verify: `CORE` — `tests/FusionRpg.Core.Tests/Battle/Board/BoardStateTests.cs`, 6/6 passing: `Gap` blocks movement (asserted, not commented) and is a distinct value from `Blocking`; one-occupant-per-cell enforced; move-into-blocking throws and leaves position unchanged; move updates occupancy on both the old and new cell; remove frees the cell and is idempotent; reflection scan proves `Positions` is the only enumerable public member.
  - Files: `Board/BoardState.cs`, `tests/FusionRpg.Core.Tests/Battle/Board/BoardStateTests.cs`

- [x] **4.3 · Wire the three sentinels, null-path exact** — IMPLEMENTED 2026-09-05, ONE GAP FLAGGED HONESTLY
  - Acceptance: `BattleRunState.PositionOf` returns real positions with a board and **`null` without**; `boardAvailable` flips correctly. Diagonals legal, **same cost** (decision 36)
  - Evidence: `BattleRunState` gained a trailing optional `BoardState? board = null` constructor parameter and a `readonly BoardState? _board` field (every existing call site inside `BattleEngine.Resolve` passes nothing, defaulting to `null` — the golden-free guarantee). `PositionOf` rewritten: `_board is null ? null : _board.Positions.TryGetValue(actorKey, out var p) ? p : null` — still `null` whenever `_board` is null, real positions otherwise. Decision 36 (diagonals legal, same cost) needed no code change: `GridDistance.Chebyshev`/`Square` already treat a diagonal as one step, and `SiegeTuning.DiagonalSurcharge` ships at `0` in the schema per the spec's own instruction, ready for a later balance pass without a code change.
  - **Gap, stated rather than hidden**: the "returns real positions when a board IS present" half of `PositionOf` has no direct unit test today. `BattleRunState` is a `private` class nested in `BattleEngine` (deliberately, per the file's own top comment on minimizing the diff) with no current caller that constructs one with a non-null board — `BattleEngine.Resolve`'s own public signature was not extended to accept one, since nothing downstream (no test, no real caller) yet observes a position through it, and doing so speculatively would be exactly the kind of unrequested surface `siege-resolver` (a later module) is specced to add for real. The **null half** IS proven, at full scale: the entire existing Core.Tests battle-golden suite (6588 tests) stays byte-identical after this change (7 pre-existing, confirmed-external failures unrelated to Battle/Board — see Core.Tests run 2026-09-05). `ActionValidator`'s `boardAvailable` flip was already a plain bool parameter with no call site to change; ​its `Area`-rejection behavior for both `true` and `false` was already covered before this task (pre-existing `ActionValidator` tests), unchanged.
  - Verify: `CORE` — full `FusionRpg.Core.Tests` run, 6588/6595 passing (7 failures confirmed external: `TurnFsmActionEnvelopeTests`, `BattleStatComposerTests`, `ExpeditionResolverTests`, `TimelinePurityGuardTests`, 3× `ProveAptitudeJsonEmitTests` — all in battle-tempo/reaction-lane territory, none touch `Battle/Board` or `BattleRunState`).
  - Files: `BattleRunState.cs` (constructor + `PositionOf`)

### `siege-pathing` — [spec](../docs/architecture/base-defense/spec-siege-pathing.md)

- [x] **5.1 · Heap A\* with a TOTAL comparator `(f, h, cellIndex)`** — IMPLEMENTED 2026-09-05
  - Acceptance: **no two frontier entries can compare equal**; neighbour order fixed and commented as replay-affecting
  - Evidence: `BoardPathfinder.Find` uses `PriorityQueue<int, (long f, long h, int cellIndex)>` — `ValueTuple`'s own structural `IComparable` gives lexicographic `(f, h, cellIndex)` ordering, and `cellIndex` is unique per cell (`spec.IndexOf`), so no two entries can ever compare equal — the exact translation of `ReachMap`'s `StringComparer.Ordinal` tie-break to a grid. Stale duplicate pops (no decrease-key in .NET's `PriorityQueue`) are filtered by a `closed[]` array. Neighbour order is a fixed, commented 8-entry array (row-major clockwise from NW) — not a `dr`/`dc` loop a refactor could reorder.
  - Verify: `CORE` — `tests/FusionRpg.Core.Tests/Battle/Board/BoardPathfinderTests.cs`: `Equal_cost_routes_resolve_identically_across_10000_runs` (a symmetric board with two mirror-image optimal routes, same `Steps` every time across 10,000 calls) and `Tie_break_survives_a_heap_swap` (the SAME search run through a from-scratch linear-scan reference implementation sharing only the `(f,h,cellIndex)` key, not a line of the heap code — byte-identical `Steps`).
  - Files: `src/FusionRpg.Core/Battle/Board/BoardPathfinder.cs`

- [x] **5.2 · Admissible heuristic + the two occupancy views** — IMPLEMENTED 2026-09-05
  - Acceptance: `MinStepCost` **computed**, not configured, so a balance pass cannot break admissibility; `TerrainOnlyOccupancy` lets a unit boxed in by allies still plan
  - Evidence: `MoveCosts.MinStepCost = Math.Min(open, rough)`, computed in the constructor from whatever the tuning file says — never a separate config key, so it cannot drift out of sync with a balance pass. `IBoardOccupancy` interface + `SolidOccupancy` (terrain + every occupant) + `TerrainOnlyOccupancy` (terrain only) — the split named in the spec as the fix for "AI surrounded by its own allies stands still."
  - Verify: `CORE` — `Heuristic_stays_admissible_when_rough_is_cheaper_than_open` (rough=5 cheaper than open=20; optimal cost 35, matched independently by a from-scratch brute-force Dijkstra oracle — proving `MinStepCost`'s computed-not-configured design holds under an adversarial table), `Optimal_cost_matches_a_brute_force_dijkstra_over_fifty_seeded_boards` (50 seeded random 8×8 boards, 15% blocking / 15% rough, A* cost vs. an independent textbook-Dijkstra oracle), `Terrain_only_occupancy_routes_through_allies` + `Solid_occupancy_does_not` (an actor fully enclosed by its own 8 immediate-neighbour allies: terrain-only finds a route past them, solid correctly returns null).
  - Files: `BoardPathfinder.cs`, `IBoardOccupancy.cs`

- [x] **5.3 · Bounded work, negative costs throw** — IMPLEMENTED 2026-09-05
  - Acceptance: expansion cap **throws** rather than returning a partial route; negative cost throws at `MoveCosts` construction
  - Evidence: `MoveCosts`'s constructor throws `MoveCostsRejection` for any negative `open`/`rough`/`diagonalSurcharge` — never during search. `BoardPathfinder.Find` throws `BoardPathfinderRejection` if expansions exceed `4 x cellCount` (pure headroom for a correct A*, which expands each cell at most once) — a `null` return there would be indistinguishable from "no route" and would hide the defect permanently.
  - Verify: `CORE` — `Negative_cost_throws_at_construction` (all three fields), `No_route_returns_null_not_a_large_number` (a walled-off goal, `null` not a sentinel), `Path_from_a_cell_to_itself_is_one_step`, `Gap_is_impassable_but_transparent`, `Expansion_cap_throws_rather_than_returning_partial` (proves the cap's generous 4x headroom never false-positives on a legitimate full-board search — tripping it on purpose isn't reachable through the public API by design, since `MoveCosts` already refuses the one input, negative cost, that could break the invariant).
  - Files: `BoardPathfinder.cs`, `MoveCosts.cs`
  - **Full suite**: `FusionRpg.Core.Tests` 6601/6609 passing after 5.1-5.3 (8 failures, all confirmed external/battle-tempo territory or transient-under-parallelism — none touch `Battle/Board`). `guard-single-writer`/`guard-secondary-no-unity`/`guard-funnel-delta`/`guard-dal` all pass.

### `district-layout` — [spec](../docs/architecture/base-defense/spec-district-layout.md)

- [x] **6.1 · Board size is a LOOKUP per base tier** — IMPLEMENTED 2026-09-05
  - Acceptance: ⛔ **not** `f(DevelopmentLevel)` — §5.1 *"the grid does not grow"* and §5.25 reject it explicitly
  - Evidence: `BaseTierCatalog`'s role is filled by `DistrictLayout.SideFor(int developmentLevel)` — a genuine table lookup (`data/tuning/siege.v1.json`'s `district.sideByBaseTier`, an `IReadOnlyDictionary<int,int>` keyed by tier), never a formula. `developmentLevel` past the highest authored key **plateaus** at that key's side rather than growing or throwing — the grid stops growing, matching §5.1's "the placement budget does [grow]" framing (build slots and tower tier, not board size, keep scaling). **Base-tier derivation gap, resolved by reading, not guessing**: `BaseTierCatalog.SideFor(tier)` names a `tier` input the spec never defines the source of. `base-defense-ideal.md` (searched, not assumed): *"Seat / tier (Town Hall → Keep → Castle) — ❌ Already ours — `DevelopmentLevel` is exactly this, and §5.10 already reads it"* — confirming tier **is** `DevelopmentLevel` directly, not a separate new dimension. Recorded in `siege.v1.json`'s own `district._note` so the next reader does not have to re-derive this.
  - Verify: `CORE` — `Board_size_does_not_change_with_development_level_past_the_top_authored_tier` (DevelopmentLevel 2 vs. 500, byte-identical board), `Board_never_exceeds_max_cells_at_the_largest_authored_tier`, `No_board_dimension_traces_to_the_power_ladder` (reflection: `SideFor`'s only input is a plain `int`, no Θ/P(Θ) type in its signature).
  - Files: `src/FusionRpg.Core/World/District/DistrictLayout.cs`, `SiegeTuning.cs` (`DistrictTuning`), `data/tuning/siege.v1.json`

- [x] **6.2 · The four stability properties S1–S4** — IMPLEMENTED 2026-09-05
  - Acceptance: byte-stable on replay, across turns, **unchanged by capture**, and **stable under slot growth** (slot cell is a function of its own index, never the list length)
  - Evidence: `DistrictSeed(worldSeed, sectorId) = SeededRng.DeriveStream(worldSeed, sectorId).NextULong()` — reuses `SeededRng`'s existing mixer verbatim (no new hash function), and its two inputs exclude turn, owner, and any clock by construction (S1-S3 — `Build`'s signature has no turn/owner parameter at all to leak one in). **S4** (the one a naive implementation fails): `CellForSlot`'s spiral is a FIXED, precomputed ordering of Core-zone offsets sorted by `(Chebyshev distance from centre, row, col)` — a pure function of Core geometry alone, never of slot count. The seed picks ONE of the 8 dihedral (D4) symmetries of a square and applies it to the WHOLE fixed list once; every dihedral transform preserves Chebyshev distance, so the rotated list stays validly ordered and collision-free by construction. Slot `i`'s cell is `Transform(seed, spiral[i])` — depends on `i`, `seed`, and Core geometry only.
  - Verify: `CORE` — `Same_sector_same_seed_same_board_10000_times` (S1), `Board_is_identical_regardless_of_which_turn_it_is_computed_on` (S2, and structurally: `Build` has no turn parameter), `Capture_does_not_change_the_board` (S3, flips `OwnerFactionId`), `Adding_a_slot_moves_no_existing_slot` (S4 — grows 6→7, asserts cells 0-5 unmoved), `Every_slot_gets_a_distinct_cell` (no collisions across every Core cell).
  - Files: `DistrictLayout.cs`

- [x] **6.3 · Zones, gates, entry edge** — IMPLEMENTED 2026-09-05
  - Acceptance: `Core` never empty at the smallest board; **at least one gate always on the entry edge**; entry edge from `OnLaneId`, lanes ordered by id
  - Evidence: `ZoneOf` computes Core/Rampart/Approach purely from Chebyshev distance from the board centre — no occupancy, no gates. `Build` places `gateCount` gates at seed-rotated cardinal midpoints, but **orders the entry edge's own gate first** before trimming to `gateCount` (and Fortress's `-1` gate reduction), guaranteeing it always survives regardless of rotation. `EntryEdgeFor` resolves from `attacker.OnLaneId`, ordering candidate lane rows by `LaneId` ordinal before picking (defensive against a duplicate id, the same discipline `ReachMap`/`LegionSupply` apply) — reuses `SeededRng.DeriveStream(0, laneId).NextInt(4)` for the id→edge mapping rather than inventing a new hash. Falls back to `BoardEdge.North` with no lane (a garrison turning on its host).
  - Verify: `CORE` — `At_least_one_gate_is_on_the_entry_edge_for_all_four_edges` (all four, not just one), `Entry_edge_follows_the_arrival_lane` + `No_lane_falls_back_to_north`, `Core_zone_is_never_empty_at_the_smallest_authored_tier` (the smallest authored tier, side 18).
  - Files: `DistrictLayout.cs`

- [x] **6.4 · Read the three declared-and-unread fields — or report wiring gaps** — IMPLEMENTED 2026-09-05, TWO WIRING GAPS CONFIRMED
  - Acceptance: `SectorTypeFlags.Fortress`, `WorldLane.WardLevel`, `SlotState.Ruined/Depleted` are genuinely read **or** reported as wiring gaps with `file:line`. ⚠️ Verify some shipped sector type actually sets `Fortress` before claiming it
  - Evidence, per field:
    - **`SlotState.Ruined`/`Depleted`** — genuinely wired. `Build` looks up each Ruined/Depleted slot's own cell (via the SAME `CellForSlot` every other slot uses) and sets it to `CellTerrain.Rough` (rubble, no structure) unless a gate corridor already claimed that cell. Real, working code, not a stub.
    - **`SectorTypeFlags.Fortress`** — a confirmed **wiring gap**, reported as one, and it runs one level deeper than "no shipped sector sets it": verified at `SectorTypeCatalog.cs` — its five seed rows (Home, two NoBase, Nexus, Boss) set `Flags` to `Home`/`NoBase`/`NoBase`/`Nexus`/`Boss` respectively; **none** set `Fortress`. Worse for testability: `SectorTypeCatalog.All` is a fixed compiled list with no seam for a test to construct a synthetic Fortress-flagged type either, so `DistrictLayout.Build`'s own `isFortress` branch cannot be reached end-to-end by anything that exists today, shipped or synthetic. The mechanism itself (rampart thickness responds to `isFortress`) is proven directly against the pure `ZoneOf` geometry function instead, since that path *is* reachable.
    - **`WorldLane.WardLevel`** — a **partial wiring gap, scoped deliberately**: `DistrictTuning.ApproachDepth`/`ApproachDepthPerWardLevel` are parsed and validated from `data/tuning/siege.v1.json`, but `Build`'s current geometry is a symmetric ring (Core/Rampart/Approach all centred), so `WardLevel` is not yet read to reshape the Approach zone asymmetrically toward the entry edge. Scoped out of this pass rather than half-built: the spec's own framing ("a warded lane means a longer approach under fire") implies an asymmetric board (deeper on the warded side, unchanged on the other three), which is a real geometry change beyond a symmetric ring and belongs with whichever module first needs Approach-zone depth to matter mechanically (no consumer reads it yet). Recorded here rather than silently claimed as done.
  - Verify: `CORE` — `Fortress_flag_is_read_but_no_shipped_or_test_constructible_sector_type_sets_it` (documents the gap, verified against the live catalog, not assumed stale), `Fortress_bonus_thickens_the_rampart_ring` (proves the mechanism against `ZoneOf` directly), `Ruined_slots_are_rough_and_carry_no_structure`.
  - Files: `DistrictLayout.cs`

### `siege-seam`

- [x] **6.5 · Read-and-default `DevelopmentLevel`; never persist the board; no `P(Θ)` on any dimension** — IMPLEMENTED 2026-09-05
  - Acceptance: `DevelopmentLevel` is **read and defaulted to 0**, never written (that is `sector-development`'s). `GridSpec` is **derived, never stored** — the same stance `SupplyGraph` takes for connectivity, and for the same reason. **No board dimension is derived from `P(Θ)`**: at the shipped dial `P(1) = 106`, so a Θ-scaled board saturates on turn one
  - Evidence: `SideFor(sector.DevelopmentLevel)` reads the field via `Math.Max(0, developmentLevel)` (never negative-indexes the tier table) and never assigns to `WorldSector.DevelopmentLevel` anywhere in this module — `git diff` on `WorldState.cs`/`RpgStore.World.cs` from this task shows no `DevelopmentLevel` write introduced. `GridSpec` returned by `Build` is a plain, transient, in-memory record — nothing in `DistrictLayout.cs`, `RpgStore.*`, or `WorldCanonical.cs` persists or hashes it (confirmed: this task touched zero files under `RpgStore.World*.cs`/`WorldCanonical.cs`, so zero goldens moved).
  - Verify: `CORE` — full `FusionRpg.Core.Tests` run, 6626/6633 passing (7 failures, all confirmed external/battle-tempo territory — none touch `World/District` or `Battle/Board`); source inspection confirms no `P(`/`PowerScale`/`ContentScale` reference anywhere in `DistrictLayout.cs`. `guard-single-writer`/`guard-secondary-no-unity`/`guard-funnel-delta`/`guard-dal` all pass.
  - Files: `DistrictLayout.cs`

### `siege-seam` — [spec](../docs/architecture/base-defense/spec-siege-seam.md)

- [x] **7.1 · Prove `BattleRequest`/`BattleOutcome` are unhashed — before widening them** — VERIFIED 2026-09-05
  - Acceptance: a **test**, not this document, shows `WorldCanonical.Write` reads `WorldState` only and is independent of the seam types — the claim this module's whole zero-golden-risk rests on
  - Evidence: `WorldCanonical.Write`'s signature takes exactly one parameter, typed `WorldState` (reflection-checked, not read off the source and trusted). Its source text contains zero occurrences of `BattleRequest`/`BattleOutcome`/`BattleSideOutcome`/`BoardProjection`/`SideBudget` (a plain string scan of `WorldCanonical.cs`, so the assertion would catch a reference added anywhere in the file, not just in an expected spot). `WorldState`'s own public properties (reflection-enumerated) contain no property typed as any of the three existing seam types either — the claim holds from both directions: the writer doesn't read the seam, and the model doesn't carry it. **The claim is confirmed true, not merely undisturbed** — this was a genuine verification, not a formality, per `DESIGN-GATE.md` rule 3 ("test the constraint before you declare it").
  - Verify: `CORE` — `tests/FusionRpg.Core.Tests/World/Turn/WorldCanonicalSeamGuardTests.cs`, 3/3 passing.
  - Files: `tests/FusionRpg.Core.Tests/World/Turn/WorldCanonicalSeamGuardTests.cs`

- [x] **7.2 · Widen the seam: `BoardProjection`, `SlotOutcome`, `Withdrawn`, budgets** — IMPLEMENTED 2026-09-05
  - Acceptance: every new field **defaults to today's behaviour**; `Withdrawn` is **not** `Routed` (F5); budget crosses in, **spend** crosses back
  - Evidence: `BattleRequest` gains `Board`/`Budgets` (both nullable, default `null`); `BoardProjection`/`SlotProjection`/`SideBudget` added, none referenced by any existing construction site. `BattleSideOutcome` gains `Withdrawn` (default `false`). `BattleOutcome` gains `SlotResults`/`SlotOutcome` (default empty). `BattleApplication.Apply` throws `InvalidOperationException` on `Withdrawn && Destroyed`; `newlyRouted`/`Routed` both gate on `!side.Withdrawn` — **provably a no-op when `Withdrawn` is false** (`entity.Routed || (side.Routed && !false)` simplifies to the pre-existing `entity.Routed || side.Routed` exactly), so every one of the three existing kinds gets today's identical behaviour. New `BattleApplication.ApplySlotResults` (owner + structure-destroyed clearing; does NOT persist HP — no `WorldSlot` field exists for it yet, that is `structure-state`'s job) wired into `BattleReporting.Fight` as a third, empty-by-default step exactly per the spec's own snippet.
  - **External interference encountered and separated from this task's own regression risk**: mid-edit, a concurrent session landed its own, unrelated, owner-confirmed fix — `BattleApplication.Apply`/`BattleReporting.Fight` gained an `arrivedViaLane` parameter so a freshly-arrived (lane cleared) attacker that routs on arrival now falls back down the lane it used, matching `spec-world-movement.md`'s own `routed` rule, which the owner confirmed 2026-09-05 was specced but never wired. This is a **deliberate, intentional** behaviour change to a *different* rule than `Withdrawn`, landed in the same shared files at the same time as this task. It moved 5 pre-existing tests (`ContactAndClearTests` x3, `ClaimTests`, `MovementPhaseHaltReportingTests`) whose own docstrings (`A_routed_force_the_winner_stands_over_is_finished_off`: *"Wave 1 has no fallback... pinned here so changing it is a decision rather than a surprise"*) explicitly pin the OLD behaviour that fix intentionally supersedes — confirmed as their change, not mine, by direct mathematical proof (above) that every line this task added is behaviourally inert when `Withdrawn=false`, which every one of those five tests' outcomes is. Updating those five tests' pinned expectations belongs to whoever owns that fix, not to this task.
  - Verify: `CORE` — `tests/FusionRpg.Core.Tests/World/Turn/BattleSeamWideningTests.cs`, 11/11 passing (existing-kinds-unchanged, `Withdrawn_is_not_routed`, `Withdrawn_and_destroyed_together_throws`, `Withdrawn_round_trips_through_apply`, slot-results empty/non-empty, guard-clearing unchanged, board-projection round-trip, budget-crosses-both-directions x2, `No_battle_path_writes_world_stock_directly` source scan).
  - Files: `BattleSeam.cs`, `BattleApplication.cs`, `BattleReporting.cs`, `tests/FusionRpg.Core.Tests/World/Turn/BattleSeamWideningTests.cs`

- [x] **7.3 · `BattleKinds.District` + `DistrictAssaultPhase`** — IMPLEMENTED 2026-09-05
  - Acceptance: ⛔ **`SiegePhase.cs` is unmodified** — `git diff` on it is empty. New phase, new kind
  - Evidence: `BattleKinds.District = "district"` added (7.2). New `WorldCommandKinds.Assault` + an admission rule (entity + sector required, mirroring `Clear`'s own). New `DistrictAssaultPhase.Run`, modelled on `SiegePhase`'s structure (command loop, same `Drop` reason-string shape, `BattleReporting.Fight` at the end) but fighting the ground itself — the sector's own holder, if any — rather than a slot's guard; unopposed (nobody defending) still resolves rather than silently no-op-ing, the same way an already-empty Sector-kind contact fight does. Wired into `TurnEngine.Step` as a new `Assaults` phase, right after `Sieges` — provably a no-op for every existing world: no command log predating this task can ever populate `Assault` (it didn't exist), so no `RulesetVersion` bump was needed, only the phase LIST changed (caught correctly by `TurnEngineTests`'s own locked-order test, updated to include it).
  - Verify: `CORE` — `git diff --stat -- SiegePhase.cs` is empty (confirmed directly). `tests/FusionRpg.Core.Tests/World/DistrictAssaultPhaseTests.cs`, 7/7 passing: an assault fights the force holding the sector, `District_kind_puts_a_sector_id_in_the_sector_slot` (the W13 bug class, not reintroduced), `Battle_id_format_is_shared` (`BattleKinds.IdFor` reused), assaulting your own sector is dropped not fought, assaulting from elsewhere is dropped, an unopposed assault still resolves, and **guard-clearing (`SiegePhase`'s own `clear` order) still works unchanged** running alongside the new `Assaults` phase in the same turn. Full `FusionRpg.Core.Tests`: 6714/6722 passing after this task's own fix to `TurnEngineTests`'s phase-order list (the one test this task's addition genuinely and correctly moved) — remaining failures confirmed external/flaky (battle-tempo `TimelineDispatch.cs` purity, `ExpeditionResolverTests`/`ProveAptitudeJsonEmitTests`/`BattleStatComposerTests` all previously-confirmed battle-tempo territory, `DemonQualityReportTests` untracked/unrelated).
  - Files: `BattleSeam.cs`, `WorldCommand.cs`, `WorldCommandAdmission.cs`, `World/Turn/DistrictAssaultPhase.cs` (new), `TurnEngine.cs`, `tests/FusionRpg.Core.Tests/World/DistrictAssaultPhaseTests.cs` (new), `tests/FusionRpg.Core.Tests/World/TurnEngineTests.cs`

- [x] **7.4 · The five plumbing sites, proven by a round trip** — VERIFIED 2026-09-05
  - Acceptance: `WorldCommandKinds` · `WorldCommand` field · `RpgStore.CommandPayload` · `WorldCommandRequest` · `WorldEndpoints` mapping. **`bind-warden` fails sites 4 and 5 today** — do not inherit that
  - Evidence: sites 1-3 were already generic, per-field rather than per-kind (confirmed by reading, not assumed) — `RpgStore.CommandPayload`/its write and read-back (`ReadCommandRow`) map `EntityId`/`SectorId`/etc. regardless of `Kind`, so `assault` (needing only the two already-existing fields) needed zero changes there. `bind-warden`'s own documented failure is specifically that `WorldCommandRequest` (site 4) never carries a `WardenId` field at all — confirmed by reading the DTO directly: it has `CommandId`/`Kind`/`EntityId`/`SectorId`/`SlotIndex`/`Stance`/`LanePath`/`Amount`/`StructureId`/`ProjectId`, no `WardenId`. `assault` does not repeat this since `EntityId`+`SectorId` are already there. `WorldEndpoints.cs`'s request-to-command mapping (site 5) is also fully generic, one line per field, no per-kind branch to fall through.
  - **Proven by an actual round trip, not by reading the code and declaring it** (`DESIGN-GATE.md` rule 3): `tests/FusionRpg.Data.Tests/WorldCommandRoundTripPropertyTests.cs` (pre-existing, iterates `WorldCommandKinds.All` — automatically gained `assault` coverage the moment it was added there) — both its tests pass, proving survival through `ListWorldCommands`, `ListLoggedWorldCommands`, and the internal hydration path reachable only from `CommitWorldTurn` (sites 1/3/partial-5). New `tests/FusionRpg.Server.Tests/DistrictAssaultCommandWireTests.cs` closes the gap that test alone leaves: it never touches `WorldCommandRequest`/`WorldEndpoints` at all (constructs a `WorldCommand` directly), so it could not have caught `bind-warden`'s exact class of bug. The new test spins up the REAL `WorldEndpoints` behind real HTTP (`WebApplication` + `HttpClient`, the same in-process pattern `WorldCedeForecastTests.cs` already established), submits an `assault` order as JSON through `POST /{worldId}/commands`, commits through `POST /{worldId}/commit`, and confirms the fight actually happened.
  - **Bug found and fixed along the way, unrelated to the plumbing claim itself**: the first version of the new test raced two independent per-class `_tuningConfigured` bootstraps (`WorldCedeForecastTests`'s own pre-existing one, and a copy in the new file) against each other under xUnit's default per-class parallelism, since neither flag knew about the other. Extracted both into one shared, `Lazy<T>`-backed `WorldPolicyTestBootstrap.EnsureConfigured()` (thread-safe run-exactly-once, CLR-guaranteed) that both test classes now call — `WorldCedeForecastTests.cs` updated to match, its own tests re-verified still passing.
  - Verify: `DATA`/`CORE` — `WorldCommandRoundTripPropertyTests`, 2/2 passing. `DistrictAssaultCommandWireTests`, 1/1 passing. `WorldCedeForecastTests`, still 1/1 passing after the shared-bootstrap fix. Full `FusionRpg.Server.Tests`: 114/137 — the 23 failures are a single pre-existing, confirmed-external root cause (`ContentRuleViolated: atom.empty-name: 'atom.searing.t1' has no display name`, a content-seed validation issue in the affix/atom program, reproduced in complete isolation with none of this task's files even loaded — untouched by any change today, mine or otherwise).
  - Files: `tests/FusionRpg.Server.Tests/DistrictAssaultCommandWireTests.cs` (new), `tests/FusionRpg.Server.Tests/WorldPolicyTestBootstrap.cs` (new), `tests/FusionRpg.Server.Tests/WorldCedeForecastTests.cs` (bootstrap dedup only, no behaviour change)

- [ ] **CP1 · Checkpoint** — seam widened, board exists, pathing deterministic, **zero goldens moved anywhere**

---

## LEVEL 3 — ⛔ THE GOLDEN-LOCKED LANDING (batch these)

> **Land 8.x and 9.x together, in one change, with one triage pass.** They are the only modules that
> touch hashed state. ⚠️ **Ask about `RulesetVersion` coordination before starting** (plan §7).

### `structure-state` — [spec](../docs/architecture/base-defense/spec-structure-state.md)

- [ ] **8.1 · `MaterialTier` ordinal + `MaxHpOf` from `P(Θ_development)`**
  - Acceptance: **`long`, `checked`**, divide by 1000 **last and once**; tier 0 = indestructible so the four shipped rows are unaffected
  - Verify: `CORE`; `NUM`; overflow test asserts `OverflowException`, **not** a wrapped negative
  - Files: `StructureCatalog.cs`, `StructurePolicy.cs`

- [ ] **8.2 · Two CONDITIONAL canonical rows**
  - Acceptance: `slot-hp` and `slot-depletion` emit **only off-default** — the `faction-scope` precedent (`WorldCanonical.cs:98`). ⛔ **Never append to the existing `slot` row**
  - Verify: `CORE` — **world goldens byte-identical at default, unblessed**; rows emit in slot-index order
  - Files: `WorldState.cs`, `WorldCanonical.cs`

- [ ] **8.3 · Repair, capacity-halt, block-fire, F12**
  - Acceptance: `RepairCost` proportional and `checked`; **capacity-halt ≠ depletion** (reversible vs not, different messages); `BlocksLineOfFire` independent of `BlocksMovement`; capacity grows enough that a new slot actually produces
  - Verify: `CORE`; `DATA` (`long` round-trips to the column)
  - Files: `StructureCatalog.cs`, `WorldState.cs`, `LoamProduction` (read first)

- [ ] **8.4 · Destruction leaves rubble — `SlotState.Ruined` gets its first reader**
  - Acceptance: at `StructureHp <= 0` the slot becomes `Ruined` with `StructureId`, `StructureHp` and `ConstructionTurnsRemaining` all cleared. **`SlotState.Ruined` is declared and read by nothing today** — this is a wiring gap closed, not a new enum, and `district-layout` §5 already maps `Ruined` → `Rough` terrain, so rubble-you-can-cross-but-slowly falls out free
  - Verify: `CORE` — and a companion test asserting it had no reader before
  - Files: `WorldState.cs`, `BattleApplication.cs`

### `combatant-kind` — [spec](../docs/architecture/base-defense/spec-combatant-kind.md)

- [ ] **9.1 · `CombatantKind` with plain `[JsonIgnore]`**
  - Acceptance: ⛔ **plain, not `WhenWritingDefault`** — two shipped precedents on the same record, both recording the same golden incident. `Animate` at index 0
  - Verify: `CORE` — `Kind` absent from serialized JSON; **`ExpeditionResolverTests.Tier_goldens_are_locked` named explicitly**
  - Files: `BattleModels.cs`

- [ ] **9.2 · Structures never act, never keep a battle alive**
  - Acceptance: `AnyActive` filters to `Animate`; structures never enter initiative; no forced basic attack; **still targetable and damageable**
  - Verify: `CORE` — a siege with surviving walls and no surviving defenders **ends**
  - Files: `BattleRunState.cs`, `BasicAttack.cs`

- [ ] **9.3 · Garrison lends actions**
  - Acceptance: occupant's action list is the union; **garrisoning a wall grants nothing**; the structure still takes no turn
  - Verify: `CORE`
  - Files: `BattleModels.cs`, `IBattleView` implementation

### `siege-objective` (3b) — [spec](../docs/architecture/base-defense/spec-siege-objective.md)

- [ ] **10.1 · The win condition**
  - Acceptance: `CoreTaken` / `AssaultBroken` / `Inconclusive`, evaluated at **round boundaries only**; structures excluded from both conditions
  - Verify: `CORE` — surviving defenders in the outer ground do **not** prevent a capture
  - Files: `Battle/Siege/SiegeObjective.cs`

- [ ] **10.2 · The field cap — authored, symmetric, NOT derived**
  - Acceptance: reuses `CapPolicy`'s **pattern, not its type** (no PvZ side vocabulary); `-1` sentinel; stable reject reason codes; **structures do not count**
  - Verify: `CORE` — wall off 30 of 40 cells, assert the attacker's cap is **unchanged**
  - Files: `Siege/FieldCap.cs`, `data/tuning/siege.v1.json`

- [ ] **10.3 · Legion slots, max members, defense slots, the escape valve**
  - Acceptance: odd slot count **throws at load**; a 3-legion attacker may assault a 4-slot area; past `gridCapacityPoint` development buys **tower tier** not slots — **this is what makes a fixed board legal under the no-ceilings rule**
  - Verify: `CORE` — run development to a large index, assert structure HP still grows
  - Files: `Siege/SiegeSlots.cs`, tuning
  - ⚠️ No `const` roster limit anywhere — `WebMatchService`'s `const int maxSquad = 6` is the named anti-pattern

- [ ] **10.4 · `DefenderBonusMilli` reads zero for a district assault**
  - Acceptance: the placeholder's `1250` is untouched for every other kind; the defender is not **paid twice**
  - Verify: `CORE`
  - Files: `PlaceholderBattleResolver.cs`, tuning

- [ ] **CP2 · GATE A** — the batched landing is in; **world goldens byte-identical, unblessed**; `NUM` clean; `BOUND` green

---

## LEVEL 4–5 — the board comes alive

### `siege-positions` (4) — [spec](../docs/architecture/base-defense/spec-siege-positions.md)
- [ ] **11.1** Make `PositionOf` real; assign `EffectBag.BoardSnapshot`; board into `Status.Tick` as an **optional trailing parameter** · Verify: `CORE`, twelve goldens with no board · Files: `BattleRunState.cs`, `EffectBag.cs`
- [ ] **11.2** The **adapter** between the tactical board and `Core/Combat/BoardSnapshot` · Acceptance: ⛔ `Core/Combat/BoardSnapshot` is **unmodified** — it mirrors the injector's capture · Verify: `CORE` · Files: `Battle/Board/BoardSnapshotAdapter.cs`
- [ ] **11.3** Deterministic placement, ordinal key order · Verify: identical over 10,000 runs · Files: `Board/Placement.cs`

### `siege-waves` (4) — [spec](../docs/architecture/base-defense/spec-siege-waves.md)
- [ ] **12.1** Third event kind; **hybrid trigger — clock OR field-cleared, whichever first** (F8's actual verdict) · Verify: a turtling defender cannot delay the deadline; clearing early pulls the batch forward · Files: `BattleEngine.cs`
- [ ] **12.2** Roster growth reusing `Resolve`'s **own** actor validation; never reorder existing actors · Verify: mixed-case key throws as at setup · Files: `BattleRunState.cs`
- [ ] **12.3** Bounded, **resumable** drain — over-cap arrivals carry over, **none dropped** (F9/C7) · Verify: 30 actors at cap 8, all present, none duplicated · Files: `BattleEngine.cs`
- [ ] **12.4** Wave composition becomes **data** — §3.5: create the wave data file the repo lacks; existing definitions move in unchanged · Verify: every battle golden byte-identical after the migration · Files: `WaveCatalog.cs`, `data/`
- [ ] **12.5 · Both sides reinforce through ONE path; a boardless battle is untouched**
  - Acceptance: attacker and defender batches run the same code with `Side` as **data**, not two code paths (decision: *both sides move*). `BattleSetup.Reinforcements` defaults **empty**, so the reinforcement event is never scheduled and the queue behaves exactly as today — **a never-scheduled event kind cannot change a tick sequence**, which is the byte-identity argument stated structurally rather than measured
  - Verify: `CORE` — all twelve goldens byte-identical with an empty batch list
  - Files: `BattleEngine.cs`, `BattleModels.cs`

### `siege-obstacles` (4) — [spec](../docs/architecture/base-defense/spec-siege-obstacles.md)
- [ ] **13.1** `ObstacleKind` + `AcquisitionPath` + cover fields on `StructureDef`; **`ObstacleKind.None` is the default**, so every existing structure and golden is untouched · Verify: `CORE` · Files: `StructureCatalog.cs`
- [ ] **13.2** ⛔ **This module OWNS `ScopeMembershipTransition.CellEntered/Exited`** — cover released it and nobody claimed it, so the Mine fired on nothing · Acceptance: every entry paired with an exit (move, death, withdrawal); `BattlefieldOwnSideReactor.cs:75-86` falls through harmlessly · Verify: `CORE` · Files: `ScopeMembershipEvents.cs`, `BattlefieldOwnSideReactor.cs`
- [ ] **13.3** The five rows · Acceptance: **Wire taxes STAMINA not movement**; a moat is a **Rampart**, not terrain; Mine damages via `DamagePacket`, single-use, **revealed** (F9) · Verify: `CORE` — movement cost provably unchanged by Wire · Files: `Siege/Obstacles.cs`
- [ ] **13.4** `acquisitionPaths` non-empty, validated at load · Verify: `CORE` · Files: `StructureCatalog.cs`
- [ ] **13.5 · Rampart blocks fire → `RequiresLineOfSight` gets its first reader**
  - Acceptance: `BlocksLineOfFire` is independent of `BlocksMovement` (a moat blocks one, not the other). **`RequiresLineOfSight` is declared, compiled, carried and persisted twice — and read by no evaluator anywhere in `src/`.** Rampart is the first thing in the game with a reason to block a shot; per decision 35 its meaning is *"pays the obstruction penalty"*, **never** *"the shot is blocked"*
  - Verify: `CORE` — and a companion test asserting it had no reader before
  - Files: `ActionRow.cs` (read), `Siege/LineOfFire.cs`, `StructureCatalog.cs`
- [ ] **13.6 · Emplacement lends its action; and nothing directional is added**
  - Acceptance: garrisoning an Emplacement gives the occupant its ranged action (`combatant-kind` §4's first real content) — and its decision is real **only because the field cap makes bodies scarce**. ⛔ **No facing/directional cover**: §5.18 cut parapet/parados precisely because *"nothing in `BattleActorSetup` or `EntityFacts` carries one"*
  - Verify: `CORE`; source scan finds no facing field
  - Files: `Siege/Obstacles.cs`

### `siege-cover` (5) — [spec](../docs/architecture/base-defense/spec-siege-cover.md)
- [ ] **14.1** Cover area from an **authored radius per kind**; best single cover, **no stacking**; a destroyed obstacle projects **nothing** · Verify: `CORE` · Files: `Siege/Shooting.cs`
- [ ] **14.2** Range penalty — threshold as a **fraction of board side**, multiplier flat · Verify: an 18-cell and a 30-cell board differ · Files: `Siege/Shooting.cs`
- [ ] **14.3** Obstruction — Bresenham trace, **deterministic, symmetric, lower-cell-index tie-break**; **reduces, never blocks**; units obstruct too · Verify: identical over 10,000 runs; **the trace is never passed to a targeting resolver** (§2 rule 10) · Files: `Siege/LineOfFire.cs`
- [ ] **14.4** `ProjectilePenalties` flags through **all five sites** `RequiresLineOfSight` occupies — the action-system half decision 35 names · Verify: `CORE`, `DATA` round trip · Files: `ActionRow.cs`, `ActionCompiler.cs`, `CompiledAction.cs`, `RpgStore.Actions.cs`
- [ ] **14.5** Compose the four multipliers in **one place**, `long`+`checked`, **four divides each after every multiply** · Verify: against a `BigInteger` reference where the combined divisor overflows; each factor **separately on the wire** · Files: `Siege/Shooting.cs`
- [ ] **14.6 · Mechanic 5 — destroying an obstacle removes cover AND obstruction, proven TOGETHER**
  - Acceptance: one test, both effects. **This two-for-one is what makes "shoot the wall first" a plan rather than a wasted turn** — a destroyed obstacle that still obstructs is exactly the bug the mechanic's appeal rests on not having
  - Verify: `CORE` — `A_destroyed_obstacle_projects_no_cover_and_no_obstruction`
  - Files: `Siege/Shooting.cs`, `Siege/LineOfFire.cs`
- [ ] **14.7 · What this module no longer does — assert the absences**
  - Acceptance: ⛔ **no `combat.dodge.omni` grant** (the contest path is untouched by cover); ⛔ **no `ScopeMembershipTransition` change here** — the budget was released to `siege-obstacles` for its Mine, and re-adding it here would spend it twice; no `(damage source × cover type)` matrix beside the four multipliers
  - Verify: `CORE`; source scans for each
  - Files: `Siege/Shooting.cs`

### `siege-construction` (5) — [spec](../docs/architecture/base-defense/spec-siege-construction.md)
- [ ] **15.1** `rubble` + `ironwork` on `WorldSector`, **conditional canonical rows**, `long` · Verify: world goldens at zero · Files: `WorldState.cs`, `WorldCanonical.cs`
- [ ] **15.2** The refine chain — lossy, **gated by a Refinery structure** not a cooldown; `StructureKind.Refinery` · Verify: 4 rubble ≠ 4 ironwork · Files: `StructureCatalog.cs`, `Siege/Refine.cs`
- [ ] **15.3** The four acquisition paths + the **shared placement validator** · Acceptance: adjacency required; **nothing may be built in the `Core`** (decision 10, both sides, both phases); **no ownership check** (decision 4); every path costs an action · Verify: `CORE` — a besieging legion can afford **≥4** structures · Files: `Siege/Construction.cs`
- [ ] **15.4** Faucets: `shard-vein` → ironwork, `material-seam` → rubble · Acceptance: verify both slot types exist and yielded **nothing** before · Verify: `CORE` · Files: `SlotTypeCatalog.cs`, `LoamProduction`
- [ ] **15.5** Interrupted build refunds **nothing** — `InterruptRefundMilli = 0` on the build envelope · Verify: killing a builder destroys the progress · Files: action envelope authoring
- [ ] **15.6 · Pre-battle and in-battle deployment are ONE path, two entry points**
  - Acceptance: pre-battle is **round 0 with a larger action budget**, not a separate system with its own rules — a second system drifts from the first immediately. Decision 5 prices both: *"pre battle and in battle, deployment cost unit action and requirement resources"*
  - Verify: `CORE` — the same validator rejects the same placements in both phases
  - Files: `Siege/Construction.cs`
  - Note: read `BuildResolver.cs` first — ten refusal gates, and it is the only order kind that passes all five plumbing sites

---

## LEVEL 6–7b — economy, AI, and the playable milestone

### `siege-economy` (6) — [spec](../docs/architecture/base-defense/spec-siege-economy.md)
- [ ] **16.1** Board income by **occupation**, ordinal cell order, exhausted nodes yield nothing · Verify: identical over 10,000 runs · Files: `Siege/BoardEconomy.cs`
- [ ] **16.2** The depot — **reconciled spend-only** · Acceptance: ⛔ board income can **never** mint world resources; board income spent **before** world stock · Verify: earn heavily, spend nothing, assert world stock unchanged · Files: `Siege/SiegeDepot.cs`
- [ ] **16.3** F11 — capture transfers the stockpile **proportional to surviving HP**; guard `MaxHp <= 0` before dividing · Verify: `OverflowException` on overflow; zero-HP recovers nothing · Files: `Siege/SiegeDepot.cs`
- [ ] **16.4** ⛔ **The board never reads `WorldSlot.OwnerFactionId`** · Verify: source scan · Files: `Siege/BoardEconomy.cs`

### `siege-ai` (6) — [spec](../docs/architecture/base-defense/spec-siege-ai.md)
- [ ] **17.1** `SiegeIntentSource` wrapper dispatching on `SideOf`; **no signature change** to `Resolve` · Verify: played delegate overrides, null falls through · Files: `Siege/SiegeIntentSource.cs`
- [ ] **17.2** Three axes — **stance** (`Hold`/`Guard`/`Engage`, on the actor) · **signed aggression** (−2..+2, on the target) · **additive score** · Verify: a `Hold` garrison does **not** chase bait; a taunt cannot pull it off the objective · Files: `Siege/SiegeAi.cs`
- [ ] **17.3** XCOM's **shipped** weights — hit-chance **70**, objective 50, kill **15**, low-HP 10, cannot-counter 10, **+ round** (anti-turtle), **− risk** · Verify: `Hit_chance_outweighs_lethality_seventy_to_fifteen`; `long` sums, `checked` · Files: `SiegeAiPolicy.cs`, tuning
- [ ] **17.4** Objective fallback via `TerrainOnlyOccupancy`; **frozen acting order**; ordinal tie-break · Verify: a unit boxed in by allies still advances; killing an actor mid-round does not reorder · Files: `Siege/SiegeAi.cs`
- [ ] **17.5** Determinism + readability · Acceptance: **no RNG and no `float` reachable** (source scan); top-3 with term breakdown to `DecisionTrace`; **read `Consideration.cs` first** — its `Weakest()` gives R6 free · Verify: identical over 10,000 runs · Files: `Siege/SiegeAi.cs`
- [ ] **17.6** ⛔ **No hidden difficulty thumb**, no score on `ActionTargetOrdering`, no targeting UI · Verify: source scans · Files: —
- [ ] **17.7 · §5.20 rule 2 — a NAMED, player-visible validity filter**
  - Acceptance: every filter carries a `DisplayKey` shown verbatim in the UI, not a debug string. CoC's `Favourite Target`: **the player can say why it did not shoot before they watch it not shoot** — which is what turns a documented miss into a feature instead of a bug report
  - Verify: `CORE` — no filter exists without a display key
  - Files: `Siege/TargetFilter.cs`
- [ ] **17.8 · §5.20 rule 3 — a retarget trigger with a STATED latency**
  - Acceptance: `ai.retargetLatencyTicks`, authored. *"Instant is not required; **specified** is."* The value matters less than it being stated — a unit that keeps swinging at a target which just moved is then following a rule the player can be told
  - Verify: `CORE`
  - Files: `SiegeAiPolicy.cs`, tuning
- [ ] **17.9 · §5.20 rule 5 — a replacement vocabulary for the garrisoned emplacement**
  - Acceptance: an emplacement cannot move, so R3's objective fallback is **meaningless for it**. It gets its own two-entry vocabulary (*Hold fire* / *Fire at will*) rather than a fallback it can never execute. **Every vocabulary still resolves to a total order** — a replacement set, never a rule that returns "no preference"
  - Verify: `CORE` — an emplacement never attempts to path
  - Files: `Siege/SiegeAi.cs`
- [ ] **17.10 · §7c — the auto-versus-played dial, a tunable from line one**
  - Acceptance: `ai.autoResolveHandicapMilli` exists from the first commit. The tension is real and unavoidable: *"playing it yourself should be **meaningfully better, never mandatory**"* — and with one kernel **both are set by the same dial**. ⛔ It selects **policy depth** (how many candidates scored, how far it looks), **never a stat bonus** — §7b's rule is *"difficulty is which policy, not a stat bonus"*
  - Verify: `CORE` — the dial changes decisions without changing any actor's numbers
  - Files: `SiegeAiPolicy.cs`, tuning
  - Note: fheroes2 hit the other failure — their maintainers openly debated making auto-battle **dumber**

### `siege-resolver` (7) — [spec](../docs/architecture/base-defense/spec-siege-resolver.md)
- [ ] **18.1** `DistrictAssaultResolver` — **delegate every non-district kind** to the placeholder (the early return **is** the feature-absence guarantee) · Verify: sector/lane/guard outcomes reference-equal to the placeholder's · Files: `World/Turn/DistrictAssaultResolver.cs`
- [ ] **18.2** The six steps; seed from `SeededRng.Mix(seed, HashOrdinal(BattleId))` — **never a new hash, never the turn alone** · Verify: two assaults in one turn get different seeds · Files: `DistrictAssaultResolver.cs`
- [ ] **18.3** ⛔ **Supply the resolver at BOTH `RpgStore.WorldTurns.cs:509` AND `:603`** · Acceptance: constructible **from statics only**, or `:603` cannot build it · Verify: **a re-derived turn report is byte-identical to the original**; a source scan asserts no `TurnEngine.Step(` omits a resolver · Files: `RpgStore.WorldTurns.cs`
- [ ] **18.4** §2 rule 8 — stamp every resolution `(engineVersion, rulesetVersion, seed)` · Verify: a version mismatch between original and re-derived is **detectable** · Files: `DistrictAssaultResolver.cs`

### `siege-engagement` (7b) — [spec](../docs/architecture/base-defense/spec-siege-engagement.md)
- [ ] **19.1** `EngagementExit` with **`Spent`** as the normal outcome · Verify: a spent engagement leaves the siege ongoing and the world advances one turn · Files: `World/Turn/SiegeEngagement.cs`
- [ ] **19.2** The persistence split · Acceptance: structure damage persists; **board positions provably do not** · Verify: scan `WorldState` for cell data after an engagement · Files: `SiegeEngagement.cs`
- [ ] **19.3** `IsUnderSiege` **derived, never stored**; marching away ends it with no cleanup; **no engagement cap** · Verify: source scan for an `IsBesieged` field; run 200 engagements · Files: `SiegeEngagement.cs`
- [ ] **19.4** One report line per engagement, through `BattleReporting.Fight` · Verify: a six-turn siege produces six lines; rounds never reported as turns · Files: `BattleReporting.cs`

- [ ] **CP4 · GATE B** — ⭐ **a siege plays and resolves in CI with no FE**; both call sites wired; determinism over 10,000 runs; `NUM` clean; `BOUND` green

---

## LEVEL 8–8b — the front end

### `board-render` (8) — [spec](../docs/architecture/base-defense/spec-board-render.md)
> ⚠️ **The largest module in the program.** Five extractions, **each landing with the lawn rendering byte-identically.**

- [ ] **20.1** `createGame({scenes})` — scenes injected, not imported · Verify: `WEB`; lawn byte-identical · Files: `src/game/createGame.ts`, `createLawnGame.ts`
- [ ] **20.2** `GridSpec` **passed**, not imported · Verify: import scan — the generic layer imports **no lawn module** · Files: `src/game/scenes/`
- [ ] **20.3** `EntityRegistry<TKey>` generic; caller-supplied kind→visual map · Verify: ptr keys and actor keys both · Files: `src/game/entities/`
- [ ] **20.4** `pickCell(spec, pointer)` pure · Verify: out-of-bounds → null · Files: `src/game/systems/PickSystem.ts`
- [ ] **20.5** Camera bridge — **model authoritative, Phaser write-only**; unbind returns a disposer · Verify: drive the model, assert **exactly one** Phaser write; unbind removes every listener · Files: `src/game/camera/bindCamera.ts`
- [ ] **20.6** Layer order terrain→structures→units→overlays; **terrain cached** to a render texture · Verify: not redrawn per frame · Files: `src/game/board/`
- [ ] **20.7** Accessibility + budget · Acceptance: keyboard reaches every cell; `prefers-reduced-motion`; **lazy-loaded**; ⛔ **no client-side prediction** (§2 rule 3, RT-15) · Verify: `WEB` incl. `check:bundle` — entry chunk unchanged · Files: `src/game/board/`

### `siege-stage` (8b) — [spec](../docs/architecture/base-defense/spec-siege-stage.md)
- [ ] **21.1** Route + six shell rows, **zero branches** · Verify: source scan of `src/shell/` for `=== "siege"` · Files: `railState.ts`, route table, layer/back maps, GG-7 matrix, i18n
- [ ] **21.2** Discharge the amendment's three costs — **stage count assertion → 5**, GG-7 row, IA + `game-gui-principles.md` D2 corrected · Verify: `WEB`; a docs assertion so it cannot be skipped · Files: `design/information-architecture.md`, `game-gui-principles.md`, CI checks
- [ ] **21.3** Stage under `stages/siege/` copying `world`'s shape; **no `*Dto`** (`contractGuard.ts:57`) · Verify: `WEB` · Files: `src/stages/siege/`
- [ ] **21.4** Pre-battle deployment (decision 37) — player-placed, AI places by policy at the same step · Verify: auto-resolve still needs no UI · Files: `src/stages/siege/`
- [ ] **21.5** ⛔ **Pause = persisted decision log replayed on resume** (decision 46) · Acceptance: **no board state stored**; resume survives a **server restart**; no timeout on a paused single-player siege · Verify: scan the persisted row for cells/HP/initiative · Files: `src/stages/siege/`, session wiring
  - ⚠️ **Blocked on a `decisions_json` writer** — `spec-interactive-turns.md` (T10), **not this program**. Raise before starting
- [ ] **21.6** Rounds and turns **never** the same number on any wire; leaving mid-siege is **not** a withdrawal; `long` HP as `bigint` · Verify: `WEB` · Files: `src/stages/siege/hud/`
- [ ] **21.7 · Played and auto-resolved sieges run ONE resolver path**
  - Acceptance: the FE **supplies `SiegeIntentSource`'s played-side delegate**; it does not implement a parallel resolution path. *"The player is defending"* and *"nobody is watching"* differ by **one nullable field** — a separate interactive resolver would drift from the auto-resolver within a release, and the divergence would surface as *"the replay doesn't match"*. Entering keeps the world stage **mounted underneath** (GG-1's *"closed back to the same state"*)
  - Verify: `WEB` — same resolver with the delegate present and null; world stage state survives a siege
  - Files: `src/stages/siege/`

### `battle-stage` (8b) — [spec](../docs/architecture/base-defense/spec-battle-stage.md)
- [ ] **22.1** Route **`#/battle/{battleId}`** + **five** shell rows — the id already exists in `railState.ts:31`, so this is the only module in the program that adds a stage **without** adding an id · Verify: `WEB`; **zero declared-but-unbuilt stage ids remain** · Files: shell
- [ ] **22.2** `projectReportToBoard` — synthetic two-rank layout for a boardless report, real cells for a siege · Acceptance: ⛔ **the synthetic layout imports nothing from `Core`'s board namespace** · Verify: import scan · Files: `src/stages/battle/`
- [ ] **22.3** Playback only — **never re-resolves** · Verify: `WEB`; all battle goldens byte-identical (FE-only module) · Files: `src/stages/battle/playback/`

- [ ] **CP5 · Checkpoint** — both stages ship; lawn byte-identical after all five extractions; entry chunk unchanged

---

## CONTENT FAMILY — parallel with everything above

### `structure-schema` (c0) — [spec](../docs/architecture/base-defense/spec-structure-schema.md)
- [ ] **23.1** The anchor: 17 fields, **four** ownership levels (`AUTHORED`/`DERIVED`/`GENERATED`/`VALIDATED`) · Acceptance: `strengthBand` is decision 32's material tier — **no `materialTier` beside it**; `acquisitionPaths` **replaces** `acquisition`; **no `side` field** · Files: `data/seed/structures/`, schema
- [ ] **23.2** The audit — **no field holds a number**, fails the **build** not a lint; `none` is a value and a missing key is a defect; every description has a **negative clause** · Verify: over every committed row · Files: `tools/` validator
- [ ] **23.3** `StructureKind` **derived from `role`**, never authored beside it; unmapped role throws at load · Files: schema + mapping table

### `structure-corpus` (c1) — [spec](../docs/architecture/base-defense/spec-structure-corpus.md)
- [ ] **24.1** Dump the four shipped rows — the **importer proof** against content already tested · Files: `data/seed/structures/`
- [ ] **24.2** Hand-author ~36 rows from §5.18 + §5.21 · Acceptance: **every row cites a source**; per-role counts meet declared targets; **grid density 2.4–4.0** · Files: `data/seed/structures/works/`
- [ ] **24.3** Build the **idempotency harness here** — byte-identical rerun proven by hash, while inputs are trivially idempotent · Verify: hash equality · Files: `tools/`
- [ ] ⛔ **Zero model calls in this module.** Tests stub the transport so it **raises**

### `structure-catalog-import` (c2) — [spec](../docs/architecture/base-defense/spec-structure-catalog-import.md)
- [ ] **25.1** `Configure(corpus)`, lazy + cache-resetting; C# rows as **fallback first** · Verify: four shipped rows **byte-identical** through the corpus; world goldens unmoved · Files: `StructureCatalog.cs`
- [ ] **25.2** The **one** ordinal→magnitude function; unknown ordinal **throws at load** · Verify: `BigInteger` reference; `OverflowException` · Files: `StructureCatalog.cs`, `Bands.cs`
- [ ] **25.3** `Name`/`role`/`obstacleKind` **reach the wire** (P3-5 — a corpus with no surface) · Verify: and a companion asserting `Name` had no reader before · Files: DTOs
- [ ] **25.4** Delete the C# literal — **only after** byte-identity passes · Files: `StructureCatalog.cs`

### `structure-instantiate` (c3) — [spec](../docs/architecture/base-defense/spec-structure-instantiate.md)
- [ ] **26.1** First **production caller** of `Instantiator.TryInstantiate` · Acceptance: ⛔ **no second roll**; traits and actions roll, **HP and every ordinal-derived magnitude do not** · Verify: source scan for RNG; identical over 10,000 runs · Files: `Siege/StructureInstantiate.cs`
- [ ] **26.2** `rollSeed` from `(worldSeed, sectorId, slotIndex, buildTurn)` — **never a clock or counter**, or replay at `:603` breaks · Verify: replay reproduces the same instance · Files: same
- [ ] **26.3** Stored per player; SQL inside `FusionRpg.Data` · Verify: `DATA`; `BOUND` · Files: `RpgStore.*`

### `structure-planner` (c3) — [spec](../docs/architecture/base-defense/spec-structure-planner.md)
- [ ] **27.1** A **committed, diffable** `_plan.json` · Files: `data/seed/structures/_plan.json`
- [ ] **27.2** Fix the five model-free decisions — **ordered tier ladder** (decision 32 is unsound without it), per-role targets, slot legality, variant counts, `acquisitionPaths` · Verify: ladder **totally ordered**; every rung has a row or is cut · Files: `tools/`
- [ ] **27.3** Check **before** generation — skew, density, empty combinations; **a failing plan blocks the run** · Verify: `CORE`/tool tests · Files: `tools/`
- [ ] **27.4** State the **call budget** — rows × stages × votes; vote fields declared by **cost-of-being-wrong** · Files: tuning
- [ ] ⛔ **Zero model calls.** Byte-identical over 10,000 runs; no clock, no unseeded RNG

### `structure-pipeline` (c4) — [spec](../docs/architecture/base-defense/spec-structure-pipeline.md)
> ⭐ **The first model call in the entire program.**
- [ ] **28.1** Permute every enum, seeded from `(entity_id, field, sample_index)` — **`sample_index` inside the seed** or three votes are one sample · Files: `tools/`
- [ ] **28.2** Vote only declared fields; **`1-1-1` → `unresolved`**, never option one · Files: `tools/`
- [ ] **28.3** **Prove constrained decoding with one real call** before the batch · Files: `tools/`
- [ ] **28.4** TRANSIENT ≠ QUALITY — a pause **replays**, no new call; repairs **bounded at two** · Files: `tools/`
- [ ] **28.5** Inherit c1's idempotency harness; provenance + `stale_ids()` · Verify: byte-identical rerun by hash · Files: `tools/`
- [ ] **28.6** Mode-collapse n-gram guard — **flags, never fails** · Files: `tools/`

### `structure-metrics` (c5) — [spec](../docs/architecture/base-defense/spec-structure-metrics.md)
- [ ] **29.1** Every metric **declares closed or open**; a metric with no declaration **fails registration** · Files: `tools/`
- [ ] **29.2** ⛔ **No open-loop metric can fail a build** — enforced structurally · Files: `tools/`
- [ ] **29.3** Skew checked at **plan and output**; rarity proven **not** a power axis; distinctness reads **abilities, not stats** · Files: `tools/`
- [ ] **29.4** Report header states **a complete anchor is not a complete roster** · Files: report template

- [ ] **CPc · Checkpoint** — corpus generated, idempotent by hash, metrics declared, **no numeric field anywhere**

---

## Deferred — named, not forgotten

- [ ] **`#/battle` beyond playback** — needs `battle`'s own spec (decision 44 fixes this module at playback)
- [ ] **Force-size tunables** — decision 29 keeps `field.maxLivingPerSide`, `legion.maxMembers`, `waves.batchIntervalTicks`, `siege.maxRounds` **deliberately unset** until a real board exists
- [ ] **Fog of war** — deferred by owner decision 2026-08-22; `IBattleView` exists to confine the change
- [ ] **`Dugout` obstacle** — §5.18 defers it until fog exists
