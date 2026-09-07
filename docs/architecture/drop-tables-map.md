# Drop-tables — capability map

**Source:** `docs/architecture/drop-tables-ideal.md` (idea phase, decided 2026-09-07). **Status:** module
specs in progress. This map indexes new, cross-program work only — it does not re-list or duplicate
work already specced elsewhere.

## Not re-specced here

- **Party-dungeon's own rich per-room-kind generator** (`ideal.md` §4 R2) already has an approved,
  unbuilt spec: `docs/architecture/party-dungeon/spec-dungeon-loot.md` (approved 2026-09-05). It needs
  building against its own existing spec, not a new one. Referenced here for visibility only.
- **`affix_channel` composition weighting** (`ideal.md` §3 W1) belongs to `effect-pipeline` module 12
  (X4), a different program. Modules below may name it as an external dependency; none of them own it.

## Modules

| # | Module id | Program | Objective | Depends on |
|---|---|---|---|---|
| 1 | `rate-floor` | item | A tunable, per-entry minimum drop-weight floor fine enough to express 0.0001% (owner decision D2: no rarity-ladder change) — widens the drop-table weight-unit convention from "per 100,000" to a unit that can name it | Nothing new — reads/extends `DropTableModel.cs`, `item-rarity.v1.json`, `bands.v1.json` |
| 2 | `sector-loot-wiring` | world-map | Wire the already-built, already-tested `WorldSectorLootSource.TryResolve` to a real sector-clear call site (closes W3 — zero production callers today), then split its one shared `drop.world.sector-clear` table into per-sector-type tables | `rate-floor` (soft — sector tables should be able to use the floor once authored, not a hard build blocker) |
| 3 | `siege-loot` | base-defense | Build base-defense's first `loot_source` binding from scratch — today `DistrictAssaultResolver` grants no items or souls through any pipeline (closes R1a, a real gap, not a wiring one) | `rate-floor` (soft, same reason as module 2); consumes the shipped `LootPipeline`/`Instantiator`/`DropTableModel` as-is, no changes needed there |
| 4 | `rate-authoring` | item | **Added 2026-09-07, owner decision (audit follow-up):** the authoring-side counterpart `rate-floor` doesn't provide — lets a designer actually PLACE an entry at a chosen rate (e.g. exactly 0.0001%) rather than merely being stopped from going rarer. Built as a standalone, reusable mechanism specifically so quests/events (and any future gameplay mode, per the owner's own framing) can adopt it without re-deriving anything | `rate-floor` (hard — shares the same tunable/unit convention) |

**Build order: 1 → 2 → 3, revised in review — "cheapest to hardest" no longer describes 2 vs 3
accurately.** `rate-floor` stays first (no dependency, no unknowns, it is the one piece every other
module's future ultra-rare content would reference). The original reasoning for 2 before 3 was
"`sector-loot-wiring` is the cheaper fix — an existing function just needs one real caller." A design-
gate review during spec-writing found that framing was wrong: locating the real call site required
tracing `ClaimResolver.cs`/`TurnEngine.cs`'s full claim-and-clear flow, not a five-minute wiring
change, and the finding **also surfaced a real, unresolved risk in module 3** — `siege-loot`'s own
win/loss signal (`SiegeOutcomeKind`) may not be where a grant belongs at all, if district clears feed
`ClaimResolver`'s slot-guard precondition the same way sector clears do (see `spec-siege-loot.md`'s own
warning). **Module 2 now goes first specifically because its call site is the one already confirmed**
(`ClaimResolver.cs:79`) — building it first turns module 3's open question ("does this decouple the
same way?") into something module 3 can check against a real, shipped precedent instead of a second
independent investigation. This is "resolve the shared unknown once, cheaply, first" reasoning, not
"easy things first."

## Dependency graph

```text
rate-floor (item)
    |
    +--> rate-authoring (item)             [hard dependency — shares the tunable/unit convention]
    |
    +--> sector-loot-wiring (world-map)    [soft dependency]
    |
    +--> siege-loot (base-defense)         [soft dependency]
```

Neither module 2 nor module 3 is blocked from starting before module 1 ships — the "depends on" is
about which module a rich, differentiated ultra-rare entry would reference, not a hard code
dependency. Module 1 has no consumer-side code changes; modules 2 and 3 could each ship a first cut
with ordinary weights and adopt `rate-floor` in a follow-up if the owner wants them built in parallel.
**Module 4 is a real, hard dependency on module 1** (unlike 2 and 3) — it reads `DropRateFloorTuning`
directly and its own id-authoring convention would otherwise drift from the floor's unit.

**Revised build order: 1 → 4 → 2 → 3.** `rate-authoring` moves ahead of the two gameplay-mode modules:
per the owner's own reasoning for wanting it ("this will help us extend drop tables for each game
mechanism easier... still have a lot of drop tables on quests, event"), any NEW table `sector-loot-
wiring`/`siege-loot` author (per-sector-type, per-district-tier) should be authored WITH the real
authoring tool from day one, not retrofitted after two gameplay modes already picked ad-hoc weights.

## Module specs

- `docs/architecture/item/spec-rate-floor.md`
- `docs/architecture/item/spec-rate-authoring.md`
- `docs/architecture/world-map-runtime/spec-sector-loot-wiring.md`
- `docs/architecture/base-defense/spec-siege-loot.md`
