# Implementation plan: `species-build`

**Program:** `species-build` · **Map:** [docs/architecture/species-build-map.md](../docs/architecture/species-build-map.md)
(ten module specs, approved 2026-09-05) · **Ideal:** [species-build-ideal.md](../docs/architecture/species-build-ideal.md)
(sixteen owner decisions, audited before spec)

**Tasks:** [species-build-todo.md](species-build-todo.md) — **30 tasks, 6 phases, 6 checkpoints.**
Every task's acceptance criteria and verification live there; this document carries the ordering,
the reasoning, and the risks.

**Status: awaiting review. No build authorized.**

---

## 1. Overview

Give every demon species its own aptitude allocation, filled automatically from a per-species build
favour as that species levels through play, with the Zomboss doing the same visibly, and a priced
respec for the player who wants to override it.

**Most of the plumbing already ships with zero production callers** — the four allocation scopes, the
budget table, the persistence, the read functions, the nine Zomboss patterns. This program supplies the
callers. **One mechanism is genuinely new**: the deterministic redistribution function that turns 829
classified favours into balanced share vectors.

---

## 2. Architecture decisions this plan inherits

All settled and **not reopened by any task**. Restated because a task list is read without its ideal.

| | Decision |
|---|---|
| **Budget source** | `max(0, speciesLevel − 1) × rate` — an **index**, never an accumulation. Almanac XP inverted the locked tier ordering 176× at ordinary play levels |
| **Zero at level 1** | An unrecorded actor defaults to `Level = 1`, so a level-1 budget of zero is what keeps every golden byte-identical — and it is what *"earn bonus when specie level up"* already meant |
| **The plan is static content** | Generation-time, checked in, `--check`-regenerable. A species' build is learned once and stays true. **No runtime randomisation of the baseline** |
| **Parity over total points, in a band** | Never over the primary field — the function steers *remainders*, so it never overrides its own input. A band (floor + ceiling), not minimised deviation |
| **Per-species lean** | Crowded primaries lean less, rare primaries lean more. The lean **falls out of solving for the band**, it is not a knob |
| **Baseline composed, override persisted** | *Save inputs, not computed totals.* `LevelChangePipeline` therefore needs **no handler** |
| **Scopes sum before share** | Merge into **one** `AptitudeAllocation` and resolve once. Per-scope resolve + concatenate is *"a different (and wrong) number"* |
| **Respec prices churn** | Rises with respec count on that species, decays over time. First override free, revert free. Souls, via the ledger path the shipped sinks use |
| **Zomboss** | Both adaptations, revealed **one fight late** — a symmetric information lag, rate-limited. Battle and expedition only |
| **XP faucet** | **Both** terms: a larger `runCompletionAward` beside a smaller `placementAward`, both tunable. Kills the grind vector by ratio, not by ban |
| **Surface** | `AptitudesLayer.tsx` — a layer over the stage, ≤3 pushes |

---

## 3. Ordering — why this sequence

**Dependency order, per the owner: *"order just to help we build dependencies first."*** All ten
modules are planned; this is not a shipping slice.

Three ordering constraints are real rather than stylistic:

1. **The two corrections go first (P0).** `resolver-memo` fixes a cost that predates this program; if it
   lands with the species work, a regression in either is attributable to neither, and *"species
   aptitudes made the game slow"* becomes the story of a cost that was already there. `budget-source`
   must precede anything that computes a `DemonType` budget, or that thing is built on an inverted
   ordering.
2. **Both read paths land together (P3).** Shipping the lawn without battle creates exactly the
   incoherence module 10 exists to prevent — points earned from expeditions that expeditions do not
   honour — and it is the state you would be *testing in*.
3. **The plan precedes the allocation (P1 → P2).** There is no baseline to compose without one.

`zomboss-adaptive` is **independent** and is scheduled late only because nothing else waits on it.

---

## 4. Task list — ordered index

Full acceptance criteria in [species-build-todo.md](species-build-todo.md).

### Phase 0 — corrections (modules 1, 2)
`T0.1` memo + Θ-in-key · `T0.2` invalidation bumps · `T0.3` split the guard test · `T0.4` the
`(level−1)` rule + three citations → **Checkpoint 0**

### Phase 1 — foundations (modules 3, 4)
`T1.1` species progression row + migration · `T1.2` lawn projection · `T1.3` the run award and its
ratio · `T1.4` expedition source (game-closed proof) · `T1.5` planner phases 1–2 · `T1.6` phase 3
refusal + canonical serializer · `T1.7` `DemonBuildPlanGen` + the committed plan · `T1.8` **CI gate**
→ **Checkpoint 1**

### Phase 2 — the allocation (module 5)
`T2.1` scope key + compose-at-read · `T2.2` override, budget enforcement, endpoints · `T2.3` seam
guard → **Checkpoint 2**

### Phase 3 — both read paths (modules 6, 10)
`T3.1` server payload gains `species` · `T3.2` Core `SpeciesAllocationSource` · `T3.3` injector cache
and refresh · `T3.4` battle setup reads species · `T3.5` the two diagnostic paths · `T3.6` **owner-run
live lawn check** → **Checkpoint 3**

