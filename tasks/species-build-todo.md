# Tasks: `species-build`

Plan: [species-build-plan.md](species-build-plan.md). **30 tasks, 6 phases, 6 checkpoints.**
Module ids from [species-build-map.md](../docs/architecture/species-build-map.md); `m N` = module N.

Sizes: **XS** 1 file · **S** 1-2 · **M** 3-5. No task exceeds 5 files.

**Standing rules for every task:** no git operations (leave the work in the tree, hand the owner a
commit message); every balance number is a named tunable with a `_why` note; `long` magnitudes, widen
before multiplying, divide by 1000 last, overflow throws; cite **symbol names** over line numbers —
another stream is editing this repo concurrently and lines drift.

---

## Phase 0 — corrections · `m1 resolver-memo`, `m2 budget-source`

Both are fixes to already-shipped code. Both are semantically neutral. **Zero goldens** is an
acceptance criterion, not a hope.

- [ ] **T0.1** The memo, with `Θ` in the key · **S** · `m1`
  - Acceptance:
    - [ ] Memo on `AptitudeSubsystem`, keyed `(StatSide Side, int TypeId, long Theta)`, generation-stamped
    - [ ] **Equivalence:** memoized and non-memoized resolves are element-wise identical
    - [ ] **⛔ Θ is honoured:** two contexts identical but for `Θ` resolve to *different* modifiers —
          the test an earlier spec draft would have failed
    - [ ] Same `TypeId`, different `Side` → different results (`polevaulterzombie`/`wallnut`)
    - [ ] Bounded growth: N entities of one `(Side, TypeId, Theta)` produce one entry
    - [ ] Instance state, never static (a static leaks between scoped test hosts)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude`
  - Files: `AptitudeSubsystem.cs`, `tests/.../AptitudeMemoTests.cs`

- [ ] **T0.2** Invalidation bumps at every path · **S** · `m1`
  - Acceptance:
    - [ ] Bumps on: allocation replaced (session start / reconnect / `AptitudesUpdated`), match edges,
          any `StatSystem.Invalidate()`, tuning reconfigured
    - [ ] **One test per path** — each fails if its bump is removed
    - [ ] A changed `Θ` needs no bump (it is a different key) — asserted, so nobody adds a redundant one
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `.\scripts\guard-single-writer.ps1`
  - Files: `CheatState.cs`, `Match/MatchHost.cs`, tests

- [ ] **T0.3** Split the guard test into the two claims it was conflating · **XS** · `m2`
  - Acceptance:
    - [ ] `Rates_are_ordered_...` — the existing constant-source check, **renamed to what it proves**
    - [ ] `Real_budgets_are_ordered_at_representative_sources` — each scope fed a value **in its own
          units**, ordering asserted on *budgets*. Fails if `DemonType`'s source returns to an accumulation
    - [ ] Covers **three** scopes, not four, with a comment naming `Aspect` as excluded **because
          `element_mastery` does not exist** — inventing a value would decide the ordering it claims to prove
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter PointBudget`
  - Files: `tests/.../PointBudgetTests.cs`

- [ ] **T0.4** The `(level − 1)` rule and the three stale citations · **S** · `m2`
  - Acceptance:
    - [ ] A named helper yields `max(0, speciesLevel − 1)`; subtraction before the multiply, `checked`
    - [ ] `PointsFor(DemonType, level=0)` and `level=1` both yield **zero**
    - [ ] "almanac XP" corrected in all three places, each stating **why an index rather than an
          accumulation**: `spec-point-economy.md` §2 table, `PointBudget`'s doc comment,
          `aptitudes.v5.json`'s `_scopeSourcesWhy`
    - [ ] `No_cap_on_an_aptitude` still passes (PS-8)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `.\scripts\guard-power.ps1`
  - Files: `PointBudget.cs`, `AptitudeTuning.cs`, `data/tuning/aptitudes.v5.json`, `spec-point-economy.md`

### ✅ Checkpoint 0 — the corrections are provably neutral
- [ ] Core + Guard suites green
- [ ] **Zero goldens re-blessed.** If one moved, the memo is not semantically neutral — that is the bug
- [ ] `guard-power`, `audit-overflow` clean

---

## Phase 1 — foundations · `m3 species-xp`, `m4 redistribution-plan`

