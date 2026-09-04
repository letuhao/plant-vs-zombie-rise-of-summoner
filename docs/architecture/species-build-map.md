# Capability map: `species-build`

**Status:** capability map **approved by the owner 2026-09-05**; **ten module specs written and then
audited the same day**. No plan, no tasks, no build authorized yet.

⛔ **A coverage audit and an adversarial spec review both ran before this was called done**, and between
them they changed real things: **module 10 did not exist** (species points were earned from expeditions
and would never have applied in them), **module 1's memo key was missing `Θ`** and would have served
stale builds, **module 6's payload renamed a field the injector hard-requires**, **module 8 declared a
dependency that does not exist**, and several citations were wrong. Each spec records its own correction
inline rather than quietly absorbing it.

| # | Module | Spec |
|---|---|---|
| 1 | `resolver-memo` | [spec-resolver-memo.md](species-build/spec-resolver-memo.md) |
| 2 | `budget-source` | [spec-budget-source.md](species-build/spec-budget-source.md) |
| 3 | `species-xp` | [spec-species-xp.md](species-build/spec-species-xp.md) |
| 4 | `redistribution-plan` | [spec-redistribution-plan.md](species-build/spec-redistribution-plan.md) |
| 5 | `demon-type-allocation` | [spec-demon-type-allocation.md](species-build/spec-demon-type-allocation.md) |
| 6 | `allocation-transport` | [spec-allocation-transport.md](species-build/spec-allocation-transport.md) |
| 7 | `species-respec` | [spec-species-respec.md](species-build/spec-species-respec.md) |
| 8 | `zomboss-adaptive` | [spec-zomboss-adaptive.md](species-build/spec-zomboss-adaptive.md) |
| 9 | `allocation-surface` | [spec-allocation-surface.md](species-build/spec-allocation-surface.md) |
| 10 | `battle-allocation` | [spec-battle-allocation.md](species-build/spec-battle-allocation.md) — **added by the coverage audit** |

**✅ The two open product questions were answered by the owner 2026-09-05**, and both are recorded in
their own specs:

- **XP faucet** — **both** signals, weighted so the run dominates: a `runCompletionAward` (larger, once
  per resolved match the species was fielded in) beside a `placementAward` (smaller, per place/spawn),
  **both tunable**. Kills the grind vector by *ratio* rather than by *ban*, and keeps the "I used this
  species" signal and its variance alive. SaGa's scaling counter stays available, not adopted.
- **Surface host** — **`AptitudesLayer.tsx`**, which is already named for the shape GG-1 requires and is
  imported by nothing, so there is no migration and no third copy.

**Still open, and deliberately so:** every band, lean, price, decay and threshold *value*. A balance pass
owns those — shipping a guess is fine, calling it balance is not.

**Build order (owner, 2026-09-05):** *"order just to help we build dependencies first"* — the plan phase
covers **all ten** modules; the sequence below is dependency order, not a shipping slice.

**Ideal:** [species-build-ideal.md](species-build-ideal.md) — **sixteen owner decisions, audited before
spec** (§0.0 decisions, §11 audit, §12 what the spec owes). This map does not restate them; it maps them
onto buildable modules.

When this is approved: module specs land at `docs/architecture/species-build/spec-<module-id>.md`, plan
and tasks at `tasks/species-build-plan.md` / `tasks/species-build-todo.md`.

---

## 1. What this program is for

Give every demon species its own aptitude allocation, filled automatically from a per-species build
favour as that species levels through play, with the Zomboss doing the same visibly, and a priced
respec for the player who wants to override it.

**It is largely wiring, and the spec review trimmed that claim.** Most of the *plumbing* — the four
allocation scopes, the budget table, the persistence, the read functions, the nine Zomboss patterns —
ships already with zero production callers, and this program supplies the callers.

⛔ **But an earlier draft oversold it by one.** It called `ZombossPattern.ToAllocation(scope, budget)`
"the auto-distributor, already written". It is not: it scales one of **nine hand-authored** share
vectors. Turning 829 classified favours into balanced share vectors is `redistribution-plan` (module 4),
and that is **built from scratch**. Honest framing: the wiring is mostly there, the one real mechanism
is not.

---

## 2. Modules

Stable kebab-case ids. Referenced by every downstream plan and task.

