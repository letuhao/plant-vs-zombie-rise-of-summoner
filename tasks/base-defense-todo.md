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

### `battle-clock-profile` — [spec](../docs/architecture/base-defense/spec-battle-clock-profile.md)

- [ ] **1.1 · Move `MaxRounds`/`RoundDurationMs` onto `BattleModeProfile`**
  - Acceptance: nullable on `BattleTuning.ProfileRow`; **null means inherit the ruleset**, which is what keeps `classic-round` byte-identical
  - Verify: `CORE` — **all twelve goldens byte-identical, unblessed** (8 battle + 4 expedition). `ClassicRound`, `GalaxySync` and `HybridAtb` all resolve unchanged, because each inherits the ruleset horizon by naming neither field
  - Files: `BattleModeProfile.cs`, `BattleTuning.cs`, `BattleModels.cs`

- [ ] **1.2 · `BattleEngine` reads the profile, not the ruleset**
  - Acceptance: zero reads of `BattleRuleset.MaxRounds`/`.RoundDurationMs` remain in `BattleEngine`; the `(long)` widen at `:240` is unchanged; `Shields.Tick` uses the profile's round
  - Verify: `CORE`; grep asserts zero remaining reads
  - Files: `BattleEngine.cs` (4 sites: `:240`, `:251`, `:460`, `:476`)

- [ ] **1.3 · `MaxLoopIterations` becomes profile-derived**
  - Acceptance: reproduces **200,000 exactly** at `classic-round`'s 50 rounds, or it is a behaviour change wearing a refactor's clothes
  - Verify: `CORE`; explicit equality test
  - Files: `BattleEngine.cs`, `BattleTuning.cs`

- [ ] **1.4 · Add the `siege` profile row — three lines**
  - Acceptance: a row in `BattleModeProfileCatalog`, one arm in `Resolve`, one entry in `ModeProfileArchitectureTests.KnownProfileIds`. **`points: false`** (one action per *activation*; `action-map.md:430` — *"no Action Points"*). `WScope.PerSide`, `OrdersBySpeed`, `RequiresLiveInput`, `ForecastExactness.Exact`
  - Verify: `CORE` — `ModeProfileArchitectureTests` green **with no new file exemption**
  - Files: `BattleModeProfileCatalog.cs`, `ModeProfileArchitectureTests.cs`, `data/tuning/battle.v*.json`
  - ⚠️ The economy was got wrong **twice** from opposite directions. Read the spec's §5 before touching it

- [ ] **1.5 · `Resolve` behaviour and the jitter statement**
  - Acceptance: `BattleModeProfileCatalog.Resolve("siege")` returns the cached row (`Assert.Same` twice) and its horizon is **separately settable**; an **unknown id still throws** — *"content did not choose"* and *"content chose wrong"* stay different failure modes. **Jitter needs no new field**: `OrdersBySpeed` + `ForecastExactness.Exact` already *is* the no-jitter statement (F6), and adding a field to express "off" where off is the only behaviour would be a claim rather than a feature
  - Verify: `CORE` — `Resolve("sieg")` throws; a tuning `maxRounds: 120` changes only siege battles, with a `classic-round` tick count asserted unmoved
  - Files: `BattleModeProfileCatalog.cs`

### `siege-supply` — [spec](../docs/architecture/base-defense/spec-siege-supply.md)

- [ ] **2.1 · Split `Usable` into `Traversable` and `Source`**
  - Acceptance: a besieged sector is **not traversable** but **is** a source, so it supplies itself. Fixes **F1** and **F1b** together
  - Verify: `CORE` — besieging a faction's only Seat isolates **one sector, not the faction**
  - Files: `SupplyGraph.cs`

- [ ] **2.2 · Read `TerritoryComponents.For` and either cite it as correct or fix it**
  - Acceptance: a besieged sector draws on **its own stock only**. ⛔ **This box may not be ticked by assumption** — the spec says read it
  - Verify: `CORE` — a neighbour's full granary does not feed a besieged sector
  - Files: `TerritoryComponents.cs` (read; change only if needed)

- [ ] **2.3 · `supply.besieged:` report line + rationing dial**
  - Acceptance: distinct from `supply.cut:`; `BesiegedRationMilli` defaults **1000 (no-op)** so the defect fix stays separately verifiable
  - Verify: `CORE` — `Ration_at_1000_reproduces_the_unrationed_fix_exactly`
  - Files: `SupplyGraph.cs`, `LegionSupply.cs`, `data/tuning/loam.v*.json`

