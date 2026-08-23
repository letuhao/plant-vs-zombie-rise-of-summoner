# Capability map: Almanac (BE seed data)

Turns the game's own almanac + live capture into a trustworthy, reusable, BE-only content source —
the generator the Demon program's species catalog already commits to using
([decisions.md:90](decisions.md) — *"species catalog generated deterministically from captured game
data (types/almanac/icons/spawn_stats), output checked in"*). No FE in this round; the owner has a
separate FE refactor task planned.

Read first: [data-architecture.md](data-architecture.md) §3 SSOT map (combat HP/ATK/armor SSOT is
`spawn_stats.stats_json`, **not** `types.hp_base`), §6 DAL boundary (SQL only in `FusionRpg.Data`).

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `almanac-capture-fix` | Fix the injector sweep's window-TMP contamination bug — swept entries currently inherit whatever `AlmanacPlantWindow`/`AlmanacZombieWindow` text was last on screen | — |
| `almanac-recipes-fix` | Diagnose and fix `PlantMixTreeManager`/`EnqueueRecipes` producing zero entries; verify live in a level (currently only tested from the almanac/menu screen) | — |
| `almanac-spawn-coverage` | Automated in-level sweep: spawn every `PlantType`/`ZombieType` once so `spawn_stats` gets a baseline hp/attack/armor sample for (near-)every type, not just the ~10% seen from manual play | `almanac-capture-fix` (sequencing only) |
| `almanac-seed` | New structured DAL tables (typed columns) + a rebuild step that normalizes `type_almanac_dump` (regex-parsed cost/cooldown) + `spawn_stats` (SSOT combat numbers) into one row per type; REST reads directly from the clean tables. Versioned contract. Recipes excluded — stays `/api/recipes`. No FE. | `almanac-capture-fix`, `almanac-spawn-coverage` |

Build order: `almanac-capture-fix`, `almanac-recipes-fix` → `almanac-spawn-coverage` →
`almanac-seed`. **Corrected after adversarial review 2026-08-23:** `almanac-capture-fix` and
`almanac-recipes-fix` both edit `src/FusionRpg.Injector/GameHooks.cs` (`EnqueueFullAlmanacText` and
`EnqueueRecipes` respectively, close together in the file) — logically independent, but not
literally parallel-safe in the same working tree without a merge; sequence them, don't run them as
simultaneous branches.

## Why this shape — and what's a hard dependency vs. a soft one

**`almanac-seed` can be built and its own automated tests (`FusionRpg.Data.Tests`) run**
independently of the other three — its test fixtures don't need real captured data. The edges below
are about the *data* being trustworthy/complete when the module is actually used, not about code or
test dependencies:

- **`almanac-seed` reading pre-fix `almanac-capture-fix` output** would mean building on top of raw
  dumps that may carry another type's mislabeled window text (`almanac-seed` doesn't consume the
  contaminated `uiXxx` fields itself — see [spec-almanac-seed.md](almanac/spec-almanac-seed.md) — so
  this is a data-hygiene reason, not a functional one: nothing breaks if the order is violated, but
  the raw dumps stay less trustworthy for anyone else reading them directly).
- **`almanac-seed`'s `observed` flag is only meaningful once `almanac-spawn-coverage` has run** —
  before that, `spawn_stats` covers ~10% of plants and ~8% of zombies, so "not observed" is the
  overwhelming default rather than the edge case the flag is meant to represent. This is a
  data-quality dependency: the rebuild works and tests pass either way, but the *output* is only
  useful for downstream consumers (e.g. the Demon species catalog) after coverage exists.
- **`almanac-recipes-fix` is independent** of everything else here. Recipes never enter the seed
  contract (owner call: keep `/api/recipes` a separate lookup), so nothing downstream blocks on it.
  It rides along because it surfaced in the same investigation and is cheap to close.

## Module specs

- [almanac/spec-almanac-capture-fix.md](almanac/spec-almanac-capture-fix.md)
- [almanac/spec-almanac-recipes-fix.md](almanac/spec-almanac-recipes-fix.md)
- [almanac/spec-almanac-spawn-coverage.md](almanac/spec-almanac-spawn-coverage.md)
- [almanac/spec-almanac-seed.md](almanac/spec-almanac-seed.md)

Plan / tasks (once specs are approved): `tasks/almanac-plan.md`, `tasks/almanac-todo.md` — prefixed
because `tasks/plan.md`/`tasks/todo.md` currently belong to the perf-v3 stream and `SPEC.md` to
vfx-v3 (checked this session, per AGENTS.md's parallel-programs convention).
