# Implementation Plan: effect-atom (Secondary effect SSOT)

Map: [../docs/architecture/effect-atom-map.md](../docs/architecture/effect-atom-map.md) · Specs: [../docs/architecture/effect-atom/](../docs/architecture/effect-atom/) (19 spec files, 20 module ids with the E14 split) · Tasks: [effect-atom-todo.md](effect-atom-todo.md).
Named pair per repo convention — `tasks/plan.md`/`todo.md` hold the perf-v3 stream.

**Status (2026-08-23):** all 21 spec'd modules (waves 1–5) are **built, tested, and green** —
Checkpoints A–E reached, no golden re-blessed. A completeness audit the same day
([effect-atom/completeness-audit.md](../docs/architecture/effect-atom/completeness-audit.md)) found
that almost none of it reaches the running game: no host loads a content table, nothing runs the
importer, nothing creates a binding, and E17's status-stat payload has a parser with no applier.
**Wave 6 closed same-day, all six modules: E20–E25 fully built and proven.** E23 was first reported
partial — `EffectSeedCatalog` deletion "deferred, needs a converter that doesn't exist" — and that
claim was wrong: `AtomPushCodec.ToDef` already shipped the converter at E19, found only after a
Stop-hook challenge forced a closer re-read of `MigrationParityTests.cs` than the first pass gave it.
Corrected: `EffectSeedCatalog` is deleted from production Core, replaced at all five call sites by a
checked-in generated catalog, proven safe by a new execution-parity suite (19 real scenarios through
a real `EffectBag`, not just DTO comparison) before the swap and after. Checkpoint F reached for all
six modules; see [tasks/effect-atom-todo.md](effect-atom-todo.md) E23 for the full account.

## Overview

Build the missing **Secondary** layer above sealed Foundation: atoms (smallest effect, values + power in SQLite), containers (skills / traits / items / passives), instances (frozen rolls), bindings (owner scope), a compiler into Foundation grant shapes, and a Hot runner for what Foundation cannot express. Richness comes from **families × tiers × containers**; the machine stays at 5 attach points and 12 kinds.

Grounded by a six-way repo sweep (2026-08-22) that corrected five counts we had been quoting and found the documented `channel` enum was fiction. Vocabulary SSOT: [atom-catalog-ssot.md](../docs/architecture/effect-atom/atom-catalog-ssot.md).

## Architecture decisions (from the specs — locked)

- **The atom layer compiles; it never applies.** Output is the `EffectGrantDto` shapes the Funnel already accepts. Foundation, its contract version, and all three guards are untouched.
- **Compile/run split, per *atom*:** an atom whose `when` is an FT* trigger plus simple filters compiles to an ordinary grant (zero runtime cost); one needing a predicate tree or per-binding state goes to the runner. **Items have no behaviour — actors do**, so there is no binding-level coherence to preserve. An atom fitting neither path is rejected.
- **Rejection over silence.** Eight documented silent failures (G1–G8) become load-time or bind-time rejections with typed reasons. Whole-row rejection; no disabled-on-error state.
- **Code owns logic, SQLite owns values** — with the rule that decides it: *a thing may be data if adding a row changes behaviour without new code*. Elements are data; channel families are code.
- **Server compiles and pushes**; the injector never holds content rows. Per-hit rolls stay local — **not** for frame latency (the pipeline is record-then-drain and *delayed effects* are its designed worst case), but for chattiness, pointer lifetime, and offline resilience. Determinism comes from the server owning the **seed**.
- **`icd_key` is a compile-time grouping key.** Atoms sharing one compile into a single grant whose `Triggers` is the union of theirs. Foundation already holds a trigger list and its ICD key already excludes the trigger, so multi-trigger defs migrate with **no runtime change**.
- **`OnGranted`/`OnRemoved` are lifecycle, not content.** `stat.modify` / `stat.derived` declare no trigger; E7 compiles them `EffectType = Passive`.
- **Hot path:** no dictionaries, no string comparison, no allocation. Budget ≤ 50 ns/atom on the CI reference machine. *(The "no recursion" law was withdrawn: the 7 ns benchmark winner is a typed object graph, which recurses.)*
- **Power is a vector**, stored; AI reads the vector and the matchup read, never the display scalar. **The pricing function is an accepted limitation owned by E9** — see Risks.

## Dependency graph