- [ ] **2.4 · §7 cost 6 — slot ownership follows sector capture**
  - Acceptance: a captured sector's slots change owner. ⚠️ **May move a golden** — measure against every shipped world first; if it does, batch with level 3
  - Verify: `DATA`, `CORE`; world goldens
  - Files: `ClaimResolver.cs`

- [ ] **2.5 · `ConnectedSectors` stays uncached, and no `IsBesieged` field is added**
  - Acceptance: still recomputed **every turn**, never cached. Its own comment is the reason: a stored flag *"is exactly the kind of derived state that goes stale the first time a lane is cut, and it would then be **wrong in the one situation the player cares about**"* — and a siege **is** that situation
  - Verify: `CORE`; source scan asserts no `IsBesieged` field on `WorldSector` and no memoisation in `SupplyGraph`
  - Files: `SupplyGraph.cs`, `WorldState.cs` (scan only)

### `world-graph-diff` — [spec](../docs/architecture/base-defense/spec-world-graph-diff.md)

- [ ] **3.1 · MEASURE first — this is the whole task**
  - Acceptance: turn-commit cost attributed across clear / write-by-table / `slots_json` / `SqliteCommand` construction, published under `docs/research/perf/`
  - Verify: the benchmark runs on an 18-sector × ~20-slot world
  - Files: `tests/FusionRpg.Bench/`, `docs/research/perf/`
  - ⛔ **Steps 3.2/3.3 are cancelled if statement reuse dominates.** Record that outcome explicitly

- [ ] **3.2 · (conditional) Prepared-statement reuse**
  - Acceptance: read-back hash unchanged; no logic or schema change
  - Verify: `DATA`; world goldens
  - Files: `RpgStore.World.cs`

- [ ] **3.3 · (conditional) Diffing writer + equivalence guard**
  - Acceptance: `WorldCanonical.Hash(readBack) == WorldCanonical.Hash(next)` over **500 randomised mutations**; **DELETE handled** for slot, entity and lane
  - Verify: `DATA`; `BOUND` (guard-dal)
  - Files: `RpgStore.World.cs`

---

## LEVEL 1–2 — the board and the seam

### `siege-board` — [spec](../docs/architecture/base-defense/spec-siege-board.md)

- [ ] **4.1 · `GridSpec` + `CellTerrain`**
  - Acceptance: row-major, `IndexOf` round-trips on a **non-square** board; `Gap` blocks movement not sight; `maxCells` enforced loudly and commented as a **structural** cap
  - Verify: `CORE`
  - Files: `src/FusionRpg.Core/Battle/Board/GridSpec.cs`

- [ ] **4.2 · `BoardState` occupancy**
  - Acceptance: one occupant per cell, enforced; `Place`/`Move` **throw** rather than no-op; no order-dependent enumeration
  - Verify: `CORE`
  - Files: `Board/BoardState.cs`

- [ ] **4.3 · Wire the three sentinels, null-path exact**
  - Acceptance: `BattleRunState.PositionOf` returns real positions with a board and **`null` without**; `boardAvailable` flips correctly. Diagonals legal, **same cost** (decision 36)
  - Verify: `CORE` — **all twelve goldens byte-identical with no board**
  - Files: `BattleRunState.cs:407`, `ActionValidator` call site

### `siege-pathing` — [spec](../docs/architecture/base-defense/spec-siege-pathing.md)

- [ ] **5.1 · Heap A\* with a TOTAL comparator `(f, h, cellIndex)`**
  - Acceptance: **no two frontier entries can compare equal**; neighbour order fixed and commented as replay-affecting
  - Verify: `CORE` — `Equal_cost_routes_resolve_identically_across_10000_runs`
  - Files: `Board/BoardPathfinder.cs`

- [ ] **5.2 · Admissible heuristic + the two occupancy views**
  - Acceptance: `MinStepCost` **computed**, not configured, so a balance pass cannot break admissibility; `TerrainOnlyOccupancy` lets a unit boxed in by allies still plan
  - Verify: `CORE` — optimal cost matches a brute-force Dijkstra on 50 seeded boards
  - Files: `BoardPathfinder.cs`, `IBoardOccupancy.cs`