| # | Module id | Responsibility | Depends on |
|---|---|---|---|
| 1 | `resolver-memo` | **Perf prerequisite, and it stands alone.** `AptitudeResolver.Resolve` recomputes per entity per apply — **526** tuning edges × a 48-lookup `Share()` each, roughly **25,000 dictionary lookups per entity resolve**, on the status/hit path too (`AptitudeSubsystem.cs:51-57`, `InjectorStatusBridge.cs:58`). Add a memo keyed `(Side, TypeId, **Theta**)` — Θ is per-actor and an earlier draft omitted it, which would have served stale builds — cleared on `Stats.Invalidate()` and at the match edges where the commander cache already refreshes (`MatchHost.cs:169,194`). **Semantically a no-op — zero goldens** — and it makes the per-species design net faster than today's commander-only path (ideal §11 A6) | — |
| 2 | `budget-source` | **Audit fix A1, pure correction, no new mechanism.** The `DemonType` budget source becomes **species level**, not almanac XP — an accumulation inverted the locked tier ordering by 176× at ordinary play levels. Fixes the guard test that could not see it (`PointBudgetTests.cs:84` holds its source constant on purpose), and corrects the three places that still say "almanac XP" (`spec-point-economy.md:37`, `PointBudget.cs:12-18`, `aptitudes.v5.json`'s `_scopeSourcesWhy`) | — |
| 3 | `species-xp` | **The progression signal, and one real identity question.** A species must have a per-player level. `rpg_actor_progression` already levels a PvZ *type* per player on `PlantPlaced` (`RpgXpAwardMap.FromActivity`'s own `PlantPlaced`/`ZombieSpawned` cases) and `LawnElementIndex` already maps `(Side, GameTypeId) → species` — so this may be a **join rather than a new store**, and the module must decide that rather than assume it. Also owns: the **expedition source** (standalone-first, ideal §4), and a verdict on the **per-placement faucet** (A10 — it rewards volume, the failure mode §9's prior art documents) | — |
| 4 | `redistribution-plan` | **The one genuinely new mechanism.** A deterministic, generation-time function that reads each species' classified favour and emits a **full share vector per species**, solved so corpus-wide allocated points land inside a tunable parity **band**. Output is checked-in, diffable, `--check`-regenerable static content — **shipped knowledge a player learns once** (decisions 3, 6, 7, 8, 11, 12, 16). No single-primary vectors; the per-species lean **falls out of solving for the band**, it is not a separate knob | — |
| 5 | `demon-type-allocation` | The `AllocationScope.DemonType` scope made real: persistence **keyed per-player by `speciesId`** (decision 10), baseline **composed at read time** from (static plan × that player's species level) rather than materialised — `AptitudeAllocation` is explicit that empty means all-zero, never an invented default, so `LoadAllocation` alone stops being sufficient (A9). Persists the **override only** ("save inputs, not computed totals") | 2 · 3 · 4 |
| 6 | `allocation-transport` | The wiring gap that keeps the whole thing off the lawn: `/api/aptitudes/{playerId}` returns a flat share map hard-coded to `Commander` (`RpgClient.cs:363-374`), with no species dimension. Adds it, caches N entries injector-side on the existing refresh cadence, and resolves `(Side, TypeId) → speciesId → allocation` through the **already-built** `LawnElementIndex` (A5). Must guard the empty-index bootstrap window, which has already produced one silent-zero defect here | 1 · 5 |
| 7 | `species-respec` | Decision 15: price **rises with the respec count on that species and decays over time** — churn-priced, not investment-priced. New persisted per-species counter + decay rate (both tunables). `RespecPolicy.PriceOf` gains a count argument, never a level. Its own feature endpoint and reason, and it **must use the ledger path the shipped sinks use** — `TrySpendSouls` has zero production callers (A4) | 5 |
| 8 | `zomboss-adaptive` | Decision 4: wire the nine already-authored patterns (`ZombossPatterns.cs:25-89`, zero production callers), add **level-up rotation + lose-streak counter-build, revealed after the next fight**, rate-limited so adaptation can never converge on "every player build is equally bad". **Battle and expedition surfaces only** — `ZombossPattern` appears in zero injector files and lawn waves are the host game's (A8). Its real seam is server-side (`WebMatchService`/`ExpeditionEndpoints`), and `ZombossCommanderAllocation` hard-codes the Commander scope today | **—** |
| 9 | `allocation-surface` | The player-facing override. A **layer over whatever stage the player is on**, never a route-away (GG-1), reachable in ≤3 pushes (GG-10). Renders with the already-authorised `AptitudePoints` unit class, whose rule binds: an estimate, **allowed only on a surface with a real allocation** | 6 |
| 10 | `battle-allocation` | ⛔ **Added 2026-09-05 by the spec-coverage audit — its absence was a real hole.** `WebMatchService.AptitudeChannelMods` reads **only the commander scope** and takes no species at all (`:415-418`). Without this, a player earns species build points **from expeditions** and those points **never apply in expeditions** — which half-defeats standalone-first, making the feature earnable game-closed but usable only game-open. Merges commander + species into **one** allocation and resolves once (resolving per scope and concatenating is explicitly the wrong number) | 5 |

**Build order:**

```text
resolver-memo ──────────────────────────┬─► allocation-transport ──► allocation-surface
budget-source ──┐                       │        (lawn)
species-xp ─────┼─► demon-type-allocation┤
redistribution-plan ┘                    ├─► battle-allocation      (battle + expedition)
                                         └─► species-respec

zomboss-adaptive                          (INDEPENDENT — battle/expedition only)
```

**`allocation-transport` (lawn) and `battle-allocation` (battle/expedition) are siblings, not a
sequence.** They are the two read paths a species allocation has to reach, and shipping only the first
produces the standalone-first incoherence module 10 exists to prevent.

No cycles. Modules 1–4 are mutually independent and may be built in parallel. **1 and 2 are the two
that should land first regardless** — both are corrections to shipped code, both are semantically
neutral, and both make every later module cheaper to prove.

---

## 2a. Coverage — every decision and audit obligation traced to a module

Run 2026-09-05 as a traceability audit. **A decision with no module is a hole**, and this is how module
10 was found.

| Ideal | Covered by |
|---|---|
| **1** souls price the respec | 7 `species-respec` |
| **2** both non-lawn sources | 3 `species-xp` (expedition built here) · 13 defers the web endpoint |
| **3** deterministic function, no single-primary | 4 `redistribution-plan` (`minAptitudesPerSpecies ≥ 2`) |
| **4** Zomboss both adaptations, revealed one fight late | 8 `zomboss-adaptive` |
| **5** no base regeneration | 4 (own tunable, does not reuse `impureSecondaryShareMilli`) |
| **6** the LLM does not do balance | 4 (*never call a model*) |
| **7** generation time, static shipped knowledge | 4 |
| **8** distribution parity as the objective | 4 |
| **9** ⛔ superseded by audit A2 | 7 (replaced by decision 15) |
| **10** per-player, by `speciesId` | 5 `demon-type-allocation` |
| **11** parity over total points, favour never overridden | 4 (test 7) |
| **12** a band, not a point | 4 (Phase 3 refuses out-of-band) |
| **13** web endpoint is another program's | 3 · map §4 |
| **14** budget source is species level | 2 `budget-source` |
| **15** respec price rises with count, decays | 7 |
| **16** per-species lean, falls out of the solve | 4 (Phase 1) |
| **A1** guard test cannot see a source-unit defect | 2 (test split in two) |
| **A4** `TrySpendSouls` has zero production callers | 7 (use the shipped ledger path) |
| **A5** species join is built; transport is the gap | 6 `allocation-transport` |
| **A6** resolver is uncached; memo makes it net faster | 1 `resolver-memo` |
| **A7** lean and ceiling are coupled | 4 (dissolved by decision 16) |
| **A8** Zomboss has no lawn presence | 8 (surfaces stated explicitly) |
| **A9** baseline storage semantics | 5 (compose at read, persist the override) |
| **A10** per-placement faucet rewards volume | 3 §3 — **recommendation given, verdict deferred to the owner** |
| **⛔ NOT COVERED — found by this audit** | **10 `battle-allocation`**, added |

### Four read paths, and they must all agree

A sweep for `LoadAllocation` callers outside `FusionRpg.Data` found **four** places a species allocation
has to reach. Only the first was covered by the original nine specs:

| # | Path | Where | Module |
|---|---|---|---|
| 1 | Lawn stat apply | `CheatState` → `AptitudeSubsystem` | 6 |
| 2 | Battle setup | `WebMatchService.cs:415-418` | **10** |
| 3 | Battle report `aptitude.snapshot` | `WebMatchService.cs:264` | **10** |
| 4 | Derived-stat inspection endpoint | `AuraDerivedEndpoints.cs:59` | **10** |

2 is a gameplay hole. **3 and 4 are diagnostics, and a diagnostic that disagrees with the game is worth
less than no diagnostic** — a battle report missing the term that decided the battle, and an inspection
endpoint confidently reporting channel values the lawn does not apply.

**Two coverage notes, both honest rather than tidy:**

- **A10 is the one obligation still open.** §12.1 of the ideal asked the spec to *state a verdict* on the
  XP faucet shape; `species-xp` §3 gives a recommendation and defers the call. That is weaker than the
  obligation, and it is deliberate — the choice changes how the grind feels, which is the owner's to
  make, not a spec's.
- **Decision 2 was only half-covered until module 10.** Points were earned from expeditions and would
  never have applied in them.

---

## 3. Why this split, at the two places it is not obvious

**`resolver-memo` is its own module rather than part of `allocation-transport`.** It fixes a cost that
**predates this program entirely** and benefits the shipped commander path immediately. Bundling it
would mean a species-aptitude change and a perf change landing together, so a regression in either
would be attributable to neither — and it would let "species aptitudes made the game slow" become the
story of a cost that was already there.

**`budget-source` is separate from `demon-type-allocation`.** It is a correction to already-shipped
code and a already-shipped test, provable on its own with no new mechanism, and it must land before
anything computes a `DemonType` budget or that thing is built on an inverted ordering.

---

## 4. What this program does not own

| Not ours | Whose | Why it is named here |
|---|---|---|
| Promoting web battle out of `FUSIONRPG_SIM=1` | `standalone-rpg`, or a dedicated module (decision 13) | This program needs the **signal**, not the endpoint. `species-xp` consumes it when it exists; the **expedition** path is what satisfies standalone-first here |
| Passive skills / build trees | [passive-tree-ideal.md](passive-tree-ideal.md) | This program allocates points; that one decides what a point can additionally buy |
| The commander-scope surface | [commander-surface-ideal.md](commander-surface-ideal.md) | `allocation-surface` is the **species** scope only |
| The `Aspect` tier | reverted 2026-08-31, not authorized to build | `decisions.md` *Demon program* row |
| Coefficient fitting | `class-system` module 12 `residual-fit` | Clean division: **this program shapes the inputs, `residual-fit` fits the coefficients** |
| Re-classifying the corpus to reduce the Onslaught skew | nobody — **ruled out** (decision 6) | The model classifies a category; balance is arithmetic |

---

## 5. Standards every module must satisfy

Not aspirations — each has a guard or audit that already runs.

| Standard | Check |
|---|---|
| Magnitudes are `long`; never `float`; widen before multiplying; divide by 1000 last; overflow throws | `python scripts/audit-overflow.py` |
| No hard progression ceilings (PS-8); bounded ratios exempt **and must say so in a comment** | [power/ssot-power-scale.md](power/ssot-power-scale.md) §11 |
| Balance surface is config — every rate/band/decay in `data/tuning/<domain>.v{n}.json`, never a literal | `python scripts/audit-magic-numbers.py` |
| One power ladder — no private `f(level)`; a budget from a level reads an index | `scripts/guard-power.ps1` |
| An aptitude is a **source, never a registered channel** | `SpecChannelClaimTests` |
| Progression never writes bare `hp`/`maxHp`/`atk`. ⛔ **Corrected by the spec review:** only ~8 of 526 aptitude edges are `progression.bonus.*`; the rest reach their consumers through the derived snapshot, so that is **not** the aptitude delivery path | `actor-hub-ssot.md:63` |
| SQL only inside `FusionRpg.Data` | `scripts/guard-dal.ps1` |
| Save inputs, never computed totals | `stat-system.md` |

---

## 6. Checkpoints

- **After 1 + 2** — the two corrections are green and semantically neutral: full suite green, **zero
  goldens moved**, and the fixed `PointBudgetTests` now compares real budgets from real sources and
  still passes.
- **After 4** — a checked-in redistribution plan exists for the whole corpus, `--check` is clean and
  byte-stable on a rerun, and the parity band is **measurably satisfied** (a pass/fail assertion, not
  an optimisation report).
- **After 6** — a species' allocation demonstrably changes that species' stats **on a live lawn**, and
  the resolver memo means the path is no slower than before. Owner-run live check.
- **After 9** — the program closes: a player can see a species' auto-built distribution, override it,
  pay to respec, and meet a Zomboss whose pattern is revealed after the fight.

---

## 7. Related

[species-build-ideal.md](species-build-ideal.md) · [class-system-map.md](class-system-map.md) (this
program is its module 14's named follow-up) · [demon-seed-map.md](demon-seed-map.md) (supplies the
favour) · [power/ssot-power-scale.md](power/ssot-power-scale.md) · [decisions.md](decisions.md)
