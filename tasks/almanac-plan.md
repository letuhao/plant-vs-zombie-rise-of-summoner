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

1. **DONE 2026-08-23.** After T1-T3 (Module 1): injector builds clean; guards pass; live — sweep
   produces zero cross-contaminated `uiXxx` entries (900/900 sweep entries capped ≤6 fields, no
   window text ever computed — proven live against the owner's own open almanac window), `GET
   /api/recipes` non-empty from inside a level (5000+ real fusion entries; the auto-latch fix alone
   resolved the original empty-recipes bug, confirmed live).
2. **DONE 2026-08-23.** After T4-T7 (Module 2 core): `FusionRpg.Data.Tests` (459/459) +
   `FusionRpg.E2E.Tests` (193/193) green; `guard-dal.ps1` green; `POST /api/almanac/seed/rebuild` →
   `GET /api/almanac/seed/plant/0` verified via E2E test against the live samples recorded in the
   spec (Peashooter, cost 100, cooldown 7.5s) — the live *production* server spot-check was
   deliberately deferred (see Checkpoint 2 note in the todo file: redeploying would drop the owner's
   actively-connected game session; the E2E suite runs the identical `Program.cs` pipeline).
3. **DONE 2026-08-23**, with one owner-only item left open on purpose. After T8 (enrichment): full
   suites green. Real data extracted from the fan tool this session (617 plants + 164 zombies, one
   broad `evaluate_script` pull per the risk mitigation below — no fabricated content). **Owner still
   needs to review the enrichment export file content before treating it as committed-quality data**
   (fan-tool data, hand-review is explicitly the spec's own boundary, not something an agent can
   self-certify) — file is at
   `data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json`.
4. **DONE 2026-08-23** (production-server spot-check deferred, see #2): `dotnet test
   tests\FusionRpg.Core.Tests tests\FusionRpg.Data.Tests tests\FusionRpg.Guard.Tests` — 2882/2882 +
   459/459 + 70/70 green; `.\scripts\guard-dal.ps1`,
   `.\scripts\guard-single-writer.ps1`,`.\scripts\guard-secondary-no-unity.ps1`,
   `.\scripts\guard-funnel-delta.ps1` — all green; injector deployed live via `deploy-play.ps1
   -NoServer` for T1-T3; browsing `GET /api/almanac/seed/{side}/{typeId}` against the raw in-game
   almanac card on the live production server is the one remaining deferred step (owner's terminal
   only, per CLAUDE.md's server-lifetime rule, and to avoid dropping the owner's active session).