- [ ] **T1.1** Species progression row + migration · **M** · `m3`
  - Acceptance:
    - [ ] Storage decision **A or B recorded with its reason** (recommendation: A — `kind='species'` +
          a nullable text key via `EnsureColumn`, because B forks the ledger, retention, compaction and
          `LevelChangePipeline`). Confirm against `RpgStore.Progression.cs` before committing
    - [ ] Curve reuses the shipped arithmetic shape with its own `first`/`step` tunables
    - [ ] **Unlimited levels** (PS-8); overflow throws, never clamps
    - [ ] A pre-migration database still opens and reads a default
    - [ ] Existing `plant`/`zombie` type rows **untouched**
    - [ ] ⛔ **Host wiring — Core reads no file.** `species-progression.v1.json` gets a loader and is
          injected by the **server host** (`Program.cs`), mirroring `AptitudeTuningHub.Configure`'s own
          shape. **Server only** — the injector never computes a species level. A missing key is a
          **load rejection naming it**, proven by test, never a silent default
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Progression`; `.\scripts\guard-dal.ps1`
  - Files: `RpgProgression.cs`, `RpgStore.Progression.cs`, `data/tuning/species-progression.v1.json`, tests

- [ ] **T1.2** Lawn projection: place/spawn → species row · **S** · `m3`
  - Acceptance:
    - [ ] A `PlantPlaced` fact levels the species row; the species resolved matches `LawnElementIndex`'s
          own answer for that `(Side, TypeId)`
    - [ ] **Collision safety:** a species that loses a `(Side, GameTypeId)` collision is still reachable
          through the non-lawn source — it is not permanently unlevellable
    - [ ] Idempotent: the same fact ingested twice levels once
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Progression`
  - Files: `RpgXpAwardMap.cs`, `RpgStore.Progression.cs`, tests

- [ ] **T1.3** The run award, and the ratio that makes it dominant · **S** · `m3`
  - Acceptance:
    - [ ] `runCompletionAward` fires **once per resolved match** in which the species was fielded,
          however many times it was placed
    - [ ] `placementAward` retained as the smaller term — both tunable
    - [ ] **The run term out-earns a plausible number of placements at the shipped tuning.** This is the
          assertion that keeps the grind vector closed; if a balance pass inverts the ratio it says so
    - [ ] Derived from **already-recorded** run-scoped facts — no new capture, nothing asked of the injector
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `RpgXpAwardMap.cs`, `RpgStore.Progression.cs`, `species-progression.v1.json`, tests

- [ ] **T1.4** Expedition source — the game-closed proof · **S** · `m3`
  - Acceptance:
    - [ ] An expedition win levels a species **with no lawn run in the test at all**. This is the test
          that proves standalone-first; it must fail if the award is removed
    - [ ] Species award shares the specimen award's transaction
    - [ ] **The `!pvzGame` rule still prevents web runs levelling PvZ almanac types** — two tests, both
          directions, because widening that condition by accident is the likely defect
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`; `.\scripts\guard-dal.ps1`
  - Files: `RpgStore.Expeditions.cs`, `RpgStore.Progression.cs`, tests

- [ ] **T1.5** `SpeciesBuildPlanner` phases 1–2 · **M** · `m4`
  - Acceptance:
    - [ ] Phase 1 derives each species' lean from its primary's **crowding** — crowded leans less, rare
          leans more, asserted on a synthetic corpus
    - [ ] Phase 2 distributes remainders against the **running corpus deficit**, ordinal iteration
    - [ ] **No single-primary:** every vector has ≥ `minAptitudesPerSpecies` (≥2) non-zero entries, even
          for an all-`pure` synthetic corpus
    - [ ] **The favour is never overridden:** every vector's largest share is its classified primary
    - [ ] Pure and Core-only — no file IO, no store, **no model call ever**
    - [ ] Permille `long`; largest-remainder rounding with ordinal tiebreak; vectors sum to exactly 1000
    - [ ] **Overflow throws, never wraps** on an extreme corpus
    - [ ] ⛔ **Host wiring.** `species-build.v1.json` gets a loader, injected by the **server host** and
          read by the generation tool. **Not the injector** — m6's design is explicit that the injector
          receives *points*, never the plan, the level or the budget rule. Missing key → named rejection
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter BuildPlan`
  - Files: `SpeciesBuildPlanner.cs`, `SpeciesBuildPlan.cs`, `data/tuning/species-build.v1.json`, tests

