# Implementation Plan: effect-atom (Secondary effect SSOT)

Map: [../docs/architecture/effect-atom-map.md](../docs/architecture/effect-atom-map.md) · Specs: [../docs/architecture/effect-atom/](../docs/architecture/effect-atom/) (19 spec files, 20 module ids with the E14 split) · Tasks: [effect-atom-todo.md](effect-atom-todo.md).
Named pair per repo convention — `tasks/plan.md`/`todo.md` hold the perf-v3 stream.

**Status (2026-08-22):** spec **closed** after four adversarial passes. **E1 and E2 built.** Next is E3.

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

**Build order:**
`E1 ‖ E2 ‖ E3 → E4 → E5 → E6 → E13 → E7 → E8 → E15 → E19 → E14a → E11 → E18 → E9 → E10 → E14b → E12`, with E16 and E17 after E14b.

Six corrections from the audit: E14 splits (importer before E11) · `ActorPowerCache` moves to E9 · `effect_curve` DAL joins E2 · new **E19 `compiled-push`** (E7's output had no delivery path) · E18 sequenced before E10 · E8's covered tables become a versioned registry.

## Waves and checkpoints

| Wave | Modules | Checkpoint |
|---|---|---|
| 1 — the spine | ✅ E1, ✅ E2, E3, E4, E5 | **A** — spine compiles, **nothing in the game changed**. If a golden moves here, stop. **B** — E5 reviewed unblocks combat-action A1 |
| 2 — doing work | E6, E13, E7, E8, E15, E19 | **C** — a bound container compiles to a Foundation grant, the push delivers it, the Funnel runs it. The content hash **exists** at E8 but is not yet stamped into reports — that is E12's, because a stamped field *is* a golden diff |
| 3 — the proof | E14a, E11 | **D** — 16 defs are rows, **49** fixtures byte-identical. "A new effect costs one row" is true here or the design failed |
| 4 — power | E18, E9, E10 | E18 precedes E9/E10 — the matchup read consumes its matrices |
| 5 — expensive | E14b, E12 | **E** ⛔ goldens move; owner sign-off; must not collide with the battle-timeline gate |
| Later | E16, E17 | after E14b |

## Definitions

[definitions.md](../docs/architecture/effect-atom/definitions.md) pins the ~40 things the specs referenced and never defined — units, tolerances, id grammars, NULL semantics, orderings, the hash algorithm, the 33 rejection codes. **Definitions win over any spec** until that spec is rewritten. §13 is the defect log; §14 holds the ICD-key and lifecycle rules.

## Risks

| Risk | Mitigation |
|---|---|
| ⛔ **The whole spec set is untracked in git** | `docs/architecture/effect-atom/`, the map, and this pair show as `??`. One bad command destroys them with no recovery — that happened on 2026-08-22 and cost a full replay from session logs. **Owner: commit this directory.** Nothing else on this list is as cheap to fix or as expensive to ignore |
| **The pricing function is unsolved** (§13 D1–D4) | Accepted limitation, owned by **E9 at position 15**. Budget ceilings, display sorting, and AI reads all work without it; only *trusting the number for balance* waits, which the ideal scoped that way from the start. Pricing multiplicative effects has no closed form — it needs a fitted sweep, which E9's coefficients were always scheduled for |
| **E1's param schemas do not match their executors** (§13 D7) | Five schemas re-derived from executor reads before **E7**, where a wrong schema first produces a wrong grant. Mechanical, no decision in it |
| E12 re-bless collides with the battle-timeline re-bless | E12's predicted delta is **zero** — sequence it first, attribution stays clean |
| E12 cannot bind its one trait | `stat.derived` is quarantined `None/None/None` (D6). **E12 ships the first consumer and re-opens the battle cell** — that is part of the module, not a later favour |
| E17 interop signatures differ from the names found | `SetEmbered`/`SetJalaed`/`SetKelped` confirmed in assembly metadata; confirm signatures before wiring. `charm_pulse` is a **def error**, not missing wiring |
| Hot-path regression from the runner | E13 budget guard in CI (median of 9, fails at >1.5×); allocation probe with a stated method. **No** no-recursion test — it would disqualify the measured winner |
| Injector rolls are not replayable | `Environment.TickCount` seeds them today. **E19's push carries a per-match seed**; until then E15's exact-count rows run on a Core-hosted double |
| Spec churn after combat-action builds on E5 | Checkpoint B is a review gate, on purpose |

## Out of scope

Damage consumer/applier · AI layer · world triggers and `LaneCost` · battle consumer growth · per-entity primary defense (waits on perf O5) · container **content** beyond E11's migration · the power coefficient sweep (E9 authors the table; fitting it is later work).
