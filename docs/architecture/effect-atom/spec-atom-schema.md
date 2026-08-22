# Spec: atom-schema (E4)

Module **E4** in the [atom effect map](../effect-atom-map.md). Depends on **E1** (kinds), **E2** (values), **E3** (predicates). Schema only — nothing in the game changes.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

The `effect_atom` table: the **SSOT base effect list**. One row is one atom — the smallest statement of what happens, with its numbers, its condition, and its power price. Code owns the logic; this table owns the values.

## Design (locked on approval)

### `effect_atom`

| Column | Type | Notes |
|---|---|---|
| `atom_id` | TEXT PK | stable kebab id — `atom.fire-rider.t3` |
| `kind_id` | TEXT | validated against the E1 registry; unknown → **load rejection** |
| `family_id` | TEXT | groups the tiers of one affix — `atom.fire-rider` |
| `variant` | TEXT | discriminator within a family — element id, channel. `''` (never NULL) when a family has one member |
| `tier` | INT | strength band within the family; `1` when a family has one tier |
| `name` | TEXT | display |
| `when_json` | TEXT | trigger, `chance` ‰, `icd_ms`, and the E3 predicate tree |
| `params_json` | TEXT | E1 schema-validated; numeric leaves are E2 **value specs** |
| `tags_json` | TEXT | element, family, category — for AI, UI, and cost lookup |
| `power_json` | TEXT | computed category vector (E9) |
| `power_override_json` | TEXT | nullable designer override |
| `power_note` | TEXT | **required when an override is set** |
| `icd_key` | TEXT, nullable, defaults to `atom_id`. **E7 groups on it at compile time** — atoms sharing a key compile into one grant whose `Triggers` is the union of theirs (definitions §14.1). Not a runtime key |
| `enabled` | INT | 0/1 |
| `revision` | INT | cache bust; joins the content hash (E8) |

**E4 also creates `content_meta`** — the one-row table holding `catalog_revision`, the monotonic integer E6 reproduces against and E7 keys its bake cache on. It lands here because E4 is the earliest module in `FusionRpg.Data`, and E14a (the importer) bumps it once per import transaction.

**Unique:** `(family_id, tier, variant)`. *(The earlier `(family_id, tier)` forbade the generation rule outright — `elemental_power` × 7 element slots (6 elements + `omni`) × 5 tiers is 35 rows over 5 tiers, and the key rejected 30 of them.)* `atom_id` is **derived** as `{family_id}[.{variant}].t{tier}` and validated against its columns; a mismatch is `IdMismatch`. **Indexed:** `kind_id`, and the trigger extracted from `when_json` — the bag already keeps a trigger index, and the runner (E15) needs the same shape.

### Tier is how strong; rarity is how many

Two axes, never conflated. A tier is a strength band *within one family*. Rarity — which lives on the container (E5) — decides **how many** atoms roll and **which tiers are allowed in the pool**. That split is how PoE and D4 both do it, and it is why loot rarity needs no third mechanism.

### `when_json`

```json
{ "trigger": "OnDamageDealt", "chance": 800, "icd_ms": 250,
  "predicate": { "op": "and", "children": [ … ] } }
```

- `trigger` is one of the **5 authorable** triggers (`OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`, `OnTimer`), or **absent** for permanent modifiers. `OnGranted`/`OnRemoved` stay in the 7-name vocabulary as runtime lifecycle states and reject as `TriggerNotAllowed` (definitions §14.2). **Encoding: the JSON key is simply omitted** — there is no `None` trigger name, so the closed count stays 7 and E1's guard is unaffected. The extracted trigger index column is therefore **nullable**. `OnTimer` is included and carries its cadence in `params_json`.
- `chance` is **integer ‰**. Absent means 1000 (always).
- `icd_ms` — absent on `OnDamageDealt` / `OnDamageTaken` defaults to **250**, the shipped Foundation rule. `0` is explicit "no ICD" and must be written, never inferred.
- `predicate` is optional; absent means always. It compiles through E3 at load — a tree that fails to compile is a **row rejection**, not a disabled row.