- [ ] **T1.6** Phase 3 verification, refusal, canonical serializer · **S** · `m4`
  - Acceptance:
    - [ ] Corpus shares outside `[floor, ceiling]` → **exit non-zero naming the offending aptitudes**
    - [ ] Deliberately infeasible tunables produce a **named refusal**, not a near-miss — the test that
          stops a near-miss shipping
    - [ ] Canonical serializer: sorted keys, pinned formatting, byte-identical rerun
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter BuildPlan`
  - Files: `SpeciesBuildPlanner.cs`, `SpeciesBuildPlan.cs`, tests

- [ ] **T1.7** `DemonBuildPlanGen` and the committed plan · **M** · `m4`
  - Acceptance:
    - [ ] CLI mirrors `DemonSpeciesGen` exactly — `--seed`, `--out`, `--check`, `_`-prefix skipping,
          refuse-the-whole-thing-rather-than-write-half
    - [ ] Run for real over the corpus; `data/generated/demons/_species-build-plan.json` committed
    - [ ] `--check` clean; a rerun is byte-identical
    - [ ] **The parity band is satisfied on the real corpus** — pass/fail, not a report
    - [ ] Shuffled input order produces the same plan (ordering is by `speciesId`, not file discovery)
  - Verify: `dotnet run --project tools\DemonBuildPlanGen -- --check`; `python scripts\audit-magic-numbers.py --targets M1`
  - Files: `tools/DemonBuildPlanGen/Program.cs`, the generated plan, tests

- [ ] **T1.8** ⛔ **CI gate for the generated plan** · **XS** · `m4`
  - Acceptance:
    - [ ] `ci.yml` runs `dotnet run --project tools/DemonBuildPlanGen -- --check` and **throws on a
          non-zero exit**, following the exact pattern already used for `DemonSpeciesGen --check` and
          `FamilyExpandGen --check` — including the `$LASTEXITCODE` check, since this repo has a
          confirmed history of a test step swallowing earlier failures
    - [ ] The throw message names the fix command, as the sibling gates do
    - [ ] **Added in this phase, not "at the end"** — the class-system standard is that each module wires
          its own gate as it lands
  - Verify: a deliberately stale plan makes the step fail locally
  - Files: `.github/workflows/ci.yml`

### ✅ Checkpoint 1 — a species can level, and a plan exists for it
- [ ] Core + Data suites green; `guard-dal` clean
- [ ] **The game-closed test passes** — an expedition levels a species with no lawn involvement
- [ ] `--check` clean and byte-stable; the band is satisfied on the real corpus
- [ ] Zero goldens
- [ ] **CI gates the generated plan** — a stale plan fails the build

---

## Phase 2 — the allocation · `m5 demon-type-allocation`

- [ ] **T2.1** Scope key and compose-at-read · **M** · `m5`
  - Acceptance:
    - [ ] `scope_key` = `player:{playerId}:species:{speciesId}`, encoded in **one place** beside the
          Commander encoding
    - [ ] **A species with a level and no override row resolves to the plan's shares × its budget — not
          to zero.** The test that catches the silent-zero risk
    - [ ] Per-player isolation: two players, same species, same level, one overriding → different results
    - [ ] Baseline is **computed, never persisted**
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude`; `dotnet test tests\FusionRpg.Data.Tests --filter Allocation`
  - Files: `SpeciesAllocation.cs`, `RpgStore.Aptitudes.cs`, tests

- [ ] **T2.2** Override, budget enforcement, endpoints · **M** · `m5`
  - Acceptance:
    - [ ] Override is **whole-vector**; deleting the row returns exactly the baseline, **free**
    - [ ] Overspend refused, **scope-locally** — a large Commander budget does not fund it
    - [ ] Scopes sum before share (an actor with both reads the sum)
    - [ ] No cap on the allocation (PS-8); overflow throws
    - [ ] ⛔ **`AptitudesUpdated` broadcasts to BOTH groups** on a species save, not just `WebGroup`.
          A WebGroup-only send is a defect this repo has already shipped once and found by live probe
          (2026-08-30): it left the injector's cached allocation **stale until the next reconnect**.
          Without this, a respec would not take effect on the lawn until a match edge
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `.\scripts\guard-dal.ps1`
  - Files: `AptitudeEndpoints.cs`, `RpgStore.Aptitudes.cs`, tests