### Phase 4 — economy and AI (modules 7, 8)
`T4.1` respec price + Soul resource · `T4.2` counter, decay, atomic spend · `T4.3` respec endpoint ·
`T4.4` pattern selector · `T4.5` scope argument + pattern on setup/report · `T4.6` the server seam and
the reveal → **Checkpoint 4**

### Phase 5 — the surface (module 9)
`T5.1` contract + bus hooks · `T5.2` panel in `AptitudesLayer` · `T5.3` GG-1/GG-10 conformance + E2E
→ **Checkpoint 5, the build closes**

### Phase 6 — playability gaps (modules 4, 9) — *added by the playability audit, §4b*
`G1` plan keyed by runtime `speciesId` · `G2` unresolvable key fails loud · `G3` plan coverage for the
17 uncovered species · `G4` a real door to contract binding · `G5` honest empty state at budget 0 ·
`G6` error branch precedence · `G7` confirm gated on the price having loaded
→ **Checkpoint 6, the program is actually playable**

---

## 4a. Coverage audit, 2026-09-05 — three real gaps, all now closed

Run before review: every spec's success criteria traced to a task. **All ten modules were covered on
their stated criteria.** Three things no spec lists as a success criterion — because they are
*plumbing every module silently assumes* — had **no task at all**:

| Gap | Why it was invisible | Closed by |
|---|---|---|
| ⛔ **No host wiring for three new tuning files.** *Core reads no file — hosts load and inject*, and `aptitudes.v5.json` is `Configure`d in **both** `Program.cs` and `RpgHost.cs`. Three new files had a loader in no task | Each spec lists its tuning file under *project structure*, so it looked owned. Nothing owned **loading** it | Acceptance added to `T1.1`, `T1.5`, `T4.4` — **and the audit narrowed the work**: all three are **server-only**. None needs injector wiring, because the injector never computes a level, never sees the plan (it receives *points*), and never meets the Zomboss |
| ⛔ **No CI gate for the generated plan.** CI already gates `DemonSpeciesGen --check` and `FamilyExpandGen --check` with a `$LASTEXITCODE` throw; a stale build plan would have shipped silently | The class-system standard says each module wires its own gate *as it lands* — easy to defer to "the end", which is where it gets forgotten | **New task `T1.8`**, in Phase 1 rather than at the end |
| ⛔ **No `AptitudesUpdated` broadcast on a species save.** Without it a respec would not reach the lawn until a match edge | The endpoint already broadcasts for commander saves, so it looked handled | Acceptance added to `T2.2` — **and this repo has already shipped this exact bug once**: a WebGroup-only send left the injector's allocation stale until the next reconnect, found by live probe 2026-08-30 |

Five smaller omissions also closed: the planner's overflow test (`T1.5`), the inertness-preserved and
behaviour-neutral-hoist assertions (`T3.4`), a per-path refresh test for the Core-testable half of the
cache (`T3.3`), and a warning that `species-build.v1.json` is **shared between `m4` and `m7`** so the
later task adds keys rather than rewriting the file (`T4.1`).

**The pattern worth remembering:** every gap was a thing *between* modules rather than *inside* one. A
per-module spec review cannot find them, because each spec is individually complete.

---

## 4b. Playability audit, 2026-09-05 — five findings, and again none of them inside a module

Run **after** Checkpoint 5 declared the program complete, against a **live server and four real save
databases** rather than the test suite, from four perspectives: cold-start wiring, combat effect, the
XP and soul economy, and the player's own click path. It retracted this program's "PROVEN COMPLETE"
claim. Three of the five findings are **silent** — they render a plausible-looking screen instead of an
error — which is precisely why every suite stayed green.