### `params_json`

Keys are exactly what the kind's `ParamSchema` declares, and **only** keys the executor honours (E1/G1). Numeric leaves are value specs:

```json
{ "element": "fire", "amount": { "min": 100, "max": 200, "roll": "onApply" } }
```

### Validation is at load, and it rejects rows — not fields

A row that fails any check is **rejected whole**, logged with its `atom_id` and reason, and does not enter the catalog. There is no partial atom and no disabled-on-error state, because a half-loaded atom is exactly the silent-failure class this program exists to remove.

| Check | Source |
|---|---|
| `kind_id` known | E1 |
| every param key declared **and honoured** | E1 |
| value specs well-formed (`Fixed` ⇒ `Min == Max`, `Min ≤ Max`) | E2 |
| `curveId` resolves | E2 |
| predicate compiles within depth 4 / 16 nodes | E3 |
| leaf subjects explicit | E3 |
| `power_note` present when `power_override_json` is set | E9 |
| `(family_id, tier, variant)` unique | this module |
| `atom_id` equals `{family_id}[.{variant}].t{tier}` | `IdMismatch` |

### Where the rows come from

Seed and migration files (E14), not a runtime loader and not an editor. The 16 `EffectSeedCatalog` defs become rows in **E11**, proven by the **49** existing fixtures — the number this program had been quoting as 19.

### Boundaries with the layers around it

- **SQL lives only in `FusionRpg.Data`** (`guard-dal.ps1`). Core sees a loaded, validated, compiled catalog and never a connection.
- **No instance data here.** Frozen rolls, roll seeds, and bindings are E6.
- **The atom layer compiles; it never applies.** Rows become Foundation grant shapes through E7.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AtomStore"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs          (new — DDL, upsert, read, revision)
                                                     (also: content_meta — the single catalog_revision row)
                                                     (also: content_meta — the single catalog_revision row)
src/FusionRpg.Core/Effects/Atoms/AtomRow.cs          (new — loaded row DTO)
src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs (new — the table above)
tests/FusionRpg.Data.Tests/AtomStoreTests.cs
tests/FusionRpg.Core.Tests/Atoms/AtomRowValidatorTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Round-trip a row through SQLite | byte-identical `when_json` / `params_json` |
| Unknown `kind_id` | row rejected, reason logged, catalog unaffected |
| Param key the executor drops (`atk` on plant spawn) | rejected (G1) |
| `Fixed` with `Min != Max` | rejected |
| Predicate at depth 5 | rejected |
| `sideIs` without `subject` | rejected |
| Override set, `power_note` empty | rejected |
| Duplicate `(family_id, tier, variant)` | insert fails |
| 7 element-slot variants (6 elements + `omni`) of one family at one tier | **all insert** — the generation rule works |
| `atom_id` disagreeing with its columns | `IdMismatch` |
| One bad row in a seed file of 50, **at load** | 49 load, 1 rejected, load succeeds — defence in depth against a database edited outside the importer |
| The same file **at import** (E14a) | **nothing is imported** — a partial import produces a hash for a state nobody authored. Two phases, not two competing policies |
| Any row edit | `revision` bumps; content hash (E8) changes |
| An identical re-write | **nothing happens** — the update is skipped, so `revision` counts changes, not writes. It is a hashed column, and bumping it on a repeat import made the import look like an edit (E14a) |
| `guard-dal.ps1` | passes — no SQL outside `FusionRpg.Data` |

## Boundaries

**Always:** reject the whole row; keep SQL inside `FusionRpg.Data`; integer ‰ for chance and curve multipliers; log `atom_id` + reason on every rejection.

**Ask first:** adding a column; changing the uniqueness key; changing the default ICD.

**Never:** a disabled-on-error row; instance or binding data in this table; a formula string in `params_json`; a runtime content loader.
