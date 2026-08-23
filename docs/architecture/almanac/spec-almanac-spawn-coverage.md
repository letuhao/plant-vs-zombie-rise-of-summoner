# Spec: almanac-spawn-coverage

Module in the [almanac map](../almanac-map.md). Depends on `almanac-capture-fix` (sequencing only —
shares no code).

## Objective

**Corrected after adversarial review 2026-08-23** — the original wording here said "`spawn_stats`/
`types` combat columns are the SSOT," which is wrong and contradicts both `data-architecture.md §3`
and this module's own sibling spec. Precisely: `events.payload` + `spawn_stats.stats_json` are the
SSOT; `types` combat columns (`hp_base`/`attack_base`/etc.) are **explicitly listed as NOT SSOT** —
"RPG features read dumps..., never `types.hp_base`. Missing hook = missing fact." This module exists
*because* `spawn_stats` (the real SSOT) is populated only by actually spawning a type during live
play, and `types.hp_base` (a convenience first-seen mirror) must never be used as a coverage proxy
for it.

Measured 2026-08-23: **66 of 677 plants (10%)** and **18 of 227 zombies (8%)** have a `spawn_stats`
sample. `almanac-seed` ([spec](spec-almanac-seed.md)) needs this number close to 100% for its
`observed` flag to mean anything — right now "not observed" would be the overwhelming default, not
an edge case.

