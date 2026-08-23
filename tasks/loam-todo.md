# Tasks: loam and the Fracture — the pre-gate build

Plan: [loam-plan.md](loam-plan.md) · Design: [empire-economy-ssot.md](../docs/architecture/empire-economy-ssot.md) · Map: [loam-map.md](../docs/architecture/loam-map.md)
**Gate: PASSED — specs sealed, build authorized 2026-08-23.** Start at L1.

Standing rules for every task: integer math only · stable ordering · no wall clock or unowned RNG in
`Step` · SQL only in `FusionRpg.Data` · **git hands-off** — leave the work in the tree, mark the task
done, hand over a commit message and the paths touched.

---

## Phase 1 — state (`loam-model`)

- [x] **L1: The rootbed slot type** *(2026-08-23 — done. `SlotKind.Rootbed` appended last; catalog
  row `Buildable=true, Yields=true`; wired into `AllowedSlotTypes` on every sector type where
  `CanHostSeat` (homeworld, stable, rich, warcamp, nexus, boss-lair) — not barren/storm, matching
  SSOT §3 "rootbed: settle from anywhere, rare, the prizes" against `NoBase` ground. New test file
  `RootbedSlotTests.cs`: enum-position guard, catalog-row properties, the `AllowedSlotTypes`
  invariant asserted for every sector type (not spot-checked), and a golden-move guard — asserts
  `first-light` places no rootbed slot yet and its canonical text contains no "rootbed", so this
  wave provably moves no hash. **Found and fixed a real regression while verifying**:
  `FusionRpg.Core.World.Ai.SlotValueCatalog` (AI valuation) also switches exhaustively over every
  `SlotKind` and rejected the new member at startup — appending to `SlotKind` was not as free as
  the model-layer plan assumed. Added `[SlotKind.Rootbed] = 750` (ranked below Seat=1000, above the
  700-tier producers — SSOT calls it more foundational than a producer, short of the anchor itself),
  covered by that catalog's own exhaustive test. Verified: Core.Tests full suite 2691/2691, Data.Tests
  421/421, Guard.Tests 61/61, E2E.Tests `~World` 34/34 — all green, no golden moved.)*
  - Description: `SlotKind.Rootbed` **appended** to the enum, its `SlotTypeCatalog` row (`Buildable`, `Yields`), and `rootbed` added to `AllowedSlotTypes` on the sector types that may carry one.
  - Acceptance: `SlotKind.Rootbed` is the **last** enum member, asserted by a test so a future insert fails loudly; the catalog self-validates; no world uses it yet, so **no hash and no golden moves** — assert that too.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `Core/World/SlotTypeCatalog.cs`, `Core/World/SectorTypeCatalog.cs`, tests. Scope: S.
  - Dependencies: none.

