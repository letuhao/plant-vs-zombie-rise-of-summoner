# ARPG effects inspiration — index

Web-research observation pack for Diablo-like attribute, modifier, and **effect** systems (procs, buffs/debuffs, spawn chances, triggers). **Not** a product ADR and **not** the same as Fusion 3.8.1 Unity dump research elsewhere under `docs/research/`.

## How to read

1. Skim this index.
2. Read peers that match the question (`01` attrs, `02` stacking, `03` procs, …).
3. End at [`06-fusionrpg-mapping.md`](06-fusionrpg-mapping.md) for vocabulary → existing StatSystem / Progression.
4. Do not treat Steal bullets as implementation tickets until a later design pass.

## Document map

| File | Topic |
|---|---|
| [01-primary-attributes.md](01-primary-attributes.md) | Mainstats, primary→secondary, requirements, skill tags |
| [02-modifier-stacking.md](02-modifier-stacking.md) | Flat / Increased / More, buckets, local vs global |
| [03-effects-procs-triggers.md](03-effects-procs-triggers.md) | Lucky Hit, CoC/CwC, CtC, buffs, chance to spawn |
| [04-ailments-status.md](04-ailments-status.md) | DoT, stacks, Vulnerable/Chill, aura vs temp buff |
| [05-hit-crit-conversion.md](05-hit-crit-conversion.md) | OA/DA, accuracy/crit, convert-once |
| [06-fusionrpg-mapping.md](06-fusionrpg-mapping.md) | Map onto FusionRpg Core + future Effect bag |

## Games surveyed (priority)

| Priority | Series | Why |
|---|---|---|
| P0 | Path of Exile | Added/Inc/More, triggers, conversion |
| P0 | Diablo IV | Buckets, Lucky Hit, Vulnerable/Fortify |
| P0 | Last Epoch | Tags, ailments, overfill chances |
| P0 | Titan Quest 2 / Grim Dawn | Primary→secondary, OA/DA |
| P1 | Diablo II / III | Classic attrs, CtC, mainstat |

## Anchor in this repo

Existing compose ([architecture/stat-system.md](../../architecture/stat-system.md)):

```text
afterFlat = Y0 + Σ Flat
afterInc  = afterFlat * (1 + Σ Increased)
afterMore = afterInc * Π (1 + More)
```

Research docs **map** other games to that model; they do not propose changing locked Core math in this pass.

## Out of scope here

- Implementing EffectBag, items, or power formulas
- Changing compose math or schema
- Treating Unity almanac/stat dumps as the same folder
