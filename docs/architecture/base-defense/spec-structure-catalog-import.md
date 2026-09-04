# Spec: `structure-catalog-import`

**Module 25 of 29 · level c2 · depends on `structure-corpus` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Folded in by owner decision 45.

---

## Objective

**`StructureCatalog` reads the committed corpus instead of a C# literal.**

`structure-seed-ideal.md` §9's third step, and the one that makes the first two load-bearing rather
than aspirational: until the catalog reads the corpus, the corpus is a document beside the code.

**Success looks like:** the four shipped structures behave byte-identically, and adding a structure is
a committed JSON row rather than a rebuild.

---

## What already exists

**Built.** `StructureCatalog` — `static readonly IReadOnlyList<StructureDef> Seed = new StructureDef[]{…}`,
four rows, lazily validated through `All => _all ??= Validate(Seed)`, with `ByIdMap()` caching. Its
`Validate` already throws on a bad kebab id, a duplicate, a missing name, a negative cost.

**The precedent to copy.** `BattleModeProfileCatalog` records why a catalog is lazy rather than a static
field initializer:

> *"a static field initializer runs at class-load, which is **before** any host or test bootstrap calls
> `Configure`, so it could only ever have baked in a hardcoded value."*

`StructureCatalog` is already lazy, so the loading hook goes where `Configure` goes for battle tuning.

**Real gap.** Nothing reads a corpus.

---

## The contract

### 1. `Configure`, matching the shipped pattern

```csharp
/// <summary>
/// Called by the composition root, never by game code. Resets the cached rows so a reconfigure is
/// honoured rather than serving a stale catalog — the same contract BattleModeProfileCatalog.Configure
/// already states for profiles.
/// </summary>
public static void Configure(StructureCorpus corpus);
```

**The four C# rows become the fallback, not the source** — and then, once the corpus contains them
(`structure-corpus` §1), the fallback is **deleted**. Two sources of truth for the same four rows is
exactly the drift this module exists to end.

> **Order matters:** land the reader with the fallback, prove byte-identity, *then* delete the literal
> in the same change. Deleting first makes a failure ambiguous between "the reader is wrong" and "the
> corpus is wrong".

### 2. Ordinals become magnitudes here — nowhere else

The corpus carries `strengthBand`, `reach`, `footprint`, `coverTier`, `costProfile`, `tempo` — **all
ordinals**. This module resolves each through `data/tuning/structure-seed.v{n}.json`'s `bands` block.

```csharp
// The ONE place an ordinal becomes a number. Seedsmith Law 2's boundary, made a single function so it
// cannot quietly appear in three places with three interval tables.
long HpFor(string strengthBand, int developmentLevel) =>
    checked(PowerScale.P(developmentLevel) * Bands.TierMultiplierMilli(strengthBand) / 1000);
```

**`long`, widened before multiplying, divided by 1000 last, `checked`** — `CLAUDE.md` rules 1, 3, 4
and 5, and this is the exact line decision 32 describes.

**An unknown ordinal throws.** Loud over silent, matching `BattleModeProfileCatalog.Resolve`'s own
stance that *"content did not choose"* and *"content chose wrong"* are different failure modes and only
the first has a default.

### 2b. ⛔ P3-5 — a generated corpus with no surface

`structure-seed-ideal.md` §2.2, a wiring gap none of the six structure specs had noticed:

> *"**`StructureDef.Name` has no reader** outside its own validator. **Nothing in the game or web UI can
> name a structure** — so a generated corpus has no surface today."*

**Generating ~36 structures whose names nothing can display is a corpus that exists only in JSON.**

Two halves, and this module owns the first:

| Half | Owner | What |
|---|---|---|
| **A reader on the wire** | **this module** | The catalog exposes `Name` (and `role`, `obstacleKind`, `strengthBand`) on the structure DTO the world/battle reports already carry |
| **A surface that shows it** | `siege-stage` · `board-render` | The inspector panel naming the thing you are about to shoot |

**Without the first, the second cannot be built; without the second, the corpus is invisible.** Stated
here so neither is left assuming the other did it.

### 3. Validation extends rather than moves

`StructureCatalog.Validate` keeps its four existing rules and gains: every ordinal resolves to a band;
`acquisitionPaths` is non-empty; `requiredSlotKind` is a known `SlotKind`. **A bad row stays a startup
error, never a runtime surprise.**

### 4. Byte-identity is the gate

The four shipped structures must produce **identical `StructureDef` values** through the corpus path —
same cost, same yield multiplier, same build turns, same capacity bonus.

`MaxHp` is the one intentional difference: it was `0` (absent) and is now `strengthBand`-derived. The
four loam rows author **tier zero**, which `structure-state` defines as indestructible — so they are
unchanged in behaviour, and the world goldens do not move.

---

## Tunables

`data/tuning/structure-seed.v{n}.json` → `bands` and `cost`. **This module reads them; it authors none.**

## Numeric types

Every resolved magnitude is **`long`**, `checked`, with the divide by 1000 last and exactly once.
Ordinal keys are strings; band indices are `int`.

## Boundaries

**Always:** lazy + cached + `Configure`-resettable · one ordinal→magnitude function · throw on an
unknown ordinal · keep `Validate` throwing at load.

**Ask first:** deleting the C# fallback before byte-identity is proven.

**Never:** a static field initializer reading the corpus · two interval tables · a silent default for an
unknown ordinal · a `float` magnitude · ship the literal and the corpus as parallel sources.

---

## Testing

| Test | Asserts |
|---|---|
| `The_four_shipped_rows_are_byte_identical_through_the_corpus` | **the gate** |
| `World_goldens_unmoved` | tier zero keeps the loam rows indestructible, as before |
| `An_unknown_ordinal_throws_at_load` | not at first use |
| `A_missing_band_row_throws` | the ordinal is useless without its interval |
| `Hp_is_long_and_overflows_loudly` | `OverflowException`, not a wrapped negative |
| `Hp_divides_by_1000_last` | against a `BigInteger` reference |
| `Configure_resets_the_cache` | `BattleModeProfileCatalog`'s contract, matched |
| `No_static_initializer_reads_the_corpus` | the class-load hazard |
| `Adding_a_row_needs_no_rebuild` | the module's purpose, asserted |
| `Structure_name_reaches_the_wire` | **P3-5** — and a companion asserting it had no reader before |
| `Role_and_obstacle_kind_reach_the_wire` | the inspector's other fields |
| `The_csharp_literal_is_gone` | after byte-identity passes |

## Success criteria

1. The four shipped rows are byte-identical through the corpus.
2. World goldens unmoved.
3. Exactly one ordinal→magnitude function, `long` and `checked`.
4. Unknown ordinals throw at load.
5. The C# literal is deleted once byte-identity passes.

## Open questions

None.
