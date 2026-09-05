# Spec: delve-graph-roll

Status: **APPROVED by the owner 2026-09-05 (wave 1)** — written against shipped code and the approved capability map; unbuilt. Ships
under gate G1: `WorldValidation` accepts a rolled graph under the delve profile, rejects it under the map
profile, every world golden byte-identical ([party-dungeon-map.md](../party-dungeon-map.md) §Gates).

Module id `delve-graph-roll`, row 4 of the [party-dungeon map](../party-dungeon-map.md) (wave 1, last of the
wave). Depends on `dungeon-seed-contract` (the domain, layout and room-archetype anchors it reads),
`delve-scope` (the world-store shape that persists what it emits) and `dungeon-registries` (room/door kind
registries, `data/tuning/dungeon.v1.json` and its T5-rejecting loader). Nothing here calls a model, a clock,
a store or the battle engine.

## Objective

One pure function, `Roll(domain, layout, seed, raidMode, tuning) → DelveGraph`, that turns a domain anchor
and a layout template into the room graph of one delve: `WorldSector` rows for rooms and `WorldLane` rows
for doors, plus the per-room facts a sector has no field for, in canonical order, already validated. The
same validators run over rows loaded back from the store, so a persisted graph can never be one the roller
would have refused.

## Locked anchors

- **Fairness rules (ideal §4.2):** *"row 1 is all ordinary fights; one fixed `cache` row in the middle; the
  row before the boss is all `rest`; no elite or rest before a tunable row; elite/merchant/rest never
  consecutive; siblings differ; at least two distinct routes; nothing unreachable; the boss connects to
  every last-row node."* §11.1 adds: *"the key must lie strictly above the gate on a walk that does not
  pass through it, validated by BFS on the ungated graph then per key"*; one-way deeper only; secrets at a
  dead end or a rest, never the boss row, never adjacent, `cache`/`shrine`/`merchant` only; reachability
  *"is a validator that throws"*; *"raid mode changes the walk count, never the row count."*