- [ ] **T2.3** The seam guard · **XS** · `m5`
  - Acceptance:
    - [ ] A guard test asserts **no production consumer of species allocation calls `LoadAllocation`
          directly** — composition only happens behind the one named effective-allocation entry point
  - Verify: `dotnet test tests\FusionRpg.Guard.Tests`
  - Files: `tests/FusionRpg.Guard.Tests/SpeciesAllocationSeamTests.cs`

### ✅ Checkpoint 2 — allocation is real, and still invisible
- [ ] Core + Data + Server + Guard green
- [ ] **Zero goldens** — holds *only* because the budget is zero at level 1. If one moved, T0.4 broke
- [ ] Nothing player-visible yet, by design

---

## Phase 3 — both read paths · `m6 allocation-transport`, `m10 battle-allocation`

**They land together.** Shipping the lawn without battle is the incoherence module 10 exists to prevent.

- [ ] **T3.1** Server payload gains `species` — additively · **S** · `m6`
  - Acceptance:
    - [ ] `shares` is **kept, not renamed** — `RpgClient` hard-requires it; a rename silently stops the
          injector applying every allocation
    - [ ] `species` added beside the existing `{theta, budget, spent, withinBudget, shares}`
    - [ ] Only species the player has actually levelled are sent
    - [ ] The commander half is **byte-unchanged** for a player with no species allocations
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter Aptitude`
  - Files: `AptitudeDtos.cs`, `AptitudeEndpoints.cs`, tests

- [ ] **T3.2** Core `SpeciesAllocationSource` · **M** · `m6`
  - Acceptance:
    - [ ] `ctx → allocation` behind an **injected lookup**, mirroring `SpecimenOwnershipOracle`'s shape —
          fully provable in Core with a fake resolver, no game required
    - [ ] `polevaulterzombie`/`wallnut` resolve differently (side stays in the key) — a named test
    - [ ] **An un-configured index reports, never returns a silent zero** — the 222-point defect's shape
    - [ ] Commander and species points **merge into one `AptitudeAllocation`**, resolved once
    - [ ] No I/O on the Hot path — a guard test
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude`
  - Files: `SpeciesAllocationSource.cs`, tests

- [ ] **T3.3** Injector cache and refresh · **M** · `m6`
  - Acceptance:
    - [ ] Cache refreshed on **exactly the existing cadence** — `StartAsync`, reconnect,
          `AptitudesUpdated`, match edges. No new lifecycle, no polling
    - [ ] Never awaits the server on the Hot path
    - [ ] **One test per refresh path** for the cache-update logic that *is* Core-testable — a stale
          cache after an `AptitudesUpdated` push is a failure, and it is the shape T2.2's broadcast exists
          to prevent
    - [ ] ⚠️ The injector-side write is unverifiable offline — **verified by direct read plus T3.6**,
          this repo's established precedent for injector-only edits
  - Verify: `.\scripts\guard-secondary-no-unity.ps1`; `.\scripts\deploy-play.ps1 -NoServer`
  - Files: `RpgClient.cs`, `CheatState.cs`

- [ ] **T3.4** Battle setup reads species · **S** · `m10`
  - Acceptance:
    - [ ] `AptitudeChannelMods` takes the species; commander read **hoisted out** of the per-actor loop
    - [ ] **The coherence test:** an actor whose species has an allocation resolves *different* mods than
          one whose species has none
    - [ ] **Merged ≠ concatenated** — asserted explicitly, so a future refactor into two resolves fails
          loudly rather than silently changing every battle
    - [ ] Two species in one squad resolve differently (the species read stays per-actor)
    - [ ] **Inertness preserved:** a player with no allocation in either scope still resolves to empty —
          the existing `AptitudeChannelModsTests` assertion, unchanged and still passing
    - [ ] **The commander-read hoist is behaviour-neutral** — a squad's mods are identical before and after
    - [ ] **Every battle and expedition golden byte-identical.** If one moves, the level-1-zero rule broke
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `dotnet test tests\FusionRpg.Core.Tests --filter Battle`
  - Files: `WebMatchService.cs`, `tests/.../AptitudeChannelModsTests.cs`

