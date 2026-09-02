# Seed content

Two unrelated corpora share this folder. Each has its own format and its own tool.

| Folder | Format | Tool |
|---|---|---|
| `items/` | item base types, gems, exemplars | `tools/ItemSeedValidator` |
| `atoms/`, `containers/`, `curves/`, `rarity/` | effect atoms (E14a) | `tools/AtomImporter` |

The atom importer sweeps **only** those four folders, never the seed root, so the two never collide.
Files whose name starts with `_` are notes and are skipped.

## Running it

```powershell
dotnet run --project tools\AtomImporter -- --check     # validate against the real catalog, write nothing
dotnet run --project tools\AtomImporter                # import
```

The database comes from `--db <dir>`, then `FUSIONRPG_DATA`, then `dist/FusionRpg.Server/data`.
Exit codes: `0` imported (or checked clean), `1` refused, `2` could not start.

**An import is all or nothing.** Every row is validated before the first write, and every write shares
one transaction, so a refused import leaves the catalog byte-identical. Importing unchanged files a
second time changes nothing at all — not the rows, not `catalog_revision`, not the content hash.

## The file format

```json
{
  "schemaVersion": 1,
  "kind": "atom",
  "entries": [ ... ]
}
```

`kind` is one of `atom` · `container` · `curve` · `rarity`, and it comes from the **file**, not the
folder — the folders are a convention for humans. `schemaVersion` must be `1`; anything else is
refused rather than read optimistically, because a format that grew a field would otherwise import
with that field silently dropped.

Every id is unique across **all** files and all four kinds. A duplicate is refused, never resolved:
last-write-wins would make the imported catalog depend on the order the filesystem handed over files.

### `atom`

```json
{
  "kind": "stat.modify",
  "family": "atom.vitality",
  "variant": "",
  "tier": 1,
  "name": "Vitality I",
  "when":   { "trigger": "onDamageDealt", "chance": 250, "icdMs": 500 },
  "params": { "channel": "maxHp", "op": "flat", "amount": 45 },
  "tags":   { "category": "survivability" },
  "icdKey": "atom.vitality",
  "enabled": true
}
```

`id` is optional and derived as `{family}[.{variant}].t{tier}` when absent. Writing one that
disagrees with those columns is an `IdMismatch` refusal — it is kept as authored so the validator can
say so, rather than quietly rewritten.

`when`, `params`, `tags`, `power` and `powerOverride` are **nested objects**, not embedded strings.
They are stored canonically (keys sorted, no whitespace), so re-indenting a file or reordering keys
inside an object is not a content change and does not move the hash. Changing a value does.

`amount` and the other magnitudes take a plain integer or a value spec —
`{ "min": 10, "max": 20, "roll": "onInstantiate", "curve": "curve.hp.level" }`. A range needs a roll
policy; a fixed value with a range is refused.

### `container`

```json
{
  "id": "item.ring-of-vigour",
  "kind": "item",
  "slot": "ring",
  "rarity": "rare",
  "minTier": 1, "maxTier": 3, "levelReq": 5,
  "prefixRolls": 2, "suffixRolls": 1,
  "atoms": [ { "atom": "atom.vitality.t1", "overrides": { "amount": 60 } } ],
  "pool":  [ { "atom": "atom.ember.fire.t1", "weight": 100, "group": "atom.ember|fire" } ]
}
```

`kind` is one of `item` · `trait` · `skill` · `species-passive` · `patron` · `world-buff`. Entries in
`atoms` are numbered by their position unless they carry an explicit `seq`. A container may reference
an atom authored in the **same** import — a new item and its affixes normally arrive together.

### `curve`

```json
{ "id": "curve.hp.level", "input": "level",
  "points": [ { "x": 1, "mult": 1000 }, { "x": 10, "mult": 2000 } ] }
```

`input` is `level` · `rarity` · `tier`; `mult` is per-mille. Points must be ordered and distinct in
`x`. Point order is content — reordering is a real edit.

### `rarity`

```json
{ "id": "rare", "ordinal": 2, "prefixRolls": 2, "suffixRolls": 1, "minTier": 1, "maxTier": 3 }
```

Ordinals are append-only: they are load-bearing for sorting and for the budget lookup, so an ordinal
already held by a different band is refused rather than renumbered underneath the content naming it.