- **Decision 5 (ideal §11.9 box #5; `decisions.md:114`):** *"Rooms are `WorldSector`s, doors are
  `WorldLane`s, persisted by the world store."* S2-16: *"decision 5 wins — the roll validates the persisted
  rows."* The store shape is `delve-scope`'s; this module emits rows that shape accepts.
- **R10 / R11:** the door *"issues the same Sanctum delve request with a `domainId`"* — the roller takes a
  domain, never a legion; unknown-room pity is *"per party"* — `event-deck`'s state, bound to the routes marked here.
- **N4 / R8:** *"`depth.bossBand` becomes `depth.bossBandDelta` on the last corridor's band."*
- **N7 / S2-2:** *"not offered (a rule, not a clamp)"* — refuse-not-clamp, applied to every structural
  input: a layout the fixed rows do not fit, a width that cannot seat two starts, a dead-end count with
  nowhere to hang — the roller throws; it never shrinks, pads or floors.
- **Raid-wide extraction (S2-16):** *"a party may hold at a rest, never bank"* — graph-level facts: any room
  is an extraction node; a `rest` room is a hold point; nothing in the graph banks.
- **Determinism (`spec-turn-engine.md:76`):** *"Integer or fixed-point only … stable ordering by entity id
  everywhere, never dictionary enumeration · seeded per-system RNG streams … using the existing
  `SeededRng`, never `System.Random` · no wall-clock read anywhere."* `spec-world-model.md:68` likewise.

## Design

### 1. Inputs and the `DelveGraph` output

Inputs, all read models owned elsewhere: the **domain anchor** (`domainId`, `climate`, `dangerBand`
ordinal, `layoutTemplateId`, `roomPalette`, `bossSpeciesRef` — ideal §11.1 A); the **layout template**
(`layoutId`, `sizeBand`, `widthBand`, `branchiness`, `gateDensity`/`secretDensity`/`oneWayDensity` with
`none` legal, `raidModes` — `spec-dungeon-seed-contract.md:74`; the ideal's `fixedRows` is gone per S2-12,
the fixed rows are validator rules here); the **sealed `ulong` seed**; a **`RaidMode`**; the
**`DungeonTuning`** record and the room-kind rows (`climateNeutral`, `secretEligible`, `bossRowAllowed`,
`neverAdjacentTo[]`, joined weights — `spec-dungeon-registries.md:72, :306`) `dungeon-registries` loads.
Every band ordinal resolves to an integer through tuning before the first draw.

Output, `DelveGraph`:

| Member | Type | What it carries |
|---|---|---|
| `Rooms` | `IReadOnlyList<WorldSector>` | one per room, ordinal by `SectorId` |
| `Doors` | `IReadOnlyList<WorldLane>` | one per door, ordinal by `LaneId` |
| `Facts` | `IReadOnlyList<DelveRoomFact>` | `(row, col, kind, archetypeId, baseBand, isSecret, sightLanes, scoutSightLanes, partyRouteMask, keyForLaneId)` — the in-memory read model |
| `Walks` | `IReadOnlyList<DelveWalk>` | `(walkIndex, partyIndex?, sectorIds[])` — the party routes and the extra walks |

`WorldSector` fields set (`WorldState.cs:129-177`): `SectorId` = `r{row:00}c{col:00}`; `TypeId` = the room
kind id (a `RoomTypeCatalog` row, `SectorTypeDef` shape — `delve-scope`); `Climate` = the domain's (`:135`);
`DangerBand` = the base band (`:137`); `LayoutX` = col, `LayoutY` = row (`:173-174`). Everything else stays
default and is never read on a delve world — `Phase`, `OwnerFactionId`, the living-world per-mille fields,
`LoamStock`, `FractureIntensityMilli`, `AuthoredIntel`, `WardenBindingId`, and `Slots` (empty:
`WorldSlot.GuardWaveId`, `:107-111`, is *not* used to pre-commit an encounter — that is
`encounter-generator`'s roll at entry from the archetype's `encounterRef`, and a slot id written at graph
time would be a second owner of that fact).

Which `WorldLane` fields are set (`:213-230`): `LaneId` = `{fromSectorId}-{toSectorId}`; `FromSectorId`
(shallower room); `ToSectorId` (deeper room); `TypeId` = a `DoorTypeCatalog` row id from
`door-kinds.v1.json` — `passage`, `one-way` (`OneWay`, `LaneTypeDef.cs:22`), `gated` (`Gated`, `:25`),
`secret` (`spec-dungeon-registries.md:73`); `GateKeyId` = `key.{laneId}` on a gated door, null otherwise
(`WorldState.cs:247`). `Length`, `Width`, `HazardMilli`, `WardLevel` stay default; `State` stays `Open`.

`rpg_delve_rooms` (`spec-delve-scope.md:81-89`) already persists `row_index`, `col_index`, `kind`,
`archetype_id`. Of the remaining facts, `isSecret` (a `secret` door), the sight radii (archetype
`sightBand` + tuning), `baseBand` (`DangerBand`) and the route mask (the header's party routes) are
derivable and stay unstored (`spec-world-model.md:108`). **One column is filed on `delve-scope`:**
`key_for_lane_id TEXT NULL` — a key assignment is a roll outcome nothing else can rebuild.

### 2. The algorithm, step by step, with the stream each step draws

Every stream is `SeededRng.DeriveStream(seed, name)` (`SeededRng.cs:26-27`), the shape of the expedition
tick roll (`ExpeditionResolver.cs:106-107` — one stream per tick, so an extra draw in one never shifts
another). Weighted picks go through `WeightedChoice.Pick(options, rollSeed, streamName)` (`WeightedChoice.cs:25`)
with `rollSeed` = the stream's first `NextULong()`; its `action.seed.` prefix (`:39`) is a name, not a second seed.

1. **Dimensions** — `dungeon:layout`. Corridor rows `N ∈ [rows.min, rows.max]` (0-based `r = 0..N−1`, matching
   `spec-difficulty-ladder.md:56`) and columns `C ∈ [cols.min, cols.max]` via `NextInt` (`SeededRng.cs:55-59`);
   the boss is row `N`, one node at col 0. Walks `K = bands.branchiness.*.pathWalks + raid.modes.{mode}.walksDelta`
   (`spec-dungeon-registries.md:119`); parties `P = raid.modes.{mode}.parties`. Refuse if `N < 4`, `C < 2`,
   `P > K`, or `N`/`C` exceed the id buffer bound (§Tunables).
2. **Walks** — `dungeon:walk:{k}` for `k = 0..K−1`. Each walk starts at a column in row 0 and steps to
   `col−1 | col | col+1` per row down to row `N−1`, then to the boss. Walks `0..P−1` are the **party
   routes** and start on distinct columns; across all `K` walks at least two distinct starts. A step that
   would cross an existing lane (`(c→c+1)` against `(c+1→c)`) is not offered; no step offered → throw.
3. **Dead ends** — still `dungeon:layout`. `graph.minDeadEnds` spur rooms hang one row below a walk node in
   rows `0..N−3`, one lane in and none out (never in row `N−1` or `N`). Fewer hang points than spurs → throw.
4. **Fixed rows** — no draw. Row 0 every node `fight`; the cache row (`graph.fixedRows.midCacheRowMilli · N /
   1000`, integer) every node `cache`; row `N−1` every node `rest`; row `N` the single `boss`.
5. **Kinds** — `dungeon:kind:{r}:{c}` per unassigned node, in (row, col) order. Options = the weight table
   minus: kinds outside `[earliestRowMilli, latestRowMilli] · N / 1000`; any kind the parent's row
   `neverAdjacentTo[]` names (the registry carries the "never consecutive" set — elite/merchant/rest
   self-adjacency as the starting shape); any kind a sibling (a node sharing a parent) already took; kinds
   with `bossRowAllowed` off the boss row. An empty option list throws `NoDrawableWeightedOptionException`
   (`WeightedChoice.cs:36-37`).
6. **Archetypes** — `dungeon:room:{r}:{c}`. A weighted pick over the domain's `roomPalette` filtered to
   `(kind, climate)`; climate-neutral kinds (`rest`, `merchant`, `boss`, `unknown`) ignore climate. An empty
   palette cell throws — a domain missing a `(kind, climate)` cell is a corpus defect the roll refuses.
7. **Gates** — `dungeon:gate:{r}:{c}` per candidate lane (from rows `1..N−3`, not into the rest row or the
   boss). `NextPerMille() < gateDensity.perRoomMilli` gates it; the key room is the shallowest `cache` or
   `elite` on a *different* walk with row `< from-row`, reachable on the ungated graph without the gated
   lane; none → the gate is not placed (a placement rule, not a fallback). The lane is emitted **shut**
   (`GateKeyId` non-null is what `MarchResolver.cs:60` refuses on); nothing in this module opens a gate.
8. **One-way** — `dungeon:oneway:{r}:{c}` per lane not gated, not into the rest row, not on a spur, whose
   `to` node keeps a two-way inbound lane. Always deeper.
9. **Secrets** — `dungeon:secret:{r}:{c}`. One `graph.secretAppearMilli` draw on `dungeon:secret:0:0` decides
   whether this graph has any; then per attach point (a spur or a `rest`, not adjacent to a placed secret)
   `NextPerMille() < secretDensity.perRoomMilli`. Kind by weighted pick over the `secretEligible` kinds;
   door type `secret`; the secret room sits on the attach row at `col ≥ C` and is a leaf.
10. **Bands and sight** — no draw (§6, §7). Then **canonical order and validation** (§3, §8).

Not drawn here: `dungeon:event|loot|unknown:{r}:{c}`, `dungeon:supply:{r}:{c}:{n}`, `dungeon:supply:entry:{n}`,
`dungeon:merchant:{r}:{c}`, `dungeon:wild:{r}:{c}:{seq|traits|cage}`, `dungeon:altar:{r}:{c}:{n}` — derived from the same
seed with those names by `event-deck`, `dungeon-loot`, `supplies-and-objects` and `wild-room` at *entry*. The roller reserves the names and never touches them, so an
entry-time draw cannot move a graph and a graph change cannot move an entry.

### 3. Fairness and structural rules — the validator list

`DelveGraphValidation.Validate(graph, tuning)` runs after `Roll` and again over rows loaded from the store.
Each rule throws `InvalidOperationException` naming the rule and the id, as `Rule3Connected` does
(`WorldValidation.cs:160-162`); none returns a flag.

1. **Stable order, well-formed ids** — `Rooms` ordinal by `SectorId`, `Doors` by `LaneId`
   (`WorldValidation.cs:45-52`); ids `r{00}c{00}` and `{from}-{to}`; every endpoint exists.
3. **Layered** — every non-`secret` door goes from row `r` to row `r+1`; a `secret` door joins a room to a
   secret room on the same row at `col ≥ C`; nothing else sideways, nothing upward.
4. **Reachable** — every room reachable from a row-0 room on the *ungated, all-two-way* view (the BFS at
   `WorldValidation.cs:137-163`, reused with the delve's own root set instead of `Sectors[0]`).
5. **Walks complete** — every walk ends at the boss; every party route is a walk; party routes start on
   distinct columns; at least two distinct starts across all walks.
6. **No crossings** — no pair of doors `(r,c)→(r+1,c+1)` and `(r,c+1)→(r+1,c)`.
7. **Boss** — exactly one `boss` (the only `bossRowAllowed` kind), on row `N`, with an inbound door from
   every row-(N−1) node and none out.
8. **Fixed rows** — row 0 all `fight`; the cache row all `cache`; row `N−1` all `rest`.
9. **Row bans** — every kind inside its `[earliest, latest]` window.
10. **Never adjacent** — no door whose `to` kind is in the `from` kind's `neverAdjacentTo[]`.
11. **Siblings differ** — children of one node are pairwise distinct in kind.
12. **Key above gate** — for every `GateKeyId`, exactly one room with `keyForLaneId` = that lane, of kind
    `cache` or `elite`, row strictly less than the gate's from-row, reachable from a row-0 room on the
    ungated graph with the gated lane removed, and not on a walk that passes through the gated lane.
13. **One-way deeper only** — every `one-way` door has `from.row < to.row`, and its `to` room keeps a two-way
    inbound door.
14. **Secrets** — kind `secretEligible`; attached to a spur or a `rest`; never row `N`; no two secrets
    adjacent; a secret is a leaf.
15. **Dead ends** — at least `graph.minDeadEnds` rooms with no outbound door other than the boss and secrets.
16. **Bands monotone** — `DangerBand` non-decreasing with row; boss band = row-(N−1) band + `bossBandDelta`.
17. **Refuse-not-clamp** — every input bound in §Tunables is checked before the first draw and no computed
    sight radius is below zero (a `dim` room under `sight.lanes = 0` is a tuning error); nothing is clamped.

### 4. Gates, keys, secrets, one-way

A gate is a `gated` lane with `GateKeyId` set — the shape `MarchResolver.cs:58-60` refuses and `delve-scope`'s
`LaneGate.Refusal` lifts (`spec-delve-scope.md:183-187`). The delve resolver (a later module) is the only
writer that nulls `GateKeyId`; this module writes shut gates and never opens one. The key is an item
`dungeon-loot` mints into the key room's drop (it reads `keyForLaneId`); the key room sits above the gate on
another route, so the first party at a gate has a choice, never a dead run. Secrets reuse the Gungeon
numbers as tunables (ideal §11.1 C) and the Isaac placement rule (dead ends are a resource); `one-way` doors
are `LaneTypeDef.OneWay` (`:22`) and point deeper, so a one-way never traps — retreat past one is extraction.

### 5. Raid routes

`raid.modes.{solo,pair,quad}.parties` fixes how many walks are party routes: 1, 2, 4. Each party route is
its own walk with its own first room; `partyRouteMask` is the bit set of parties whose route passes through
a room, so a shared room (common from the cache row down, universal on the rest row) is visible as shared.
The boss row is the rendezvous by construction (rule 7). Extra walks beyond `P` are route choices, not
parties; raid mode adds `walksDelta`, never a row or a column. Extraction is raid-wide and any room is an
extraction node; a `rest` room is where a party may hold while another catches up — the graph exposes both
as `Facts` (kind) and nothing more; the banking rule lives in `loot-pack`.

### 6. Sight

Per room, `sightLanes = sight.lanes + bands.sightBand.{archetype.sightBand}.extraLanes` and
`scoutSightLanes = sight.scoutLanes + the same extra` — tuning, never `Visibility.SightLanes` /
`ScoutSightLanes` (`Visibility.cs:33, :36`). The roller exposes the radii as facts; `delve-scope`'s
`DelveSight.ForParty(world, partyEntityId, tuning)` (`spec-delve-scope.md:198-202`) turns them into
`SectorSight.Glimpse` / `Full` (`Visibility.cs:6-16`). **Filed on `delve-scope`:** `ForParty` reads the
room's archetype `sightBand` extra, not the bare `sight.lanes` its §6 names — otherwise a `dim` room is lit.

### 7. Depth seam to `difficulty-ladder`

This module writes the **row index** (`LayoutY`) and the **base band**:
`baseBand(row) = entranceBand + row / depth.rowsPerBandStep` (integer division; `rowsPerBandStep: 2`, S2-2),
`entranceBand = bands.dangerBand.{domain.dangerBand}`, `baseBand(boss) = baseBand(N−1) + depth.bossBandDelta`
(N4; `spec-difficulty-ladder.md:55-60` is the same formula). `difficulty-ladder` builds
`ContentContext(DangerBand = baseBand + rung bandDelta [+ once] [+ tail·n])` and `PowerIndexComposer` yields
`Θ_room`; a rung whose sum would clamp on this domain is not offered there (N7). No `Θ`, `P(Θ)` or `Wm` here.

### 8. Determinism

Pure: same `(domain, layout, seed, raidMode, tuning)` ⇒ byte-identical `Rooms`, `Doors`, `Facts`, `Walks`.
Every collection is emitted in canonical (row, col) order, which the zero-padded ids make equal to ordinal
id order — `RequireStableOrder` holds without a sort at load. No dictionary enumeration reaches an output.
No `DateTime`, `Environment.TickCount`, `System.Random`, store or I/O. The per-run seed is minted at commit
as an expedition's is (`BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0)`, `ExpeditionEndpoints.cs:34`),
stored as text (`RpgStore.Expeditions.cs:91`; `rpg_worlds.seed TEXT`, `RpgStore.World.cs:24`) and never
leaves the server (`ExpeditionEndpoints.cs:309-310`). "Random each run" is a fresh seed on a `many` domain's
re-roll; the roller has no notion of "run".

## Tunables

All in `data/tuning/dungeon.v1.json`, loaded by `dungeon-registries`; units in the key; one owner each (N13).

| Key | Unit | Starting shape |
|---|---|---|
| `bands.dangerBand.{shallow,mid,deep,abyssal}` | band (int) | 2 / 4 / 6 / 8 — the planner keeps `many` domains ≥ 2 (§1(g) (4)) |
| `bands.depth.{short,medium,long}.rows.{min,max}` | corridor rows | 4–5 / 7–8 / 11–12 (ideal §5's 4/7/11 as the minimums) |
| `bands.width.{narrow,mid,wide}.cols.{min,max}` | columns | 3–4 / 5–5 / 6–7 |
| `bands.branchiness.{linear,forked,webbed}.pathWalks` | walks | 3 / 4 / 6 |
| `raid.modes.{solo,pair,quad}.parties` · `.walksDelta` | party routes · extra walks | 1 / 2 / 4 · 0 / 0 / 2 — §11.1 E's `graph.raid.*.walks` is retired for these (registries `:119`) |
| `bands.{gate,oneWay}Density.{none,sparse,dense}.perRoomMilli` | ‰ per candidate lane | gate 0 / 80 / 200 · one-way 0 / 100 / 250 |
| `bands.secretDensity.{none,sparse,dense}.perRoomMilli` | ‰ per attach point | 0 / 200 / 350 — Gungeon's 1/5 is `sparse`. **Not read:** `graph.secretAttachAnyMilli` (registries `:127`) is the same number under a second owner; filed on `dungeon-registries` (N13) |
| `graph.secretAppearMilli` | ‰ per graph | 900 (Gungeon's 90%) |
| `graph.minDeadEnds` | rooms | 2 — Isaac's 5 belongs to a 3.33·depth+5 room count; ours is ~20–40 rooms. **Tunable**, not structural (review N15 relabel) |
| `graph.fixedRows.midCacheRowMilli` | ‰ of corridor depth | 500 |
| `nodes.{kind}.weightMilli` — all eleven | relative weight | fight 450 · curio 120 · unknown 100 · rest 100 · elite 80 · wild 60 · trap 60 · merchant 50 · shrine 40 · cache 40 · boss 0 (StS 530/220/80/120/50 spread over eleven kinds; fixed-row kinds are never drawn) |
| `nodes.{kind}.{earliestRowMilli,latestRowMilli}` | ‰ of corridor depth | elite ≥ 300, rest ≥ 300, merchant ≥ 200; rest ≤ 850 (StS 6/6/14 of 15, tier-relative) |
| `nodes.unknown.pity.*` | — | **Not read here** — `event-deck`'s (map row 9, R11); registries `:306` lists it under this module and that row is filed as a correction |
| `sight.lanes` · `sight.scoutLanes` · `bands.sightBand.{dim,lit,scouting}.extraLanes` | lanes (extra signed) | 1 · 2 · −1 / 0 / +1 |
| `depth.rowsPerBandStep` | rows per band | 2 |
| `depth.bossBandDelta` | band delta on the row-(N−1) band | +1 |

**Structural, with the exemption comment in code:** `MaxRows = 99` / `MaxCols = 99` — the two-digit id
buffer bound (changing it breaks id ordering, not feel); `MinCorridorRows = 4` — three fixed rows plus one
free row; `MinWidth = 2` — two distinct starts. `WeightedChoice`'s "weight ≤ 0 skipped" (`:31`) is the library's.

## Numeric types

`int` for rows, columns, bands, lane counts and every `NextPerMille()` roll — bounded ordinals or bounded
ratios (bands stay below 100 by the row bound; per-mille is 0..999). `ulong` for the seed (`SeededRng.cs:15`,
`WorldState.cs:303`). Per-mille *tunables* arrive as `long` from the registries loader and are compared
against the `int` roll by widening the roll — no multiply anywhere. This module emits no magnitude — no hp,
atk, value or yield; `Θ` is `difficulty-ladder`'s and `P(Θ)` is read by `Instantiator` / `LootPipeline` /
`BattleRuleset`, none called here.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve|FullyQualifiedName~World"  # roll suites + world goldens
python scripts\audit-magic-numbers.py --summary                             # Delve/Roll must add zero M1 rows
```

## Structure

```
src/FusionRpg.Core/Delve/Roll/
  DelveGraph.cs             → DelveGraph, DelveRoomFact, DelveWalk, RaidMode
  DelveGraphRoll.cs         → Roll(domain, layout, seed, raidMode, tuning)
  DelveGraphValidation.cs   → Validate(graph, tuning) — the 17 rules, each throws
  DelveStreams.cs           → stream-name and id formatters (dungeon:layout, dungeon:walk:{k}, r{00}c{00}, …)
tests/FusionRpg.Core.Tests/Delve/
  DelveGraphRollGoldenTests.cs, DelveGraphPropertyTests.cs,
  DelveGraphValidationTests.cs, DelveGraphDeterminismTests.cs (hashes: docs/research/party-dungeon/_baseline-delve-graph.json)
```

Read models live under `dungeon-seed-contract`; room/door catalogs under `delve-scope`; this folder holds only the roller.

## Code style

Mirrors `WorldTemplateCatalog.Build` (`WorldTemplateCatalog.cs:39-45`): a static pure builder, seed as a
parameter, output returned through the validator, unknown input throws.

```csharp
public static class DelveGraphRoll
{
    /// <summary>Pure: same inputs ⇒ byte-identical rows. No clock, no store, no I/O, no retry.</summary>
    public static DelveGraph Roll(
        DomainAnchor domain, LayoutTemplate layout, ulong seed, RaidMode raidMode, DungeonTuning tuning)
        => DelveGraphValidation.Validate(RollUnchecked(domain, layout, seed, raidMode, tuning), tuning);
}

static void Rule12KeyAboveGate(DelveGraph g)
{
    foreach (var door in g.Doors.Where(d => d.GateKeyId != null))
    {
        var key = g.Facts.SingleOrDefault(f => f.KeyForLaneId == door.LaneId)
            ?? throw new InvalidOperationException($"Gate '{door.LaneId}' has no key room.");
        if (key.Row >= RowOf(door.FromSectorId) || !ReachableWithout(g, key.SectorId, door.LaneId))
            throw new InvalidOperationException($"Key for '{door.LaneId}' is not strictly above it on another route.");
    }
}
```

## Testing strategy

- **Goldens:** one rolled graph per (layout tier × raid mode) — 3 × 3 = 9 — hashed over the canonical rows
  in order, blessed once, moved only with a version bump in the test file's header.
- **Property tests** over a 256-seed sweep per tier: reachability; key strictly above gate and reachable
  without it; siblings differ; fixed rows; no `neverAdjacentTo` door; party routes start on distinct
  columns; every walk ends at the boss; one-way deeper only; secrets never on the boss row and never
  adjacent; ≥ `minDeadEnds`; bands monotone with the boss delta.
- **Refuse-not-clamp:** `rows.min = 3`, `cols.min = 1`, `parties > pathWalks`, `minDeadEnds` above the hang
  points, a `dim` room under `sight.lanes = 0` — each throws before any row is emitted; none is coerced.
- **Determinism replay:** roll twice, compare bytes; roll with a different `raidMode`, assert `N`/`C` and
  the grid identical and only the extra walks and `partyRouteMask` differ.
- **Validator throws named:** one test per rule over a hand-built bad graph asserting the rule's message.
- **World goldens unchanged:** the world suite runs in the same command.

## Boundaries

- **Always:** streams named exactly as §11.1 B.2; every input bound checked before the first draw; canonical
  (row, col) order on every output; the validator on every path, rolled or loaded; the seed as a parameter.
- **Ask first:** a twelfth room kind or a fifth door type; a non-layered walk model; `rowsPerBandStep` semantics.
- **Never:** a magnitude; a container roll (`Instantiator.TryInstantiate`, `Instantiator.cs:98-107`, rolls
  atoms into an `InstanceRow` from the six-kind `ContainerKind`, `ContainerRow.cs:7-15` (`encounter-generator` adds a seventh, `enemy`, for elite affixes — a graph is still none of them) — a graph is
  none of them); a wall clock; a retry-until-valid loop (the genre backtracks, this repo throws); copying
  `Visibility.cs:33`'s `const`; opening a gate; drawing `dungeon:event|loot|unknown`.

## Success criteria

1. Nine goldens blessed; 256-seed property sweep green per tier. 2. Every validator rule has a named
throwing test. 3. A rolled graph passes `WorldValidation.Validate(world, delveProfile)` and fails the map
profile (G1, with `delve-scope`). 4. World goldens byte-identical. 5. `audit-magic-numbers.py` adds zero rows
under `Delve/Roll`. 6. No `System.Random`/`DateTime`/`Environment.TickCount` symbol under `Core/Delve/Roll/`
(guard test, the `spec-turn-engine.md:138` pattern).

## Interface exposed to dependents

- **`delve-scope` persists** through `CreateDelve` (`spec-delve-scope.md:350`): `Rooms` → the delve world's
  sector rows; `Doors` → its lanes; `Facts` → `rpg_delve_rooms` (`row_index`, `col_index`, `kind`,
  `archetype_id` exist; `key_for_lane_id` filed); `Walks` → the header's party routes; it calls
  `DelveGraphValidation.Validate` on load.
- **Consumers read `(row, col, kind, archetypeId, baseBand, partyRouteMask, gateKeyId)`:** `difficulty-ladder`
  → `row`, `baseBand` → `Θ_room`; `encounter-generator` → `kind`, `archetypeId` (`encounterRef`) at entry;
  `event-deck` → `kind`, `archetypeId` (`eventPool`), route mask for per-party pity; `dungeon-loot` →
  `keyForLaneId` (mints the key), `kind` (table binding); `delve-stage` → all of it plus the sight radii.
- **Graph-level facts:** any room extracts; a `rest` holds; a gate is shut until `GateKeyId` is nulled; one-way is deeper only.

## Design-gate checklist

```
[x] Subsystems: world model (sector/lane rows), battle RNG, party dungeon.
[x] Read this session: map; ideal §4.2/§4.8/§11.1/§11.9/§11.10; audit §1(a)/§1(g)/S2-2/S2-16;
    spec-world-model; spec-turn-engine; spec-expeditions.  decisions.md rows :113-116 checked.
[x] Every claim cites file:line, verified against CODE (SeededRng, ExpeditionResolver, WorldTemplateCatalog,
    WorldValidation, LaneTypeCatalog, WorldState, Visibility, MarchResolver, WeightedChoice, AtomRandom,
    Instantiator, ContainerRow, ExpeditionEndpoints, RpgStore.World/Expeditions); sections read around quotes.
[ ] Constraints tested — nothing was run: the spec is unbuilt. "World goldens unchanged" is a G1 criterion.
[x] No §2 invariant contradicted — no magnitude, no private f(level), no cap, tunables in data.
[x] Reconciled with the sibling wave-1 specs read this session (delve-scope, dungeon-registries,
    difficulty-ladder, dungeon-seed-contract): door ids, registry-owned bans, walksDelta, 0-based rows,
    no fixedRows. Filed: `key_for_lane_id` + DelveSight sightBand read (delve-scope); `secretAttachAnyMilli`
    duplicate + `nodes.unknown.pity` consumer row (dungeon-registries).
```
