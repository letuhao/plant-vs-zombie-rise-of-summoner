# `data/seed/loot/` — the runtime drop-table corpus

Owner: **item module 11 `drop-volume`** ([spec](../../../docs/architecture/item/spec-drop-volume.md)).
Read by `LootCorpusReader` (`src/FusionRpg.Core/Items/Drops/LootCorpus.cs`), judged by
`DropTableValidator`, drawn by `LootPipeline`.

## Why this is not `data/seed/items/drop-tables/`

There are two drop-table corpora and neither replaces the other.

| | `data/seed/items/drop-tables/` | **this directory** |
|---|---|---|
| Shape | the seedsmith's **authored input** — `dropBand` where a weight belongs, `qtyCurve` where a count belongs, because `seed-contract.md` §1 forbids an author typing a magnitude | the **generated** shape — real integer weights, real `minCount`/`maxCount`, real `affixChannel` |
| Contract | `entry-shapes.md` §9 | `ssot-generation.md` §5.1 |
| Size today | 40 tables, 92 groups, 468 entries (batch `drop-tables-1c`, 2026-08-22) | 10 tables |
| Status | ⏸ the band→row generator that would turn it into rows is stage-1b infrastructure and **does not exist yet** | live |

⛔ **The seedsmith corpus is not importable today, and it is not this module's to fix.** 315 of its
468 entries are of kinds this build cannot resolve to a payload — 144 `unique` (module 17), 70 `charm`
and 41 `insert` (both gated on **X7**, which `ContainerRow.cs`'s six `ContainerKind` values do not
ship), and 60 `consumable` (module 18, and `ssot-generation.md` §5.4 keeps it deliberately absent).
`DropTableValidator` refuses each **by name** with `ContentRuleViolated{drop.entry-kind-unavailable}`,
naming the module that lands it — never a silent drop and never a quiet fall-through to `nothing`.

## What is calibrated here

`spec-drop-volume.md` **Correction 1**, exactly, at **Θ = 20** — the pin where
`volumeScaleMilli = 1000‰ = ×1.0`:

| Table | `E[equipment]` at the pin |
|---|---|
| `drop.web.wave-normal` | 0.55 |
| `drop.web.wave-boss` | 1.40 |
| `drop.exp.scout-30m` | 0.70 |
| `drop.exp.forage-4h` | 1.60 |
| `drop.exp.hunt-8h` | 2.60 |
| `drop.exp.warpath-20h` | 4.20 |
| `drop.world.sector-clear` | 1.50 |
| `drop.pvz.run` | 0.50 |

`DropVolumeCorpusTests.At_theta_pin_the_shipped_per_event_yields_hold` asserts every row against this
file, in exact integer arithmetic. **Nothing here is expressed per day** — the game has no day axis
(verified: the only per-day concepts in Core are the demon-contract timers), so I12's
*"20–30 equipment items per day"* is restated per content event at the pin. The behavioural target it
was derived from is unchanged and is what a balance pass steers by: *the player looks at 100 % of
equipment drops and keeps 20–35 % of them.*

⛔ **`drop.exp.warpath-20h`'s yield is Correction 1's; its decomposition is not.** The spec writes
"4 + boss" (4 × 0.55 + 1.40 + 0.60), but `ExpeditionResolver.WaveChain("warpath-20h")` is **four waves
total** — `rift-warband`, `rift-onslaught`, `rift-onslaught`, `rift-tyrant` — three normal battles and
the boss. The table is re-derived against the shipped chain and still totals exactly 4.20.

## Where the first-clear grant lives

`item.first-clear-almanac-seed` is **not** in this directory. It is a real `effect_container`, so it
sits in `data/seed/containers/first-clear-grants.json` — an *owned folder* of
`SeedScanner.OwnedFolders`, which means it imports through the standard
`AtomSeedFile` → `RpgStore.ImportContent` path like every other container. A second, hand-written
writer beside the drop tables is exactly the drift module 7 refused when it seeded the rarity ladder.

It is also the **first shipped container to name a rarity at all** (`data/seed/rarity/README.md`
records that none did), so it is the first live exercise of the `effect_container.rarity` FK module 7
wired into `RpgStore.UpsertContainer` and the `ImportContent` batch path.

## Two things deliberately not authored

- **No `pvz-run` `loot_source`.** Item level for a PvZ run is **undesigned** — `mappedRunLevel` was
  never implemented anywhere, and §11 Q8 names two candidates (the player's own level, or a flat
  session level the PvZ side reports) and picks neither. Such a source is **refused by name**, never
  defaulted to 1. `drop.pvz.run` the *table* exists because Correction 1 calibrates it; wiring it is
  whoever owns standalone-first PvZ drops.
- **No `world-sector` `loot_source`.** `sectorLevel(danger_band)` is owed by the world program (X5).

## No cap, anywhere

There is no drop cap, no inventory ceiling, no per-run and no per-period limit in this directory or in
`src/FusionRpg.Core/Items/Drops/` — **D26**, proven by `No_drop_cap_exists_anywhere_in_the_pipeline`
rather than by review. I12 §8's `40/day` tripwire is read as written: it asks for a **loot filter**,
and the loot filter belongs to **module 20 `item-surfaces`**. What this module owes it is the
*measurement* — `item_drop_log` carries every mint, so inflow is queryable without a counter that
could become a gate.

## Smart loot is deferred, not omitted

Step 6 draws base types **uniform over the legal set**, and the shared `drop.shared.hybrid-core-*`
tables are that uniform slate: 12 hybrid-core roles × 2 frames, all at weight 1.

The reason is structural, not budgetary. I12 §3.3's smart loot is frame-weighted —
`frameWeight(f) = 250 + 750 × squadShareMilli(f) / 1000` — and it reads the deployed squad's **frame
mix**, which exists on no species type today (**X1** `frame-classify`, resolved 2026-09-03 and
unbuilt). A frame-weighted draw over an unclassified roster is a uniform draw with extra code. It is
also the one bias that can break step 6, and step 6 feeds step 9's `affix_channel`, which **X4**
weights composition off; landing a bias here before X4's weights exist means the two get tuned against
each other later, from opposite sides.

**Trigger to revisit: X1 built and X4 landed, whichever is later. Owner: this module, in a follow-up.**
`item_drop_log.context_json` already reserves and writes the two keys smart loot will need —
`smartLoot: false` and `squadFrameMix` — so §4.3's rule that *"a settings change must not alter an
already-sealed result"* is true from the first drop rather than retrofitted.

**Not deferred:** the 250-weight serendipity floor's *reason*, because it is the part a later session
would drop. I12: *"one drop in six is for a body you may not own … the only reason to keep hunting a
frame you have not unlocked."* A frame-weighted draw with **no** floor is the D3-style
manufactured-loot failure §9 names.