See the single dependency table in [the map §4](../docs/architecture/effect-atom-map.md#4-dependency-graph-and-build-order). It is not duplicated here — two copies is how the last two audits found contradictions.

**Build order (waves 1–5, all built):**
`E1 ‖ E2 ‖ E3 → E4 → E5 → E6 → E13 → E7 → E8 → E15 → E19 → E14a → E11 → E18 → E9 → E10 → E14b → E12`, with E16 and E17 after E14b.

Six corrections from the audit: E14 splits (importer before E11) · `ActorPowerCache` moves to E9 · `effect_curve` DAL joins E2 · new **E19 `compiled-push`** (E7's output had no delivery path) · E18 sequenced before E10 · E8's covered tables become a versioned registry.

**Wave 6 order:** `E20 → E22`; `E21 ‖ E23 ‖ E24 ‖ E25` are independent of each other and of E20/E22 and
can build in any order or in parallel. See the [module table](#wave-6--the-seams-2026-08-23) below for
why each dependency (or absence of one) holds.

## Waves and checkpoints

| Wave | Modules | Checkpoint |
|---|---|---|
| 1 — the spine | ✅ E1, ✅ E2, ✅ E3, ✅ E4, ✅ E5 | **✅ A** — spine compiles, nothing in the game changed. **✅ B** — E5 reviewed, unblocked combat-action A1 |
| 2 — doing work | ✅ E6, ✅ E13, ✅ E7, ✅ E8, ✅ E15, ✅ E19 | **✅ C** — a bound container compiles to a Foundation grant, the push delivers it, the Funnel runs it |
| 3 — the proof | ✅ E14a, ✅ E11 | **✅ D** — 16 defs are rows, 49 fixtures byte-identical |
| 4 — power | ✅ E18, ✅ E9, ✅ E10 | E18 precedes E9/E10 — the matchup read consumes its matrices |
| 5 — expensive | ✅ E14b, ✅ E12 | **✅ E** — goldens measured, zero moved, no sign-off needed |
| Later | ✅ E16, ✅ E17 | after E14b |
| **6 — the seams** | **E20–E25** | **F** — see below. Closes the 2026-08-23 completeness audit |

### Checkpoint F — the layer is live, not just tested

Waves 1–5 proved every module correct **in isolation**: given inputs it built itself, each one
produces the right output, and the suites say so. Checkpoint F proves the opposite direction — that a
row an author edits on disk is the row the running game composes with, with nothing hand-wired in
between. It is met when, from a **clean checkout**:

1. `deploy-play.ps1` imports `data/seed/**`, boots the server, and the server's own battle resolve
   uses the imported roster/power/policy — not the shipped code fallback. A changed seed value (e.g. a
   test element added to the roster) is visible in a composed snapshot without touching a `.cs` file.
2. A live `rally` (or any of the four `ModifyStat` statuses) applied in a running match measurably
   changes a composed stat, and the change is gone once the status expires.
3. Every table in `ContentHashRegistry.Current` has a **production** reader — enforced by a standing
   guard test, not by re-running this audit by hand next time.

Checkpoint F is an **owner-observable** proof, on purpose — the whole finding of the audit is that
green tests were not sufficient evidence the layer worked, so the closing checkpoint cannot be "tests
are green" again.

## Test coverage mandate for Wave 6 (owner directive, 2026-08-23)

This layer underlies every other feature — items, traits, skills, statuses, world buildings all
compile through it. **Wave 6 is not exempt from the corpus-first discipline that found E11's seven
defects; it needs a stricter version of it, because a wiring bug here is invisible everywhere at
once.** Every module below carries three kinds of test, not one:

1. **Unit** — the new code in isolation (a plugin's `Contribute`, a loader's table-to-static mapping,
   a cache's invalidation).
2. **Integration / seam** — the thing the audit found missing: an actual host boot, an actual import,
   an actual composed number changing because a row changed. This is what Waves 1–5 did not have.
3. **Regression guard** — a test that fails *if this exact gap reopens*, so the next module that adds
   a hashed table or a parsed payload cannot repeat E17's or E16's mistake silently. Where one already
   exists (E13's ns/atom guard, the four boundary guards) extend it; where none exists, add one.

No wave 6 module is done at "the new class has tests." It is done when an existing shipped-but-inert
capability is demonstrably live, proven by a test that would have caught it being inert.

## Definitions

[definitions.md](../docs/architecture/effect-atom/definitions.md) pins the ~40 things the specs referenced and never defined — units, tolerances, id grammars, NULL semantics, orderings, the hash algorithm, the 33 rejection codes. **Definitions win over any spec** until that spec is rewritten. §13 is the defect log; §14 holds the ICD-key and lifecycle rules.

## Wave 6 — the seams (2026-08-23)

Six modules. Each closes one lettered finding from
[completeness-audit.md](../docs/architecture/effect-atom/completeness-audit.md) §2–3. Full acceptance
criteria, verify commands and the three-tier test plan for each are in
[effect-atom-todo.md](effect-atom-todo.md) — this table is the map, not a duplicate of it.

| id | Name | Closes | Depends on | One-line shape |
|---|---|---|---|---|
| **E20** | `content-boot` | A2, A3 | — | `RpgStore.LoadContentIntoRuntime()` calls `ElementTable.Use` / `PowerTables.Use` at host boot; `deploy-play.ps1` runs the importer against the live data dir first |
| **E21** | `status-stat-applier` | A1 | — | A stat plugin (or composer hook) that turns `StatusRuntime.ForHost(...)`'s live instances into bag contributions via `StatusStatPayload.ToModifiers` |
| **E22** | `channel-policy-reader` | B1 | E20 | A `ChannelPolicyTable` static (same shape as `ElementTable`/`PowerTables`), loaded by E20's boot call, read by `DerivedStatRegistry` with the code constant as fallback |
| **E23** | `content-codegen` | B2 | — | `tools/ElementEnumGen`: generates `ElementTypeId` from the roster, deletes `EffectSeedCatalog` (E11 Step 4), generates the trait/roster C# literals from `data/seed/**` |
| **E24** | `validation-in-ci` | B4, B5 | — | `AtomImporter --validate` runs `ContentValidation` and fails the process on a finding; `Server.Tests` and `E2E.Tests` added to `ci.yml` |
| **E25** | `compose-channel-cache` | B3 | — | Cache `AllCombatChannelIds`, invalidated by a version counter on `ElementTable.Use`/`UseScoped`; a compose-path ns budget guard beside E13's |

**A4 (nothing creates a binding) is deliberately not a wave 6 module.** It is an item/skill/trait
*feature*, which §7 of the map assigns to those programs. Wave 6 instead adds one sentence to the map
and this plan recording that the runtime is inert until one of them binds a container — done in this
edit, so the next reader is not misled by twenty-one-and-then-twenty-seven green rows.

## Risks

| Risk | Mitigation |
|---|---|
| ⛔ **The whole spec set is untracked in git** | `docs/architecture/effect-atom/`, the map, and this pair show as `??`. One bad command destroys them with no recovery — that happened on 2026-08-22 and cost a full replay from session logs. **Owner: commit this directory.** Nothing else on this list is as cheap to fix or as expensive to ignore |
| **The pricing function is unsolved** (§13 D1–D4) | Accepted limitation, owned by **E9 at position 15**. Budget ceilings, display sorting, and AI reads all work without it; only *trusting the number for balance* waits, which the ideal scoped that way from the start. Pricing multiplicative effects has no closed form — it needs a fitted sweep, which E9's coefficients were always scheduled for |
| E17 interop signatures differ from the names found | `SetEmbered`/`SetJalaed`/`SetKelped` confirmed in assembly metadata; confirm signatures before wiring. `charm_pulse` is a **def error**, not missing wiring |
| Hot-path regression from the runner | E13 budget guard in CI (median of 9, fails at >1.5×); allocation probe with a stated method. **No** no-recursion test — it would disqualify the measured winner |
| Injector rolls are not replayable | `Environment.TickCount` seeds them today. **E19's push carries a per-match seed**; until then E15's exact-count rows run on a Core-hosted double |
| Spec churn after combat-action builds on E5 | Checkpoint B is a review gate, on purpose |
| **E20's boot call mutates process-global statics** (`ElementTable._global`, `PowerTables._current`) | Its own tests must not run parallel with any other test reading `.Current` — isolate in a dedicated xunit collection, reset in `finally`. The pattern already exists (`UseScoped`); the risk is a new test class forgetting to use it |
| **E21's design point is genuinely open**: does stat compose already re-run per tick (so `ForHost`'s natural expiry is enough), or is it baked once (so `ToModifiers`'s source-tagged withdraw is load-bearing)? | Read `StatComposer`'s call sites and `EntityApply`'s scheduling **before** writing E21 — this is a "read before propose" case, not a guess |
| **E23's codegen touches files three other modules (E11, E12, E18) already ship** | Generate into a clearly-marked file (`*.Generated.cs`) and diff the generated trait/roster output against the current hand-kept literals before deleting anything — a silent value change here moves battle goldens |

## Out of scope

Damage consumer/applier · AI layer · world triggers and `LaneCost` · battle consumer growth · per-entity primary defense (waits on perf O5) · container **content** beyond E11's migration · the power coefficient sweep (E9 authors the table; fitting it is later work) · **a producer of instances/bindings (A4)** — belongs to whichever item/skill/trait program binds a container first.