Done means: one triggered action spawns every `PlantType`/`ZombieType` at least once on an active
board, so the existing live-capture hooks (`Plant.Start`/`Zombie.Start` →
[game-types-381.md](../../research/game-types-381.md) Capture A) do their normal job and
`spawn_stats` gains a baseline row for every type that *can* be spawned by cheat (some may still
fail — bosses, scripted-only types — and that's a legitimate "still unobserved," not a bug).

## Design — draft, needs owner decision before implementation

Reuses existing pieces, no new capture code:

- Enumeration: `GameHooks.EnumerateEnum(typeof(PlantType))` /`(typeof(ZombieType))` — already
  IL2CPP-safe (this is what `almanac-capture-fix`'s sweep and the existing type catalog dump both
  use).
- Spawning: `CheatActions.SpawnPlant(int)` / `SpawnZombie(int, bool)`
  ([CheatActions.cs:310-364](../../../src/FusionRpg.Injector/CheatActions.cs)) — already the exact
  mechanism the Cheats page uses for one-off spawns.
- Cleanup between batches: `CheatActions.DeleteAllPlants()` / `DeleteAllZombies()`
  ([CheatActions.cs:518,534](../../../src/FusionRpg.Injector/CheatActions.cs)) — already exist.

Sketch: chunk types into board-sized batches (one per lawn cell), spawn a batch, wait a tick or two
for `Plant.Start`/`Zombie.Start` to fire and the stat capture to land, clear the board, advance to
the next batch. Paced across frames like `GameHooks.PumpMainThread`/`CheatActions.TickContinuous`
already do for other multi-step cheat sequences — not a tight loop in one call.

**Three things this spec does not lock, because they're the owner's call, not an implementation
detail:**

1. **Board requirement and blast radius.** `SetPlant`/`SetZombie` need `Board.Instance` — this only
   runs inside an active level. Running it on the player's **real, in-progress level** means
   deleting their planted lawn and spawning up to 227 zombie types (including armored/boss-tier
   ones) into it — a real chance of instantly losing that level. Options: (a) accept that risk,
   document it as destructive and require an explicit confirm; (b) require a dedicated
   sandbox/survival-endless level with no lose condition; (c) gate behind a new safety check
   (god-mode/no-lose forced on for the duration). Undecided.
2. **Zombie spawn side-effects.** Some zombie types may trigger board-wide effects on spawn (waves,
   screen shake, huge-wave events) that are fine once but not 227 times back to back. Needs a live
   check of whether `SetZombie` has any such side effect before this is safe to batch — not
   assumed here.
3. **Pacing budget.** How many ticks/seconds per batch, and how large a batch (bounded by the lawn's
   actual placeable-cell count, not a guessed constant) — needs a quick live measurement of how long
   `Plant.Start`'s hook chain takes to land in `spawn_stats` before picking a number.

## Commands

Same as [almanac-capture-fix](spec-almanac-capture-fix.md) — Injector-only, live verification.

## Structure (once the open questions above are resolved)

```
src/FusionRpg.Injector/CheatActions.cs         (SpawnAllForCoverage() or similar orchestrator)
src/FusionRpg.Injector/CheatCommandRunner.cs   (new command case, e.g. "almanac-spawn-coverage")
web/fusion-rpg-web/... (Cheats page button)     — out of scope this round per owner's FE-later call
```

## Testing strategy

Live-only. **Must measure against `spawn_stats`, not `types.hpBase`** — `GET /api/types` surfaces
`hp_base` as a `COALESCE`-once convenience mirror that is written once and never trimmed
([RpgStore.cs:2701](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs)), while `almanac-seed`'s
`observed` flag reads `spawn_stats` directly, which **is** subject to the hot→cold retention policy
below. Measuring the wrong table would let this module report success while the module that depends
on it still sees near-zero coverage.

1. Query `spawn_stats` directly (not `/api/types`) for a distinct-`(side,type)` count before the
   sweep — e.g. `SELECT side, COUNT(DISTINCT type) FROM spawn_stats GROUP BY side` against the
   server's hot SQLite, or a debug endpoint if one exists for this.
2. Trigger the sweep inside a level.
3. Re-query the same way — assert the distinct-type count rose toward the full catalog size
   (677/227), not toward 100% blindly — some types are legitimately unspawnable by cheat and should
   stay unobserved rather than the test asserting a number that will flake.
4. Manual confirmation the player's level state is handled per whatever the owner decides in open
   question 1 (either survives, or the destructive behavior was explicitly consented to).

**Known limitation, not solved by this module:** `spawn_stats` is a retention-trimmed capture
buffer, not a durable corpus — `PromoteClosedRunCapture` moves it to `archive/*.sqlite` and deletes
the hot rows once a run closes and `KeepLastNFullCaptureRuns = 50` is exceeded
([data-architecture.md §5](../data-architecture.md)), and cold-path query fan-in is a deliberate
`IsImplemented => false` stub. This sweep's whole point is to *populate* `spawn_stats` for
`almanac-seed`'s next rebuild — but if too much time or too many runs pass between the sweep and the
rebuild, the samples this module worked to create can silently age out of hot storage before
`almanac-seed` reads them. Out of scope to fix the archival/fan-in gap here; in scope to flag it so
the two modules aren't run far apart in practice.

## Boundaries

- **Always:** pace spawns across frames/ticks, never a single-frame loop over 900 entries; measure
  coverage against `spawn_stats`, never `types.hpBase`.
- **Ask first:** everything under "Design — draft" above (board/blast-radius policy, zombie
  side-effects, pacing budget) — these are owner decisions, not implementation details, and this
  spec does not proceed to a plan until they're answered.
- **Never:** run this against the player's real progress without the blast-radius question being
  explicitly resolved first; report coverage from `types.hpBase` as if it proved `spawn_stats`
  coverage.

## Open questions

1. Is spawning into the player's active level acceptable (destructive), or does this need a
   dedicated sandbox level?
2. Do any zombie types have spawn-time side effects unsafe to trigger ~227 times in a row?
3. What batch size / pacing keeps this from lagging or racing the capture hooks?

This module stays at spec stage — pending answers — before a plan/tasks file is written for it.

## Success criteria

1. `spawn_stats` distinct-`(side,type)` coverage rises from 66/677 plants + 18/227 zombies toward
   the full catalog, measured directly against `spawn_stats` (never `types.hpBase`).
2. The sweep runs inside a level, paced across ticks, without a single-frame loop over ~900 entries.
3. Whatever the owner decides on the three open questions is implemented as stated — this module
   does not ship with an unresolved destructive-behavior question.
4. `almanac-seed`'s next rebuild, run reasonably soon after this sweep, shows `stats_observed = true`
   for the types this sweep covered (the actual end-to-end proof that coverage landed where the
   dependent module reads it).