- [ ] **T3.5** The two diagnostic paths · **S** · `m10`
  - Acceptance:
    - [ ] Battle report `aptitude.snapshot` includes the species contribution — a report missing the term
          that decided the battle is worse than no report
    - [ ] The derived-stat inspection endpoint agrees with what the lawn applies
    - [ ] Provenance no longer hard-codes `"scope" = "commander"`
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`
  - Files: `WebMatchService.cs`, `AuraDerivedEndpoints.cs`, tests

- [ ] **T3.6** ⚠️ **Owner-run live lawn check** · `m6`
  - Acceptance:
    - [ ] A plant whose species has a real allocation shows changed stats on a live lawn
    - [ ] Clearing the allocation returns it to baseline
    - [ ] No frame-time regression versus before this program (T0.1's memo is why)
  - Verify: `.\scripts\deploy-play.ps1 -NoServer`, then the live check
  - Files: none — this is a proof, not a change

### ✅ Checkpoint 3 — the feature is real everywhere it should be
- [ ] All C# suites green; four boundary guards green
- [ ] **Zero goldens**
- [ ] Lawn **and** battle both honour a species allocation; both diagnostics agree with the game
- [ ] Owner live check passed

---

## Phase 4 — economy and AI · `m7 species-respec`, `m8 zomboss-adaptive`

- [ ] **T4.1** Respec price and the Soul resource · **S** · `m7`
  - Acceptance:
    - [ ] `RespecResource` gains `Soul`; `PriceOf` gains a **count** argument, never a level
    - [ ] `price(count) = base + base × count × escalationPermille / 1000` — **linear, not geometric**
          (geometric against a flat faucet is how a price becomes a ceiling)
    - [ ] `RespecPolicy` carries no bare literal
    - [ ] ⚠️ **`species-build.v1.json` is shared with `m4`** — T1.5 created it with the band and lean keys.
          **Add the three respec keys beside them; do not rewrite the file.** Its loader and host wiring
          already exist from T1.5, so this task adds keys, not plumbing
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Respec`
  - Files: `RespecPolicy.cs`, `data/tuning/species-build.v1.json`, tests

- [ ] **T4.2** Counter, decay, atomic spend · **M** · `m7`
  - Acceptance:
    - [ ] `rpg_species_respec(player_id, species_id, count, last_respec_utc)` as a **partial `RpgStore`
          slice** sharing the one connection/lock/`EnsureHotSchema`/`Reset()` pipeline
    - [ ] Decay day-quantised in UTC, applied **on read** — no timer, no background job; count floors at
          zero and carries a comment naming it a **bounded counter**, exempt from PS-8
    - [ ] **Spend + counter + override in one transaction** — a simulated failure between them leaves
          *neither* applied
    - [ ] Uses **the ledger path the shipped sinks use** — `TrySpendSouls` has zero production callers
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Respec`; `.\scripts\guard-dal.ps1`
  - Files: `RpgStore.SpeciesRespec.cs`, tests

- [ ] **T4.3** The respec endpoint · **S** · `m7`
  - Acceptance:
    - [ ] Its **own** feature endpoint and reason — spends are never a generic endpoint
    - [ ] **First override free; revert free**; subsequent changes escalate then decay — all asserted
    - [ ] Replayed correlation id returns the original result **without spending again**; a refusal
          writes no state
    - [ ] Insufficient balance → `409 souls.insufficient`, no counter increment
    - [ ] **Never refused for being a respec** (PS-8)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`
  - Files: `SpeciesBuildEndpoints.cs`, tests

- [ ] **T4.4** `ZombossPatternSelector` · **M** · `m8`
  - Acceptance:
    - [ ] Pure: `(history, level, seed, tuning) → patternId`. No store, no clock, no I/O
    - [ ] Same inputs → same pattern; the pick is a function of `(seed, level)`, never a live roll
    - [ ] **Rate limit binds:** no second re-pattern within the cooldown even when both triggers fire
    - [ ] **Counter-bias is a weight, not a guarantee** — over many seeds the countering pattern is more
          likely *and is not always chosen*. Both halves asserted; the second keeps it out of the Mario
          Kart failure mode
    - [ ] Roster pinned at nine so a self-cancelling tenth cannot be added quietly
    - [ ] ⛔ **Host wiring.** `zomboss-adaptive.v1.json` gets a loader injected by the **server host only**
          — the Zomboss exists on battle and expedition surfaces, never the lawn, so wiring it into the
          injector would be dead weight. Missing key → named rejection
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Zomboss`
  - Files: `ZombossPatternSelector.cs`, `data/tuning/zomboss-adaptive.v1.json`, tests

- [ ] **T4.5** Scope argument and pattern on setup/report · **S** · `m8`
  - Acceptance:
    - [ ] `ZombossCommanderAllocation` takes the scope as an **argument** — it hard-codes Commander today,
          and a Zomboss pattern is a named allocation, not a player's commander build
    - [ ] Pattern id on `BattleSetup` and on the report
    - [ ] Budget cap holds for every pattern at every budget — the anti-cheat property, re-asserted here
          because this is what makes it reachable
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Battle`
  - Files: `ZombossCommanderAllocation.cs`, `BattleModels.cs`, tests