- [ ] **5.3 · Bounded work, negative costs throw**
  - Acceptance: expansion cap **throws** rather than returning a partial route; negative cost throws at `MoveCosts` construction
  - Verify: `CORE`
  - Files: `BoardPathfinder.cs`, `MoveCosts.cs`

### `district-layout` — [spec](../docs/architecture/base-defense/spec-district-layout.md)

- [ ] **6.1 · Board size is a LOOKUP per base tier**
  - Acceptance: ⛔ **not** `f(DevelopmentLevel)` — §5.1 *"the grid does not grow"* and §5.25 reject it explicitly
  - Verify: `CORE` — raising development leaves the grid **byte-identical**
  - Files: `World/District/DistrictLayout.cs`, `data/tuning/siege.v1.json`

- [ ] **6.2 · The four stability properties S1–S4**
  - Acceptance: byte-stable on replay, across turns, **unchanged by capture**, and **stable under slot growth** (slot cell is a function of its own index, never the list length)
  - Verify: `CORE` — one test per property. **S4 is the one a naive implementation fails**
  - Files: `DistrictLayout.cs`

- [ ] **6.3 · Zones, gates, entry edge**
  - Acceptance: `Core` never empty at the smallest board; **at least one gate always on the entry edge**; entry edge from `OnLaneId`, lanes ordered by id
  - Verify: `CORE`
  - Files: `DistrictLayout.cs`

- [ ] **6.4 · Read the three declared-and-unread fields — or report wiring gaps**
  - Acceptance: `SectorTypeFlags.Fortress`, `WorldLane.WardLevel`, `SlotState.Ruined/Depleted` are genuinely read **or** reported as wiring gaps with `file:line`. ⚠️ Verify some shipped sector type actually sets `Fortress` before claiming it
  - Verify: `CORE`
  - Files: `DistrictLayout.cs`

### `siege-seam`

- [ ] **6.5 · Read-and-default `DevelopmentLevel`; never persist the board; no `P(Θ)` on any dimension**
  - Acceptance: `DevelopmentLevel` is **read and defaulted to 0**, never written (that is `sector-development`'s). `GridSpec` is **derived, never stored** — the same stance `SupplyGraph` takes for connectivity, and for the same reason. **No board dimension is derived from `P(Θ)`**: at the shipped dial `P(1) = 106`, so a Θ-scaled board saturates on turn one
  - Verify: `CORE` — world goldens unmoved (nothing here is hashed); source scan finds no `P(` or `PowerScale` in the module
  - Files: `DistrictLayout.cs`

### `siege-seam` — [spec](../docs/architecture/base-defense/spec-siege-seam.md)

- [ ] **7.1 · Prove `BattleRequest`/`BattleOutcome` are unhashed — before widening them**
  - Acceptance: a **test**, not this document, shows `WorldCanonical.Write` reads `WorldState` only and is independent of the seam types — the claim this module's whole zero-golden-risk rests on
  - Verify: `CORE`
  - Files: `tests/.../WorldCanonical` scan test

- [ ] **7.2 · Widen the seam: `BoardProjection`, `SlotOutcome`, `Withdrawn`, budgets**
  - Acceptance: every new field **defaults to today's behaviour**; `Withdrawn` is **not** `Routed` (F5); budget crosses in, **spend** crosses back
  - Verify: `CORE` — world goldens byte-identical; `Withdrawn_and_destroyed_together_throws`
  - Files: `BattleSeam.cs`, `BattleApplication.cs`

- [ ] **7.3 · `BattleKinds.District` + `DistrictAssaultPhase`**
  - Acceptance: ⛔ **`SiegePhase.cs` is unmodified** — `git diff` on it is empty. New phase, new kind
  - Verify: `CORE` — guard-clearing still works unchanged
  - Files: `BattleSeam.cs`, `World/Turn/DistrictAssaultPhase.cs`

- [ ] **7.4 · The five plumbing sites, proven by a round trip**
  - Acceptance: `WorldCommandKinds` · `WorldCommand` field · `RpgStore.CommandPayload` · `WorldCommandRequest` · `WorldEndpoints` mapping. **`bind-warden` fails sites 4 and 5 today** — do not inherit that
  - Verify: `DATA` — submit through the API, commit, read back, assert survival. **A round trip, not a checklist**
  - Files: the five sites (read `BuildResolver.cs` first — it passes all five)

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
