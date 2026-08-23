# Plan: Almanac — capture-fix, recipes-fix, seed (BE)

Map: [docs/architecture/almanac-map.md](../docs/architecture/almanac-map.md)
Specs: [almanac/spec-almanac-capture-fix.md](../docs/architecture/almanac/spec-almanac-capture-fix.md) ·
[almanac/spec-almanac-recipes-fix.md](../docs/architecture/almanac/spec-almanac-recipes-fix.md) ·
[almanac/spec-almanac-seed.md](../docs/architecture/almanac/spec-almanac-seed.md)

Scope: the three modules locked and ready. `almanac-spawn-coverage` stays at spec stage — its own
file has three unresolved owner decisions (destructive-to-active-level blast radius, zombie
spawn side-effects, pacing) and explicitly defers its own plan until those are answered.

All four specs were adversarially reviewed this session (21 findings; every confirmed-valid one
fixed in the spec files before this plan was written) — see the "Corrected after adversarial
review" notes inline in each spec for what changed and why.

## Dependency graph

```
Module 1: injector fixes (live-verified)        Module 2: almanac-seed (fully unit-testable)
  T1 skip window capture in the sweep              T4 schema + DTO + identity/flavor rebuild
       │  (same file: GameHooks.cs,                     │
       │   sequence, don't parallel-branch)              ├─→ T5 cost/cooldown parsing
       ▼                                                 ├─→ T6 combat stats baseline (spawn_stats)
  T2 stop the auto-latch (RequestTypeCatalog            │        (both T5/T6 independent of each
     fires EnqueueRecipes at boot, before               │         other, both depend only on T4)
     any level exists — confirmed live)                 ▼
       │                                            T7 REST endpoints (needs T4-T6)
       ▼                                                 │
  T3 diagnose remaining silent-drop points               ▼
     (dict-null / cast-catch / null-client /        T8 external enrichment (optional add-on,
     possible server-side ProjectRecipes loss)           separate table, depends on T7)
```

Module 2 has no code dependency on Module 1 — it reads `type_almanac_dump`/`spawn_stats` as they
already exist and can be built/tested in parallel with Module 1. Sequencing Module 1 first (as
listed below) is about not building the seed contract on top of known-contaminated raw dumps, not a
hard blocker — see the map's "Why this shape" section for the hard-vs-soft distinction.

## Ordering rationale

- T1 before T2/T3: both touch `GameHooks.cs`; landing T1 first avoids a merge, and T1 is the
  simpler, already-fully-designed fix (T2/T3 still have an open diagnosis step).
- T2 before T3: T2 is a confirmed defect from static reading alone (`RpgClient.cs:128` →
  `RequestTypeCatalog` → `EnqueueRecipes` at boot) — fixing it is not conditional on live diagnosis.
  T3's exact remaining fix genuinely is conditional on what the live check after T2 shows.
- T4 before T5/T6: both add columns/logic to the same rebuild function `RebuildAlmanacSeed()`;
  T4 establishes the table/transaction/stale-row-deletion skeleton first. T5 and T6 are independent
  of each other (different source tables: `type_almanac_dump` prose vs. `spawn_stats` numbers) and
  can be done in either order or split across two sessions.
- T7 after T4-T6: REST just exposes what the rebuild already produces.
- T8 last, optional: enrichment is a separate table joined in, never required for the core contract
  to be useful — can ship independently, later, or not at all without blocking anything upstream.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| T3's exact code change is unknown until live diagnosis | Could reveal a bigger fix than expected (server-side `ProjectRecipes`) | Spec deliberately left this open; diagnose live before locking a change, per design-gate evidence rule |
| T8's enrichment data file requires re-extracting from the third-party Scratch tool | Repeat of this session's inefficient probing if not careful (user feedback: too many small speculative queries) | One broad `evaluate_script` pull of the full `Plant-id-*`/`Zombie-id-*` lists, not incremental field-by-field queries |
| `almanac-seed`'s real-world coverage stays low (~10% plants, ~8% zombies) until `almanac-spawn-coverage` ships | Table is correct but sparse in practice | Out of scope for this plan — flagged, not solved; spawn-coverage is a separate future plan once its 3 open questions are answered |
| T1's fix permanently forecloses future window-text enrichment for any type the sweep has touched (server "first write wins," see capture-fix spec) | Running the sweep broadly is a one-way decision | Documented in the spec's Boundaries; not this task's bug to fix, but flag to owner before running the sweep broadly in Checkpoint 1 |

## Verification checkpoints

1. After T1-T3 (Module 1): injector builds clean; guards pass; live — sweep produces zero
   cross-contaminated `uiXxx` entries, `GET /api/recipes` non-empty from inside a level.
2. After T4-T7 (Module 2 core): `FusionRpg.Data.Tests` + `FusionRpg.E2E.Tests` green; `guard-dal.ps1`
   green; manual `POST /api/almanac/seed/rebuild` then spot-check `GET /api/almanac/seed/plant/0`
   against the live samples recorded in the spec.
3. After T8 (enrichment): full suites green; owner reviews the enrichment export file content before
   it's committed (fan-tool data, hand-reviewed per the spec's own boundary).
4. End to end: `dotnet test tests\FusionRpg.Core.Tests tests\FusionRpg.Data.Tests
   tests\FusionRpg.Guard.Tests`; `.\scripts\guard-dal.ps1`; `.\scripts\deploy-play.ps1 -NoServer`;
   browse a handful of `GET /api/almanac/seed/{side}/{typeId}` results against the raw in-game
   almanac card.