| Finding | Why it was invisible | Closed by |
|---|---|---|
| ⛔ **Every species baseline is empty.** The generated plan is keyed in seedsmith anchor PascalCase (`FumeShroom`); every runtime lookup asks the compiled catalog's lowercase id (`fumeshroom`) through an **ordinal** dictionary. Exact overlap across 829 plan keys and 84 live species: **0** | `SharesFor` returns `EmptyShares` on a miss by design — no throw, no log. Both suites `Configure` a hand-built plan keyed `"fumeshroom"`, the *lookup* id, so no test ever loaded the shipped file's `"FumeShroom"` against the real roster | `G1` (write the real `speciesId`, per `spec-redistribution-plan.md` §"Shape") and `G2` (fail loud, per that same spec's §"Tuning" rule for a missing key) |
| ⛔ **No door to the feature.** Pacts is gated on `hasAnyContract`; the only binding UI is `/demons`, which nothing in the app links to. Pacts' empty state names a page the player cannot reach | Each surface is individually correct. The missing thing is a **link between two of them**, which no module owns | `G4` — named as crossing into the shell/rail rather than absorbed as this program's code |
| ⛔ **An unlevelled species renders as an unexplained zero panel** — twelve zeroes under the copy "You're running the shipped build", which is false, with the disabled-Save reason hidden in a `title=` tooltip | Budget `max(0, level-1) x 4` is correct math; the *copy* assumes a build exists. Compounds the first finding — two independent causes, one identical broken-looking screen | `G5` |
| **A failed load renders as "Loading..." forever** — `!state.data` is tested before `isError`, so the error branch and its retry button are unreachable dead code | No test covers the failure path | `G6` |
| **A priced respec can spend souls with no confirm** — `isFree` falls back to `true` while the price query is pending or errored | The happy path is covered; the pending-price path is not | `G7` |

**What the audit confirmed as genuinely working**, and what the fixes must not regress: species XP does
accrue from real lawn play (real `plant_place` -> `peashooter` and `zombie_spawn` -> `normalzombie`
rows in a live save — evidence static reading could not produce); species points do reach lawn,
web-match and expedition combat via the scope-blind allocation sum; one completed match (104 XP) clears
the 60-XP threshold to a 4-point budget; every tuning hub is configured at real cold start with its
files deployed to `dist/`.

**§4a's closing line predicted this exactly:** *"every gap was a thing between modules rather than
inside one."* It happened again, one layer out — plan file to catalog, roster UI to binding UI, species
level to budget copy. The durable fix is `G1`'s last acceptance criterion and `G2`: **test the real
artifact against the real roster**, rather than a fixture that agrees with the code under test.

**Ordering:** `G1` first — it is XS and buys back 67 of 84 species immediately. `G2` next, so the class
of defect cannot silently return. Then `G4`, which is what makes the feature reachable at all, and the
three XS surface-honesty fixes (`G5`–`G7`) which are independent of each other. `G3` is content work
and last, because `G2` makes its absence visible rather than silent in the meantime.

---

## 5. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **A golden moves in P2/P3.** A nonzero species baseline does not merely add modifiers — `Share()` divides by `GrandTotal()` across all four scopes, so it **changes the value of every commander-derived modifier** for that actor | **High** — silent, and it would look like a balance change | The `max(0, level − 1)` rule makes a never-levelled species carry exactly zero. `T0.4` lands it **before** anything can compose a baseline, and `T3.4`'s acceptance is *every battle and expedition golden byte-identical*. If one moves, that rule broke — do not re-bless |
| **The memo serves a stale build.** Its key must carry every input `Resolve` reads; an early draft omitted `Θ` and the equivalence test would still have passed | **High** — silent wrong stats | `T0.1` includes the Θ-honoured test explicitly: two contexts identical but for `Θ` must resolve differently |
| **A silent zero from the un-configured index.** `LawnElementResolverHost` returns a throwaway empty index before `Configure()`; this codebase has already shipped one 222-point allocation that reached the writer as nothing | Medium-high | `T3.2`'s acceptance requires an un-configured index to **report**, not return empty. Tested in Core with a fake resolver, no game needed |
| **Injector-side work is unverifiable offline** (`T3.3`) | Medium | Follows this repo's established precedent: the Core half is fully tested with an injected lookup, the injector half is verified by direct read plus the owner-run live check in `T3.6` |
| **The parity band may be infeasible** at chosen tunables | Medium | `T1.6` makes the tool **exit non-zero naming the offending aptitudes** rather than emit a near-miss. A refusal is the designed outcome, not a failure |
| **Concurrent edits drift line numbers.** Another stream is actively editing this repo; one citation in the specs was already stale within the session | Low, but it wastes time | Tasks cite **symbol names** over line numbers wherever possible. Re-grep before trusting any `file:line` in a task |
| **`ZombossCommanderAllocation` hard-codes the Commander scope**, so module 8 carries a signature change its first draft did not admit | Low | Called out in `T4.5` rather than discovered during `T4.6` |

---

## 6. What this plan does not do

- **No git operations.** Per `AGENTS.md`, every task ends with the work in the tree and a suggested
  commit message; the owner commits.
- **No balance values.** Every band, lean, price, decay and threshold ships as a named placeholder with
  its `_why` note. A balance pass owns the numbers — shipping a guess is fine, calling it balance is not.
- **No web-battle endpoint.** Decision 13: that promotion belongs to another program. `T1.4`'s
  expedition path is what satisfies standalone-first here.
- **No `Aspect` tier.** Reverted 2026-08-31 and not authorized to build.

---

## 7. Open questions

**None.** All sixteen decisions are settled (ideal §0.0), the pre-spec audit's three findings were
re-decided (§12), and the two product questions the specs deferred — the XP faucet shape and the
surface host — were answered on 2026-09-05 and are recorded in their own specs.

The only things deliberately left unset are **tunable values**, which a balance pass owns by rule.

**Phase 6 adds no open question either.** `G4` needs one product call — where the door to contract
binding lives — and it ships with a named default rather than a gate: **put it on the Pacts empty
state that already names the Demons roster**, the smallest fix that invents no new information
architecture. A Demons rail entry is the alternative if the owner wants it discoverable before a first
contract exists; it is a one-line difference and does not block the task from starting.