- [ ] **T4.6** The server seam and the reveal · **M** · `m8`
  - Acceptance:
    - [ ] The enemy side actually carries a pattern — without this, "a real production caller" is
          unreachable
    - [ ] **Pattern is part of the setup**, resolved before the battle runs, never rolled during
          resolution: the same `(setup, seed)` resolves identically twice
    - [ ] Revealed on the **following** fight's report per `revealDelayEncounters`; at delay 0, immediately
    - [ ] Battle and expedition only — **not the lawn**, and the acceptance does not ask for it
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `dotnet test tests\FusionRpg.Core.Tests`
  - Files: `WebMatchService.cs`, `ExpeditionEndpoints.cs`, tests

### ✅ Checkpoint 4 — the loop closes except for the surface
- [ ] All C# suites green; `guard-dal` green; `audit-magic-numbers` finds no bare literal in either Policy
- [ ] A respec can be bought, escalates, decays, and is never refused
- [ ] A Zomboss pattern reaches a real enemy squad and is revealed one fight late

---

## Phase 5 — the surface · `m9 allocation-surface`

- [ ] **T5.1** Contract and bus hooks · **S** · `m9`
  - Acceptance:
    - [ ] Species allocation DTO added **additively**; a narrowing or rename would be a version bump and
          is not done here
    - [ ] Hooks go through the existing bus — TanStack Query + the one SignalR hub; features call
          `useX()` only. `AptitudesUpdated` already broadcasts, so no second refresh mechanism
  - Verify: `cd web/fusion-rpg-web && npm run test`
  - Files: `contract/types.ts`, `lib/bus/queries.ts`, `lib/bus/mutations.ts`

- [ ] **T5.2** The panel, mounted in `AptitudesLayer` · **M** · `m9`
  - Acceptance:
    - [ ] Hosted by **`AptitudesLayer.tsx`** (owner, 2026-09-05) — imported by nothing today, so no
          migration and no third copy of the draft/save logic
    - [ ] Shows the shipped baseline, the override **as a deviation from it**, and the remaining budget
    - [ ] **Respec price shown before the confirm, never after**; first override and revert labelled free
    - [ ] Points render through the `aptitudePoints` unit class — and **never as a speculative preview**,
          which that class's rule forbids
    - [ ] No engine vocabulary in any rendered string
  - Verify: `npm run test -- SpeciesBuild`; `npm run build`
  - Files: `layers/aptitudes/AptitudesLayer.tsx`, `features/species-build/SpeciesBuildPanel.tsx`, `useSpeciesBuild.ts`

- [ ] **T5.3** GG conformance and E2E · **S** · `m9`
  - Acceptance:
    - [ ] **GG-1:** opening the layer from a stage leaves the stage mounted, its state identical **by
          reference**, with no refetch — the assertion GG-1 names as its own test
    - [ ] **GG-10:** the override action is ≤3 pushes from a stage
    - [ ] E2E: a species' build is visible, adjustable, revertible, and survives a reload
  - Verify: `npm run test`; `npx playwright test`
  - Files: tests only

### ✅ Checkpoint 5 — the program closes
- [ ] Web suite + build green; E2E covers the round trip
- [ ] A player can see a species' shipped build, override it, revert free, and respec with the price
      shown first
- [ ] **No third copy** of the allocation draft/save logic exists
- [ ] Full sweep: all C# suites, four boundary guards, `audit-overflow`, `audit-magic-numbers`
- [ ] **Zero goldens across the whole program**, or each move triaged and explained before re-blessing