- [x] **L2: The three fields, the canonical row, and `first-light`'s minimum — the program's first
  golden move** *(2026-08-23 — done. `WorldSector.LoamStock` (`long`), `WorldSector.FractureIntensityMilli`
  (`int`, default 1000), `WorldFaction.UpkeepHandicapMilli` (`int`, default 1000) added; all three
  wired into `WorldCanonical.Write`'s sector/faction rows. `first-light`'s homeworld gets a rootbed
  slot (G-D) and `LoamStock = 500` (G-A placeholder — L9's harness decides the real number).
  `RulesetVersion` unchanged (asserted). New test file `LoamFieldsTests.cs`: determinism, the G-D
  minimum edit, each new field proven part of the canonical hash by mutation, defaults match the
  pre-loam world. **Exactly one golden re-bless**, in `WorldWaveOneAcceptanceTests.GoldenFinalHash`
  — captured the real post-change hash rather than hand-editing one, reason recorded as entry #8 in
  that file's re-bless log (the established style there). Found on running Data.Tests: 8 more
  failures, all the *same* root cause — `RpgStore` doesn't persist the three new fields yet, so a
  save/reload of `first-light` silently drops the homeworld's starting stock, breaking every
  store-vs-engine replay-parity test. Diagnosed as L4's job, not a defect in L2 — continuing straight
  into L4 rather than leaving the tree red at a task boundary.)*
  - Description: `WorldSector.LoamStock` (**`long`**) + `FractureIntensityMilli` (`int`), `WorldFaction.UpkeepHandicapMilli`, all three into `WorldCanonical`; `first-light` gains a homeworld rootbed, a starting stock and an authored intensity (**G-D** — validation in L3 would otherwise reject the only existing world).
  - Acceptance: two builds from the same `(template, seed)` are canonically identical; **exactly one golden re-bless, reason recorded on the constant**; the store-versus-engine replay assertion still passes across it; `RulesetVersion` **unchanged** — this adds state, not behaviour.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `Core/World/WorldState.cs`, `WorldCanonical.cs`, `WorldTemplateCatalog.cs`, tests. Scope: M.
  - Dependencies: L1.

- [x] **L3: Validation rules 9–13** *(2026-08-23 — done as four rule methods, not five: rule 3
  ("rootbed only where `AllowedSlotTypes` permits") is deliberately not new code — it's the existing
  `Rule6SlotShape`, already extended by L1's catalog change, per the spec's own words. Added
  `Rule9FractureIntensityBounded`, `Rule10LoamStockNonNegative`, `Rule11HomeworldHasARootbed`,
  `Rule12HandicapBounded`, plus `WorldValidation.MaxIntensityMilli=3000` (matches the spec's already-
  decided value; also cleaned up two stale leftover "open question" bullets in
  `spec-loam-model.md` that re-asked questions the same section had already answered two bullets
  above — no-manufactured-uncertainty), `MinHandicapMilli=1`, `MaxHandicapMilli=3000` (new,
  documented in the spec's Decided section). New test file `LoamValidationTests.cs`: a rejecting
  case per rule plus a boundary-is-legal case for the intensity ceiling, and one test confirming the
  *existing* slot-shape rule already rejects a rootbed on barren ground. Core.Tests `~World`:
  484/484 green.)*
  - Description: intensity in `[0, MaxIntensityMilli=3000]`; `LoamStock >= 0`; a `rootbed` only where `AllowedSlotTypes` permits; the homeworld carries at least one rootbed; handicap in range. Rules 1–8 already exist — these append.
  - Acceptance: **each rule has its own rejecting case**; every rejection names the offending id in the message; no golden moves (validation refuses, it does not rewrite).
  - Verify: Core filtered `~World`.
  - Files: `Core/World/WorldValidation.cs`, tests. Scope: S.
  - Dependencies: L2.

- [x] **L4: Persistence and migration** *(2026-08-23 — done. `EnsureColumn` added for
  `rpg_world_sectors.loam_stock`/`fracture_intensity_milli` and
  `rpg_world_factions.upkeep_handicap_milli`, established style (additive, not in `CREATE TABLE`, so
  an existing database gets them via `ALTER TABLE`, matching the `routed`/`on_lane_toward_sector_id`
  precedent). Wired into `WriteWorldGraphUnlocked`'s INSERTs and `LoadWorldState`'s SELECTs.
  **This is what actually closed the 8 Data.Tests failures L2 surfaced** — every one was the same
  root cause (homeworld's authored `LoamStock` silently dropped on save because the columns didn't
  exist), not 8 separate bugs. New test file `LoamPersistenceTests.cs`: non-default values on all
  three fields round-trip (deliberately non-default on every field, since first-light alone only
  varies `LoamStock` from its type default and would not have caught a miswired
  intensity/handicap column), and the legacy-row migration reads back as exactly the pre-loam world
  (0 / 1000 / 1000) — `EnsureColumn`'s ALTER-when-missing mechanics are already covered generically
  elsewhere (`WebMatchStoreTests`), so this proves the *value*, not the SQL primitive.
  **Second re-bless discovered and corrected**: the L2 golden captured *before* this task landed had
  actually blessed a lossy round trip (`Play()` commits every turn through `RpgStore`, so the
  original stock was already gone by turn one) — re-captured the true hash after persistence was
  fixed and rewrote the log entry to say so, rather than leaving a technically-passing but wrong
  bless in place. `guard-dal` green. Data.Tests full suite: 423/423 green.)*
  - Description: `EnsureColumn` for the three columns in the established style; read/write in `RpgStore.World.cs`.
  - Acceptance: create → reload → deep-equal including all three; **an existing pre-loam row migrates to `stock 0 / intensity 1000 / handicap 1000`**, which is exactly the pre-loam world; `guard-dal` green.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World`; `.\scripts\guard-dal.ps1`.
  - Files: `Data/Sqlite/RpgStore.World.cs`, `RpgStore.cs` schema block, tests. Scope: M.
  - Dependencies: L2.

- [x] **L5: Fog — intensity is terrain, stock is live** *(2026-08-23 — done.
  `IntelSnapshot.FractureIntensityMilli` added (default 1000, matching the sector default),
  populated unconditionally in `IntelRecorder.Snapshot` alongside `Climate`/`DangerBand` — terrain,
  captured on any sighting. `WorldSectorDto.FractureIntensityMilli` added and wired in
  `WorldEndpoints.ProjectSector` from `believed`, never from raw `sector` truth (the file's existing
  pattern for every other field). `LoamStock` deliberately has no belief field and no DTO field at
  all — gating by omission rather than a conditional check, so there is no code path that could leak
  it. New tests: `LoamFogTests.cs` (Core) — intensity matches truth when scouted, survives after
  sight is lost, an unseen sector (given a deliberately loud non-baseline true value) has **no
  belief entry at all**, and a reflection sweep over every belief type confirms none exposes a
  `LoamStock`-named property; `WorldLoamFogE2ETests.cs` (E2E) — no faction's `/state` JSON ever
  contains `loamStock` (including the owner), a scouted sector's intensity reaches the wire, an
  unseen sector reports the baseline (noted honestly: `first-light` gives every sector the same
  baseline intensity today, so this wire-level check alone can't distinguish "withheld" from
  "coincidentally equal" — the Core test with the loud value is what actually proves the boundary).
  **Found and fixed the expected fixture-drift regression**: the new DTO field moved
  `web/.../first-light.json`, caught by `WorldFixtureTests`; re-blessed with
  `FUSIONRPG_BLESS_WORLD_FIXTURE=1`. Verified: Core.Tests `~World` 488/488, E2E.Tests `~World`
  37/37, web suite 292/292 — all green.)*
  - Description: `FractureIntensityMilli` into the intel snapshot (remembered once scouted); `LoamStock` **never** projected to a non-owner and **never** remembered as a stale value; DTO gating.
  - Acceptance: a scouted sector's intensity survives in belief; **no faction ever receives another's stock** — asserted as a **property over every projection** (`/state`, turn report, intel), following W22's shape, not spot-checked on one endpoint.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `Core/World/Intel/IntelRecorder.cs`, `FactionIntel.cs`, `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, tests. Scope: M.
  - Dependencies: L4.

### Checkpoint 1 — the state exists ✅ PASSED 2026-08-23
- [x] All suites green; all four guard scripts OK. (Core.Tests 2709/2709, Data.Tests 423/423,
  Guard.Tests 61/61, E2E.Tests 180/180, web suite 292/292; guard-single-writer,
  guard-secondary-no-unity, guard-funnel-delta, guard-dal all OK.)
- [x] **Exactly one golden re-bless** (L2+L4 — see `WorldWaveOneAcceptanceTests.GoldenFinalHash`
  entry #8), reason on the constant. No other golden literal exists in the suite (confirmed: the
  other store-vs-engine parity tests compare computed-vs-computed at runtime, nothing hardcoded).
- [x] `RulesetVersion` unchanged — asserted directly (`LoamFieldsTests.This_wave_adds_state_not_behaviour...`).
- [x] A pre-loam saved world still loads and plays — `LoamPersistenceTests.An_existing_pre_loam_row_migrates...`
  plus the full 20-turn `WorldWaveOneAcceptanceTests` scenario passing.

---

## Phase 2 — arithmetic, wired to nothing (`loam-calc`)

- [x] **L6: `TerritoryComponents` — the load-bearing four lines** *(2026-08-23 — done.
  `Core/World/Loam/TerritoryComponents.cs`: flood-fill over sectors a faction owns, edges from
  `SupplyReach.LinksOf` (reused, not reimplemented — same edge rule, different traversal). Two
  overloads: `For(WorldState, factionId)` (truth) and `For(IEnumerable<string>, IReadOnlyList<Link>)`
  (belief-safe rows). Ascending-id iteration means the component list is correctly ordered by lowest
  member with no extra sort. New test file `TerritoryComponentsTests.cs` against a hand-built a-b-c-d
  fixture (not `first-light`): one unbroken chain, a severed lane producing two components, an
  unowned neighbour never joining, order-invariance under reversed sectors/lanes, a no-holdings
  faction gets zero components, an isolated sector is its own singleton, both overloads agree on the
  same data, and — the distinction the spec insists on — a fixture with no Seat anywhere shows
  `SupplyGraph.ConnectedSectors` empty while `TerritoryComponents` still finds the real block, proving
  they're genuinely different questions rather than one being a subset of the other. Core.Tests
  `~Loam`: 28/28. Full suite: 2718/2718.)*
  - Description: connected components of a faction's held sectors; stable-ordered sets, collection ordered by lowest member id. Two overloads from the start: `WorldState`, and rows/ids alone for the belief side.
  - Acceptance: a severed territory yields **two** components; reversing sector order changes neither contents nor order; an unowned sector never joins one; **both overloads agree on the same data**; a test states explicitly that this is *not* `SupplyGraph.ConnectedSectors` (different seeds, different question).
  - Verify: Core filtered `~Loam`.
  - Files: `Core/World/Loam/TerritoryComponents.cs`, tests. Scope: S.
  - Dependencies: L2.

- [x] **L7: `LoamPolicy`, `LoamProduction`, `LoamUpkeep`** *(2026-08-23 — done. `LoamPolicy.cs`:
  every constant (`SeepPerTurn=50`, `BaseUpkeepPerSector=10`, `GarrisonUpkeepPerMember=2`,
  `DevelopmentUpkeepPerLevel=5`, `DangerUpkeepPerBand=3`) explicitly marked provisional — L9's
  harness tunes them, not this file. `LoamProduction.For`: unconditional per-rootbed seep, no chain
  gate (S3), G-B (unowned = 0) enforced in the shared row overload so both call paths get it for
  free. `LoamUpkeep.For`: `long sum * intensityMilli * handicapMilli / 1_000_000`, one division;
  the truth overload enforces both G-B (unowned = 0) and **G-C** (a faction with no rootbed anywhere
  in its territory is exempt entirely, mirroring `SupplyGraph.cs`'s no-Seat exemption) — placed here
  rather than deferred to L8's `LoamBalance` since it's a per-sector truth-overload concern once you
  have `world.Sectors` in hand, and the acceptance criterion named it as this task's. New test files
  `LoamProductionTests.cs`, `LoamUpkeepTests.cs`: ordering-invariance, unowned = 0, the G-C exemption,
  intensity at 500/1000/2000 = half/exact/double, the handicap scaling the same way, a `1,000,000`-
  garrison boundary case that would overflow `int` at the ceiling multipliers (3000×3000) and
  doesn't, both overloads agreeing. Core.Tests `~Loam`: 40/40. Full suite: 2730/2730.)*
  - Description: every constant in one file with its reasoning (the `MovementPolicy` precedent); production summing rootbed seep, **no chain gate**; upkeep = `(base + garrison + f(development, danger)) × intensity × handicap`, **no distance term** (A3). Both overloads each.
  - Acceptance: intensity 500/1000/2000 gives half / exactly the unmultiplied sum / double; **no overflow at the largest legal `(sum, intensity, handicap)` triple** — quantities are `long` so the expression promotes without a cast, one division; a boundary test because this fails silently into *negative* upkeep rather than crashing; **unowned sectors produce and cost nothing** (G-B); **a faction with no loam source anywhere is skipped entirely** (G-C, mirroring `SupplyGraph.cs:18`); multiply-before-divide, divide once; ordering-invariant.
  - Verify: Core filtered `~Loam`.
  - Files: `Core/World/Loam/{LoamPolicy,LoamProduction,LoamUpkeep}.cs`, tests. Scope: M.
  - Dependencies: L6.

- [x] **L8: `LoamBalance`, `FadePolicy`, `Habitability`** *(2026-08-23 — done.
  `LoamBalance`: `PerSector`/`PerComponent`/`PerFaction`, each summing the layer below (`PerFaction`
  = Σ over `TerritoryComponents`' output of `PerComponent`, reusing L6 rather than re-deriving
  ownership). `FadePolicy`: `RecoveryMilli=20` fixed, `BaseDecayMilli=40` (always > recovery, even
  at a one-unit shortfall), scaling to `MaxDecayMilli=300` — all `long` until the final clamp so a
  huge deficit can't overflow past the ceiling instead of hitting it. `Habitability`: unchanged
  wording, rootbed-only wave-1 source set. New test files `LoamBalanceTests.cs`, `FadePolicyTests.cs`,
  `HabitabilityTests.cs`: production-minus-upkeep by hand, ordinary ground running a deficit (§12.4
  asserted), **the severed-territory claim built on L6's fixture** — a rich half (+80) and a poor
  half (−62) subsidise each other while connected (+18 combined) and stop the moment the lane
  between them is severed, recovery strictly slower than decay at every depth, stability clamped to
  [0,1000], habitability true/false/no-slots, belief-overload parity throughout. Core.Tests `~Loam`:
  53/53. Full suite: 2743/2743.)*
  - Description: balance per sector / per component / per faction; fade where recovery is **strictly slower** than decay and both are graded; habitability = holds at least one loam source.
  - Acceptance: **a fixture of ordinary ground runs a deficit** — SSOT §3's central claim asserted, not believed; recovery slower than decay, asserted as an inequality; a sector with a rootbed is habitable and one without never is; ordering-invariant.
  - Verify: Core filtered `~Loam`.
  - Files: `Core/World/Loam/{LoamBalance,FadePolicy,Habitability}.cs`, tests. Scope: M.
  - Dependencies: L7.

- [x] **L9: The economy harness (map finding A9)** *(2026-08-23 — done.
  `EconomyHarnessTests.cs`: a self-contained turn loop (deliberately not `TurnEngine` — the module's
  own boundary, "nothing wired yet"), against a hand-built two-faction fixture (not `first-light`),
  mirroring what `loam-turn` will do for real: per-sector capped accrual, per-component pooled draw
  (proportional, ordinal remainder — the SSOT's stated rule), unpaid upkeep tracked as a harness-local
  shortfall rather than a negative stock. Added `LoamPolicy.LoamCapacity=300` (anticipated by
  spec-loam-model but not yet consumed anywhere — L9 is its first real user, L12 its second).
  **Found and fixed a real bug while building the harness**: capping via
  `Math.Min(capacity, before + nominal)` claws back stock a fixture authored *above* the cap (a poor
  sector starting at 500 against a 300 ceiling), producing negative "realized production" for a
  sector that made nothing — fixed to throttle only the increment (`before + min(room, nominal)`),
  a mistake `loam-turn`'s Production phase (L12) would otherwise have been one copy-paste away from
  repeating. The two required assertions: net flow is not monotone positive for either faction over
  100 turns (the rich faction's flow is genuinely positive-then-zero once its cap binds — proving
  the cap matters, not just asserting it), and deficit share (4 of 7 sectors, 57%) stays above a
  0.5 floor. Income-growth, yield-concentration and binding-frequency are printed as output only,
  per the spec — the last is honestly noted as **not yet measurable at all** (no action reads loam
  until L20's Abandon rule), rather than a fabricated placeholder. Core.Tests full suite: 2746/2746.)*
  - Description: given template, seed and turn count, replay and report net flow per faction per turn, deficit share, binding frequency, income-vs-upkeep growth, yield concentration. **Test-shaped, not a dashboard.**
  - Acceptance: net flow is **not monotone positive** over a long run, and deficit share stays above a floor — these two are **assertions**, the rest is output; the run is deterministic.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~EconomyHarness`.
  - Files: `tests/FusionRpg.Core.Tests/World/Loam/EconomyHarnessTests.cs`. Scope: M.
  - Dependencies: L8.

- [x] **L10: Mutants for `loam-calc`** *(2026-08-23 — done. `scripts/mutants/loam-calc.json`: 16
  mutants across all six calculators plus `LoamPolicy.DevelopmentAndDangerUpkeep`'s formula shape.
  **First run: 15/16 caught, one survivor** — "upkeep divides twice, rounding early"
  (`sum*I/1000*H/1000` instead of `sum*I*H/1_000_000`) survived because every existing test used
  round multiplier values (500/1000/2000/3000) where the two shapes coincidentally land on the same
  floored integer. Added `The_formula_divides_only_once_not_once_per_multiplier` with deliberately
  ugly inputs (333, 777) that make single- vs double-truncation diverge (4 vs 3) — a real gap the
  mutant caught, not a rubber-stamp. Re-run: **16/16 caught** on a verified-green baseline.
  `.\scripts\coverage.ps1 -Namespace FusionRpg.Core.World.Loam`: **94% on `LoamUpkeep`** first pass —
  the truth overload's `world.Entities` garrison-counting lambda had never actually run (every
  fixture in `LoamUpkeepTests` had zero entities). Added
  `A_standing_garrison_raises_the_truth_overloads_upkeep` with a real `WorldEntity`/`WorldEntityMember`
  standing in the sector; **100% line and branch across all seven classes** on re-run. (Noted, not
  fixed: `mutate.ps1` itself exits code 1 even on a full "every mutant was caught" — a pre-existing
  quirk in the shared script inheriting the last per-mutant `dotnet test` exit code, not something
  this task's scope covers; the authoritative signal is the printed verdict, not the process exit
  code.) Core.Tests full suite: 2749/2749. Guard.Tests: 61/61.)*

### Checkpoint 2 — the tables exist and nothing has an opinion ✅ PASSED 2026-08-23
- [x] Every calculator proven against a hand-built fixture with an answer workable on paper — not
  `first-light` (L6-L9 all use bespoke fixtures; the economy harness's own fixture is likewise
  hand-built, not `first-light`).
- [x] **No golden moved and `RulesetVersion` unchanged.** L6-L10 touch nothing `TurnEngine` calls;
  no test in this phase re-blessed a golden.
- [x] Every mutant caught on a verified-green baseline (16/16, second run — first run's honest
  survivor fixed with a real test, not a weakened mutant).
- [x] Both overloads of every calculator agree, by test (`TerritoryComponents`, `LoamProduction`,
  `Habitability` each have a dedicated belief/truth parity test; `LoamUpkeep`'s and `LoamBalance`'s
  row overloads are what the truth overload delegates to, exercised directly by every row-level test).
- [x] A5 is a measurement, not a claim (L11, closed earlier this session: 8/16/32/64/128 all measured).
  - Description: a `scripts/mutants/loam-calc.json` set covering every calculator, plus a coverage pass.
  - Acceptance: **every mutant caught, on a verified-green baseline** — the script already refuses a red one, after an earlier "all 22 caught" turned out to be 22 build failures from a concurrent stream; any survivor gets an explanation next to the code or a new test.
  - Verify: `.\scripts\mutate.ps1 -Set loam-calc`; `.\scripts\coverage.ps1 -Namespace FusionRpg.Core.World.Loam`.
  - Files: `scripts/mutants/loam-calc.json`, tests. Scope: S.
  - Dependencies: L9.

- [x] **L11: Measure `ReconnectionCost` at scale (map finding A5)** *(2026-08-23 — done. The
  concurrent stream's blocker (`StatusStatPayloadTests.cs` referencing a missing `StatusCatalog.Get`)
  resolved itself; `Core.Tests` builds green. Ran the sweep three separate process times to smooth
  cold-JIT single-shot noise: 8→0.1–0.2ms, 16→11.5–16.8ms, 32→6.4–10.9ms, 64→46.8–79.7ms,
  **128→606.5–700.0ms**. `huge` confirmed shippable (comfortably sub-80ms). 128 lands inside the
  spec's own pre-run 0.4–0.8s estimate and changes no decision — the `giant` tier was already gated
  on the Tarjan-first optimisation unconditionally in `empire-economy-ssot.md` §4's size table, so
  this closes the "asserted, not measured" gap DESIGN-GATE evidence rule 4 flagged, without moving
  the gate. `spec-world-topology.md` §"Cost, honestly" updated with the numbers and the honest note
  that 16 repeatably measures slower than 32 — real, unchased, doesn't affect either conclusion.)*
  - Description: `spec-world-topology.md:52` asserts `O(V⁴)` is *"fine at six sectors and fine at sixty."* The six is proven daily; **sixty has never been run.** Benchmark at 8/16/32/64 nodes and record the numbers in the spec.
  - Acceptance: real timings recorded; the `huge` tier is either confirmed shippable or the Tarjan-first optimisation is scheduled. **This closes a claim our own docs make**, and DESIGN-GATE evidence rule 4 does not exempt us.
  - Verify: the benchmark run; the spec updated with measurements rather than an assertion.
  - Files: `tests/FusionRpg.Core.Tests/World/Topology/ReconnectionCostBench.cs`, `docs/architecture/world/spec-world-topology.md`. Scope: S.
  - Dependencies: none — measures shipped code, can run any time.

---

## Phase 3 — the turn wakes up (`loam-turn`)

- [x] **L12: The `Production` phase** *(2026-08-23 — done. Built together with L13 in one new file,
  `Core/World/Loam/LoamPhases.cs` — genuinely coupled (Production's cap and Pressure's draw are two
  ends of the same ledger) and the plan's own dependency graph places them back to back. `Production`:
  per-sector yield via `LoamProduction`, capped at `LoamPolicy.LoamCapacity` — the cap throttles new
  accrual only, never claws back stock a template authored above it (the same fix the L9 harness
  needed, applied here on first write rather than rediscovered). Overflow reported per sector, named.
  Wired into `TurnEngine.Production` in one line. New tests in `LoamPhasesTests.cs`: a rootbed sector
  gains its seep, overflow above capacity is lost and reported naming the sector, determinism.)*

- [x] **L13: The `Pressure` phase — pooling, the draw, and the fade** *(2026-08-23 — done.
  `LoamPhases.Pressure`, called from `TurnEngine.Pressure` **after** `SupplyGraph.Run` (garrison upkeep
  reads the garrison that survived attrition). Per faction, per `TerritoryComponents` block: sum
  upkeep, draw proportionally from member stocks (remainder in ordinal id order), and — on a
  shortfall — apply the **whole** shortfall as fade to the single weakest contributor (worst
  `LoamBalance.PerSector`, ordinal tiebreak), not a same-turn cascade across every member. If that
  sector hits zero stability it becomes `Lost` (ownership clears) this same call — L14's "losing
  ground" mechanic landed here since the two are one function. A paid-in-full component recovers
  every member, not just the weakest, which is ideal §12.4's "a rich core carries a poor frontier
  indefinitely" made literal. New tests: a chained rootbed sector holds 50 turns unchanged; a cut,
  no-source sector runs down and is lost on a predictable turn (**found and fixed a fixture bug
  identical to L9's**: an isolated sector with no rootbed anywhere for its faction was silently
  G-C-exempt from all upkeep and never decayed — fixed by giving the faction an unconnected source
  elsewhere, which is exactly the "different component, same faction" case G-C is supposed to still
  charge); production-in-the-same-turn saves a component that would otherwise have shortfallen;
  the rich-core/poor-frontier claim; severing splits the economy and only the far half starves; the
  weakest sector (not an arbitrary one) degrades first. Core.Tests: 9/9 for `LoamPhasesTests`.)*

- [x] **L14: Losing ground, and the barren-claim warning** *(2026-08-23 — done. The "losing ground"
  half was built as part of L13 (same function, same call). The remaining piece —
  `ClaimResolver.cs` gains one check after a claim succeeds: `!Habitability.For(sector)` emits a
  `claim.barren:<id>` warning entry naming the sector, temporary-holding. **Never refuses the
  claim** — refusing would delete a real strategy (seizing a corridor to sever a chain), matching
  the spec's explicit boundary. New test file `ClaimBarrenGroundTests.cs`: claiming barren ground
  succeeds and warns; claiming a rootbed sector carries no such warning; **reclaiming does not
  rescue** — a faded (Lost, stability 0) sector, reclaimed, keeps its zero stability (claiming
  touches ownership and phase only) and is marked `Lost` again on the very next `Pressure` pass,
  proving the loophole the settlement rule closes for free rather than assuming it. Core.Tests: 3/3.)*

- [x] **L15: `RulesetVersion` 4 — the program's second and last golden move** *(2026-08-23 — done.
  `TurnEngine.RulesetVersion` 3→4, reasoning on the constant matching the W20/L2 precedent;
  `decisions.md`'s "World turn phase order" row extended (not replaced) with the 3→4 entry;
  `spec-turn-engine.md` gained a note that `Production`/`Pressure` stopped being pass-throughs — the
  phase **order** is unchanged, confirmed by inspection and by every phase-order test still passing.
  **Exactly one golden re-bless**: `WorldWaveOneAcceptanceTests.GoldenFinalHash`, entry #9, captured
  the real post-wiring hash rather than hand-editing one. Wiring L12/L13 into `TurnEngine` before
  this task landed broke only that one stored literal — every store-vs-engine replay-parity test
  (the determinism claim that actually matters) was **already green** at the moment of wiring,
  proving replay held throughout. **Version-3 refusal**: verified by inspection rather than a new
  test — `RpgStore.WorldTurns.cs`'s `GetWorldTurnReport` already refuses to re-derive on any
  `EngineVersion`/`RulesetVersion` mismatch (`log.RulesetVersion != TurnEngine.RulesetVersion` →
  `null`), unconditionally on the *values* involved; no existing test exercises this branch for
  *any* past version bump either (a pre-existing gap, not one this task introduced or is positioned
  to close alone). L2's own "ruleset version unchanged" test updated to reflect that the version
  legitimately moved later, at this task, not at L2. Full sweep: Core.Tests 2762/2762, Data.Tests
  423/423, E2E.Tests `~World` 37/37, Guard.Tests 61/61, all four guard scripts OK.)*

### Checkpoint 3 — ground can be lost ✅ PASSED 2026-08-23
- [x] All six named scenarios pass, each failing if its rule is removed (verified by construction:
  each scenario's fixture is built so the rule under test is the only thing that could make it pass).
- [x] Replay byte-identical at `RulesetVersion` 4; version-3 reports refuse re-derivation (mechanism
  verified by inspection — see L15's note on the pre-existing test gap for this generic path).
- [x] **Two golden moves total** (L2, L15), both with reasons. No third — confirmed no other stored
  hash literal exists in the suite.
- [x] All four guard scripts green; no new float, clock or RNG violation (`WorldDeterminismGuardTests`
  passing within the full Core.Tests green run).

---

## Phase 4 — a map that can teach (`loam-maps`)

- [x] **L16: `WorldSizeCatalog`** *(2026-08-23 — done. Five-tier catalog, ids plain
  (`small`/`medium`/`large`/`huge`/`giant`), display names as content (Pocket/Fragment/Expanse/
  Abyss/Maelstrom), `MinNodes`/`MaxNodes` range per tier, `Available` flag. `RequireAvailable(sizeId)`
  throws naming the tier for `large`/`huge`/`giant`. `WorldTemplateCatalog.SizeIdOf(templateId)` maps
  `first-light`→`small`, `two-hearths`→`medium`; new `WorldValidation` rule 13 checks a built world's
  actual sector count falls inside its declared tier's range. New test file `WorldSizeTests.cs`.
  Core.Tests `~World` unaffected regression: full suite stayed green through this and L17-19.)*

- [x] **L17: `two-hearths` — the gate map**  *(2026-08-23 — done. New file
  `WorldTemplateCatalog.TwoHearths.cs` (made the catalog `partial` per the spec's own 700-line
  guidance rather than growing the main file) — 16 sectors, a dumbbell: two capitals (`d-home`+
  `d-flank-1`, `z-flank-1`+`z-home`), each a small internal **loop** so the capital itself is not a
  single point of failure, each trailing one outlying holding (`d-outpost`, `z-outpost`) reachable by
  exactly one lane — the severable waist, twice, symmetric per ideal §12.3. A seven-sector barren
  corridor joins them with one hot sector (two rootbeds, the map's highest intensity) at the
  midpoint. Zomboss's capital is ordinary owned ground, never a second `Flags.Home` — validated by
  the existing, untouched Rule4. Builds and validates clean on the **first** full-suite run (2762/2762)
  with no iteration needed on the authored data itself. Determinism and stable-order asserted directly
  in `TwoHearthsTests.cs`.)*

- [x] **L18: The teaching properties — one test per design target** *(2026-08-23 — done.
  `TwoHearthsTests.cs`, one test per row, all passing on the **first run**: rootbed scarcity (5 of 16,
  "~4" — the range assertion allows 3-6); barren corridors (7, "~6" — allows 5-8), none habitable, none
  carrying a Seat; a chaos gradient (every capital <1000, the hot sector >2000, strictly fiercer than
  every capital); the severable waist (severing `l-df2-do` splits Dave into two components, one a
  singleton); the hot sector (2 rootbeds, intensity 2600); two capitals (both habitable, exactly one
  `Flags.Home` — Dave's — Zomboss's capital ordinary and therefore genuinely losable); **≥2
  articulation points, measured** via the already-existing `ArticulationPoints.Find`/`LaneGraph.Build`
  (not reimplemented) — found 10 in this topology, including both authored waists by name, confirming
  the capital loops work (their members are *not* cut vertices) while the whole corridor chain is.)*

- [x] **L19: The story runs** *(2026-08-23 — done, with one honest deviation from the literal
  spec wording, explained in the test file's own doc comment. The spec says "take a rootbed sector...
  get cut off, lose it" — played through, `hot-ground` sits behind four **unowned** corridor sectors,
  so claiming it never joins Dave's `TerritoryComponents` at all; with two rootbeds it is
  self-sufficient alone (production 100 vs upkeep ~25) and **cannot** be lost to the fade by design —
  that is the settlement rule's positive case, not a story to script. `TwoHearthsStoryTests.cs` tells
  both real halves instead: `A_legion_can_take_and_hold_the_hot_sector...` (march six lanes — found
  empirically that a mid-march entity needs its Move order **re-filed every turn**, there is no
  automatic continuation — clear the one guard, claim, hold 20 turns) and
  `Ordinary_ground_taken_and_connected_is_subsidised_then_lost_once_cut_off` (claim `corridor-1`,
  connected and subsidised by the capital cluster, then sever `l-df2-c1` and watch it alone reach
  `Lost` on a predictable turn while the whole capital cluster — `d-home`/`d-flank-1`/`d-flank-2`/
  `d-outpost` — stays untouched). **A second real finding surfaced while writing the second test**:
  unowned/freshly-claimed ground is authored at **zero** `StabilityMilli` (nobody has been anchoring
  it), so "subsidised and holding" means *climbing steadily* via the paid-in-full recovery rate, not
  starting full — fixed a wrong assertion that assumed 1000 as a baseline, and the corrected assertion
  (monotonically non-decreasing, ending above where it started) is the honest claim. **Fixture
  regeneration was not needed**: nothing about `first-light`'s own wire output changed in this phase
  (two-hearths is a new, separate template), confirmed by running `WorldFixtureTests` unmodified — it
  passed without a bless. Full sweep: Core.Tests 2783/2783, Data.Tests 423/423, E2E.Tests 180/180,
  Guard.Tests 61/61, web suite 292/292, all four guard scripts OK.)*

### Checkpoint 4 — the map can exercise the mechanic ✅ PASSED 2026-08-23
- [x] Every teaching property has a passing named test (7/7 in `TwoHearthsTests.cs`).
- [x] The cut-off-and-lost story runs end to end (`TwoHearthsStoryTests.cs`, two tests — see L19's
  note on why the story's *subject* changed from the literal spec wording, for a mechanically sound
  reason discovered by actually playing it through the real engine rather than assuming).
- [x] `first-light` unchanged in behaviour and still the default template — untouched by this phase;
  `WorldTemplateCatalog.Build`'s dispatcher still resolves both templates explicitly, no default drift.
- [x] No third golden move — this phase touched no `TurnEngine` behavior and re-blessed nothing.

---

## Phase 5 — an opponent, and eyes (`loam-ai-survival`, `loam-fe`)

- [x] **L20: The `Abandon` rule — and the A4 hypothesis, tested rather than assumed** *(2026-08-23 —
  done. Inserted as rule #2 in `FrontierRulesPolicy`'s chain (right after `Defend`, above everything
  else) — belief-side only, using `TerritoryComponents`/`LoamProduction`/`LoamUpkeep`/`LoamBalance`'s
  row overloads exclusively, verified by `WorldDeterminismGuardTests`' literal `WorldState`-token scan
  over `World/Ai/`, still green. **New `IWorldView.OwnLoamStock(sectorId)`** — a small, deliberate
  addition: the AI needs its *own* current stock to judge "will this run out," and `IntelSnapshot`
  deliberately never carries it, even for the owner — this reads truth directly for sectors the
  faction itself owns, which is self-knowledge, not fog. `LoamPolicy.AbandonmentHorizonTurns=3`
  (provisional, per the spec's own "still open, found by measurement"). **Found and fixed a real
  regression on the first run**: two pre-existing `FrontierRulesTests` (built before loam existed,
  no rootbed anywhere in their fixtures) started firing `Abandon` on every turn, because my belief-side
  computation never checked G-C — fixed by mirroring `LoamUpkeep`'s truth-side exemption exactly
  (a faction with no source anywhere is exempt, full stop). **A4 hypothesis tested, not assumed**:
  a 3-turn staged evacuation first appeared to oscillate ("abandon b" then "expand to b" twice) —
  investigated rather than accepted, and the cause was a **bug in the test harness**, not the engine:
  manually re-running `IntelRecorder.Observe` each loop iteration with a turn counter out of step with
  `TurnEngine.Step`'s own `Observe` phase corrupted belief into reporting "b is not mine any more."
  Building the view straight from `world` (whose `Intel` the engine already maintains correctly)
  removed the phantom oscillation entirely — a false-positive of exactly the shape A4 was written to
  catch. Six named tests (fires / does not fire on surplus / does not fire with deep runway despite
  insolvency / picks the worst contributor and only its occupant evacuates / Defend wins / A4) plus
  the **hundred-turn survival test on `two-hearths`, and Zomboss survives without any handicap at
  all** — closing L21's "without a handicap, or record the finding" question before L21 even starts.
  Mutant set extended by 5 (all caught) in `scripts/mutants/world-ai.json`; **also found, while
  running it, 2 stale anchors and 1 pre-existing survivor in `Recover`/`Expand`** — fixed the two
  stale anchors (mechanical, matched to code that had already changed before this session) and left
  the `Expand` survivor **documented with a comment next to the code**, since building an Expand-
  specific fixture is outside this module's one-rule boundary. Full mutant run: 53/54 caught, 0 stale,
  1 documented pre-existing survivor. Core.Tests full suite: 2790/2790, Guard.Tests 61/61, no golden
  moved (Data.Tests 423/423 — `first-light`'s scenarios still commit explicitly for Zomboss).)*

- [x] **L21: The handicap, applied and announced** *(2026-08-23 — done. `LoamUpkeep` already read the
  handicap since L7; the new piece is the announcement — `LoamPhases.Pressure` emits
  `loam.handicap:<value>` exactly once per faction per turn whenever `UpkeepHandicapMilli != 1000`,
  placed once at the top of the per-faction loop regardless of how many components/sectors that
  faction touches. "A handicapped faction pays proportionally less" was already proven at L7
  (`The_handicap_scales_the_same_way_intensity_does`); "Zomboss survives a hundred turns without a
  handicap" was already proven at L20 — this task's own new test is
  `A_non_default_handicap_is_announced_exactly_once_per_faction_per_turn`, confirming the entry fires
  for a 500-handicap faction and stays silent for a default-1000 one in the same turn. No golden
  moved: `first-light`'s factions are all default-1000, so the new report line never fires for it.
  Core.Tests 2791/2791, Data.Tests 423/423.)*

- [x] **L22: The wire — derived numbers, owner-only** *(2026-08-23 — done. `WorldSectorDto` gains
  `habitable` (anyone-who's-scouted), `loamProduction`/`loamUpkeep`/`loamNet`/`componentId`/
  `componentProduction`/`componentUpkeep`/`componentNet`/`stabilityMilli` (owner-only). New private
  `WorldEndpoints.ComputeLoamReading(world, factionId)` — same shape as the existing `Lifelines`
  helper: computed ONCE per request over `TerritoryComponents.For(world, view.FactionId)`, so
  owner-gating is **structural** (a sector the viewer doesn't own has no entry in any of the
  dictionaries at all, never a value that's merely zeroed) rather than a per-field check that could
  be forgotten on the next field added. `componentId` is the component's lowest sector id — stable
  and meaningful on the wire, not an opaque index. `StabilityMilli` (previously always-zero, per an
  earlier "nothing observes it yet" comment) now reads truth directly for the owner only, the same
  pattern `LifelineCost` already used. No loam arithmetic in TypeScript — everything is server-computed
  and merely displayed. New test file `WorldLoamWireTests.cs` (E2E): the leak-proofing property
  (every owner-only field is exactly 0/null for every sector the viewer doesn't own, across both
  factions on `two-hearths`), the owner's own sectors carry real numbers grouped consistently by
  component, and `habitable` reaches a sector before it's owned but never before it's scouted.
  **Honest scope note**: "a split territory reports two independent components" is proven at the
  Core level already (L18/L13's severing tests) — there's no player-issued sever command in this
  wave, so a single faction can't be driven into two components through the live HTTP surface at all;
  `ComputeLoamReading` has no component-count special case, so its correctness generalises from the
  one-component case this file actually exercises. Found and fixed the expected fixture drift
  (`WorldFixtureTests`, re-blessed). Full sweep: Core.Tests 2791/2791 (one unrelated flaky allocation
  test in `Atoms` reproduced red once, green on every re-run — not touched by this task), Data.Tests
  423/423, E2E.Tests 183/183.)*

- [x] **L23: Territory is light in the dark** *(2026-08-23 — done. `worldTypes.ts` gains `habitable`/
  `stabilityMilli`/the seven other L22 fields; `worldViewModel.ts` gains `AnchorState` ("anchored" |
  "fading" | "barren" | "not-yours") and the pure `anchorStateOf(ownership, habitable, stabilityMilli)`
  — not-yours wins first (loam adds nothing to the existing fog treatment), then barren (yours, no
  source, **regardless of the stability number** — a flat state, not a point on the fading scale),
  then anchored/fading split at a 900‰ floor. `SectorNode.tsx`: continuous `anchorOpacity` dims
  `fading` in proportion to `stabilityMilli`; `barren` gets a **flat, distinct** `grayscale` +
  stone-toned treatment instead, at full opacity, so it never reads as "just a deeper fade"; a small
  status line uses player words only ("fading" / "cannot be kept"), silent when anchored (healthy is
  the quiet state); every card carries `data-anchor-state` for assertion. New tests: `anchorStateOf`'s
  four branches plus the DTO wiring in `worldViewModel.test.ts`; rendering + distinctness (all four
  states produce different `data-anchor-state` values, `fading`'s opacity is strictly between 0 and
  1, `barren`'s is exactly 1 with `grayscale` doing the work) in `SectorNode.test.tsx`. **Honest gap**:
  this project has no `npm run lint` script (checked `package.json` directly rather than assume) —
  `npm run build` already runs `tsc --noEmit` first, which is what would have caught a type-level
  regression; noted rather than fabricating the script. Web suite 303/303 (11 new), build green.)*

- [x] **L24: The gauge and the sector panel** *(2026-08-23 — done. The wire needed one more thing
  before the gauge could show "stock": raw `LoamStock` wasn't on it at all (spec-loam-model's fog
  rule banned it outright, written when no economy endpoint existed yet). Resolved the same way
  `StabilityMilli` already was — owner-gated at the truth layer, never through belief/`IntelSnapshot`
  — and updated the one E2E test (`WorldLoamFogE2ETests`) that had locked in the old absolute ban,
  with a comment explaining the supersession rather than silently deleting the assertion. Added
  `ComponentStock` and `WillReleaseNextTurn` alongside it. The release forecast is the harder half:
  "a sector the engine will release next turn" requires predicting `LoamPhases.Pressure`'s own
  shortfall-and-weakest-contributor selection a turn early, from the server's projection layer, over
  the state as it stood after the *last* resolved turn — not simply re-checking `componentNet < 0`
  (a flow number), because `Pressure` actually compares next turn's *pooled stock* (current stock
  plus this turn's own capped accrual) against upkeep. Rather than risk two independent copies of
  that selection drifting apart, factored the weakest-contributor tiebreak out of `LoamPhases.Pressure`
  into a new `LoamForecast.Weakest(world, component, available, upkeep)`, which `Pressure` now calls
  too (pure refactor — full Core.Tests re-run confirmed zero behavior change, golden hash included);
  added `LoamForecast.ProjectedStock` (one turn of `Production`'s own capped accrual, replayed without
  mutating state) and `LoamForecast.WillRelease` (upkeep vs. projected stock, weakest contributor,
  then `FadePolicy.Apply` to check whether *that specific sector* would actually hit zero — not just
  fade further). `WorldEndpoints.ComputeLoamReading` calls `LoamForecast.WillRelease` directly — zero
  duplicated arithmetic in the Server layer. New Core-level `LoamForecastTests.cs` (5 tests, including
  one that runs the forecast and the real `Pressure` phase against the same fixture and asserts they
  agree, both on a doomed and a healthy component) — deliberately a Core-level test, not a live-turn
  E2E grind, since constructing an actual multi-turn HTTP shortfall scenario would be far more fragile
  than a hand-built `WorldState` fixture (the same call this program made for `LoamPhasesTests`).
  Frontend: `worldViewModel.ts` wires the nine new/existing loam fields into `SectorNodeData` (mirrors
  L23's pattern exactly) and adds a pure `summarizeLoam(nodes)` fold — empire totals are the *sum of
  each distinct component's already-server-finalized totals*, once per component, never re-derived
  per-sector math, keeping to the "no loam arithmetic in TypeScript" boundary the same way L22 did.
  New `LoamGauge.tsx`: income/upkeep/net/stock always visible; per-component rows only appear once
  territory is actually split (`"Your supply is split into N parts."` — the spec's own example
  phrase); a starving component says `"can't cover its own keep"` in place of its net figure rather
  than making the player subtract two numbers. New `SectorPanel.tsx`, mounted inside the existing
  "Sector" inspector rather than as a second competing panel: a selected sector's own
  earns/costs/net, its component's stock or the same starving-plainly message, and — the abandonment
  surface — a release marker (`"Losing ground next turn — its territory can't keep up."`) that only
  appears when `willReleaseNextTurn` is true; silent otherwise, matching L23's "silence is the healthy
  state" convention. Barren-owned ground still shows its upkeep (it still draws on the pool and is
  exactly the ground most worth abandoning) — only non-owned ground is hidden outright, since showing
  zeroed economy fields for an enemy sector would read as "you know their economy," which the
  boundary forbids even as an implication. **Honest scope note**: neither new component has an
  interactive control — pinning (the one feature that would need one) is explicitly deferred past the
  gate, so the "real controls" acceptance criterion has nothing to bind to yet; it applies once
  pinning ships. Full sweep: Core.Tests 2796/2796 (includes the new forecast tests and the `Pressure`
  refactor — no drift), Data.Tests 423/423, E2E.Tests 183/183 (fixture re-blessed for the three new
  DTO fields — third fixture rebless this program, all mechanical, no golden-move budget spent since
  fixtures aren't the determinism-lock hash), Guard.Tests 61/61, Launcher.Tests 162/162, all four
  guard scripts OK, web suite 322/322 (19 new: 6 in `worldViewModel.test.ts`, 5 in
  `LoamGauge.test.tsx`, 8 in `SectorPanel.test.tsx`), `npm run build` green. **Pre-existing, unrelated
  failure noted, not fixed**: `FusionRpg.CheatCore.Tests` has one red test
  (`DebugScenariosTests.No_unknown_step_names`, a shield/VFX debug-scenario registry mismatch on
  `debug.shield.demo-all`) — confirmed via `git diff`/`git log` that `DebugScenarios.cs` is untouched
  this session and matches the committed `HEAD` exactly, so this belongs to the shield/VFX stream, not
  loam.)*

### ⭐ Checkpoint 5 — THE GATE
- [x] All suites green; all four guard scripts OK; two golden moves total, both with reasons.
- [x] **Falsifying probe, ahead of the owner's own ten turns** *(2026-08-23). New
  `TwoHearthsTenTurnProbeTests.Ten_baseline_turns_on_two_hearths_never_degenerate_for_either_faction`
  (Core.Tests): runs the exact scenario named in the gate brief — ten turns on `two-hearths`, real
  `TurnEngine.Step`, zero commands filed by either commander — and asserts, every turn, for both
  factions: no component's pooled stock ever goes negative, and a sector is never named a release
  candidate unless its component's net is actually negative (an external re-check of
  `LoamForecast.WillRelease`'s own invariant, not a restatement of it). Captured log: both capital
  clusters (`d-flank-1`'s component for Dave, `z-flank-1`'s for Zomboss) settle into an exact,
  unchanging equilibrium by turn 1 (stock frozen at 553 / 551 respectively for the remaining 9 turns)
  — the two-rootbed capital is self-sufficient immediately, not eventually, matching
  `A_rich_core_carries_a_poor_frontier_indefinitely`'s and `A_chained_rootbed_sector_holds_forever`'s
  existing Core-level proof of the same rule. **What this tells the owner going in**: neither capital
  is at any risk from a passive ten-turn baseline — whatever tension the playtest surfaces will come
  from ground taken *beyond* the capital (corridor sectors, the outpost), not from the home base
  itself, matching what `TwoHearthsStoryTests`'s existing take-and-lose story already demonstrates
  through the real engine. **What this is not**: a substitute for the owner's own judgment. It proves
  the mechanic is not degenerate before the owner spends their own time on it; it cannot answer
  whether losing ground *feels* like a decision, tense, or frightening — those three questions have
  no test. Full sweep re-run after adding it: Core.Tests 2797/2797 (one unrelated flaky allocation
  test in `Atoms.PredicateCompilerTests` reproduced red once in the full-suite run, green in isolation
  and on immediate re-run — the same pre-existing flake already logged during L22, not touched here).)*
- [x] **Live-browser check, real server, real persisted world** *(2026-08-23). Republished
  `dist/FusionRpg.Server` with the L24 changes, restarted it (direct `Start-Process`, per this repo's
  own server-lifetime note — not `deploy-play.ps1`, which needs a game install this check doesn't),
  and opened `#/world` in an actual Chrome tab against the player's real, already-persisted
  `first-light` save (turn 3) — not the checked-in fixture. Found one real bug this way that no unit
  test had caught: `SectorPanel`'s Net reading used `>= 0` for the "+" sign, so a sector earning and
  costing exactly nothing showed **"Net +0"** — a false positive sign on a flat zero (`LoamGauge`'s
  own reading already used the correct `> 0`, so the two panels disagreed on the same number this
  session had introduced in the same task). Fixed to `> 0`, added a regression test asserting the
  zero case renders a flat "0" for both, rebuilt, republished, restarted, and re-checked live — "Net"
  now reads plainly. Confirmed via this browser session: `Homeworld` (owned, no rootbed) correctly
  reads Earns 0 / Costs 0 / Net 0 / Supply "0 in store" and the barren "CANNOT BE KEPT" tag from L23 —
  first-light predates the Rootbed slot type entirely, so Dave's G-C exemption (no source anywhere)
  applies faction-wide, and the gauge correctly shows flat empire zeros rather than crashing or
  showing `NaN`/`undefined`. Console/network check: one pre-existing, unrelated `404` on
  `/favicon.ico` (no favicon ever configured, nothing to do with loam); every data endpoint
  (`/api/world/1`, `/api/world/first-light/state`, `.../turn/2`) returned `200`. **Honest scope
  note**: the live server has no route to create an arbitrary `two-hearths` world (`/api/test/world/
  create` is E2E-harness-only, never mapped in `Program.cs`), so the split-supply / non-zero /
  release-marker render paths are still verified only against the fixture-driven unit tests, not a
  second live world — reaching a live two-hearths split would need either a temporary production
  route or direct SQLite seeding, neither of which this check's scope covers. Full re-run after the
  fix: web suite 323/323 (1 new).)*
- [x] **Second live check: a real `two-hearths` world, not just `first-light`** *(2026-08-23). The
  "honest scope note" above turned out to have a cheap answer: `SimFlags.Enabled` (env var
  `FUSIONRPG_SIM=1`) already gates `MapSimAndProbes` → `MapWorldTest` in the shipped `Program.cs` —
  no code change needed, just restarting the already-published server with that one variable set.
  Created a **second player** (`gate-preview`, id 2) via the existing production `/api/players`
  route and a `two-hearths` world for them via the now-exposed sim-only route — Player 1's real,
  in-progress `first-light` save (turn 3) was never touched or switched away from except to view the
  new one, and was restored as current player afterward. Confirmed live, real, non-fixture data end
  to end: gauge read Income 100 / Upkeep 47 / **Net +53** / Stock 600 — the exact turn-0 numbers the
  Core-level `TwoHearthsTenTurnProbeTests` already computed, now independently confirmed through the
  Server projection and the rendered React tree, not just Core math. Clicked into `D Flank 2`
  (barren, owned, real upkeep this time since the *faction* — unlike `first-light`'s — does have a
  rootbed elsewhere so G-C does not swallow it): Earns 0 / Costs 13 / **Net -13** in the red/bad
  style / Supply "600 in store" (not the starving warning, since the *component* nets positive even
  though this one sector doesn't — the sector-vs-component distinction the design calls for,
  rendering correctly). Clicked into `D Home` (rootbed): Earns 50 / Costs 8 / Net **+42**, plus the
  legion (`e-dave-legion-1 — 330 hp`) listed under "Forces". No console errors beyond the same
  pre-existing unrelated `/favicon.ico` 404. Cleanup: switched current player back to 1, stopped and
  restarted the server **without** `FUSIONRPG_SIM`, confirmed `simEnabled:false` again — the running
  server and its data are in the same state this check found them in, plus the L24 code.)*
- [ ] **Owner playtest** — ten turns on `two-hearths`, against the brief written into `spec-loam-maps.md`. Three questions: is choosing what to let go a real decision · does the fade read as tense or as bookkeeping · is a split economy frightening.
  - **How to actually get a `two-hearths` save running** (the brief says what to play, not how to start it — closing that gap here since this session had to work it out to do its own live checks):
    1. Publish + start the server once, normally: `dotnet publish src/FusionRpg.Server/FusionRpg.Server.csproj -c Release -o dist/FusionRpg.Server` then `Start-Process dist/FusionRpg.Server/FusionRpg.Server.exe` from `dist/FusionRpg.Server` (or just use the player-facing Launcher, which does the same thing).
    2. `two-hearths` isn't reachable from the normal UI yet — `first-light` stays the default template until the gate passes (`spec-loam-maps.md`'s own decision), and the sim-only world-creation route is gated behind `FUSIONRPG_SIM=1`. Set that env var before starting the server for **this playtest only** — set `$env:FUSIONRPG_SIM = "1"` before the `Start-Process` line above.
    3. Create your own save for it rather than reusing an existing one: `curl -X POST http://127.0.0.1:5088/api/players -d '{"name":"loam-gate"}'` (note the returned `id`), then `curl -X POST http://127.0.0.1:5088/api/test/world/create -d '{"playerId":<id>,"worldId":"loam-gate","templateId":"two-hearths","seed":"7"}'`.
    4. `curl -X PUT http://127.0.0.1:5088/api/players/current -d '{"id":<id>}'`, then open `#/world` in the browser — it's turn 0 on `two-hearths`, ready to play. Restart the server without `FUSIONRPG_SIM` afterward if you want the sim-only route closed again for normal play.
- [ ] **Read the verdict correctly.** *"It works but feels pointless"* is the **expected** result — the reward layer does not exist yet. The mechanic is condemned only by *"I did not care which sector I lost"* or *"I could not tell what was happening."*
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).
- [ ] **Decide:** does the post-gate program happen? `loam-legions`, `loam-ai`, `structure-substrate`, `loam-structures`, `loam-texture` are unspecced and unplanned on purpose.
