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

## Correction 1 (2026-09-06, found building this module) — a real gap between this spec and structure-schema's own foundational rule

**Found by reading code and attempting the actual implementation, not by re-reading the spec text.**
This spec's own §4 requires: *"the four \[now eight, see structure-corpus's own correction\] shipped
structures must produce identical `StructureDef` values through the corpus path — same cost, same
yield multiplier, same build turns, same capacity bonus."* But `structure-schema`'s own foundational
rule (spec-structure-schema.md, tested and closed) is that the anchor **"holds no numbers at
all"** — enforced by `numeric_audit` over the schema itself, not an incidental omission.

**The contradiction, concretely:** `well`'s real `Cost` (200) and `granary`'s real `Cost` (150) are
independently-tuned numbers (`data/tuning/loam.v4.json`'s own `structures` block — a real balance
surface, CLAUDE.md's own tunables rule). §2's own proposed mechanism — resolving the anchor's
`costProfile` ordinal (cheap/moderate/steep) through one shared band table — can only ever produce
ONE number per band value. `well` and `granary` both authored `costProfile: "moderate"` in
`structure-corpus` (a legitimate ordinal choice, §2's own "ratio-band name, never an amount"). A
single shared "moderate" → number mapping cannot reproduce BOTH 200 and 150 at once. This is not a
hypothetical — it was found by trying to write the actual resolver and failing to make it work for
more than one row at a time.

**Resolution — a third, sibling key on a corpus row, `magnitudes`, never inside `anchor`:**

```jsonc
{
  "id": "well",
  "anchor": { /* the 21 schema fields, ordinals and identity only — unchanged */ },
  "_provenance": { "source": "AUTHORED", "citation": "..." },
  "magnitudes": {
    "cost": 200, "yieldMultiplierMilli": 2000, "buildTurns": 2, "capacityBonus": 0,
    "flatYieldPerTurn": 0, "constructRubbleCost": 0, "constructIronworkCost": 0,
    "materialTier": 0, "blocksMovement": false, "blocksLineOfFire": false,
    "obstacleKind": "None", "coverPowerMilli": 0, "coverRadius": 0,
    "entryStaminaMultiplierMilli": 1000, "visionRangeTiles": null
  }
}
```

- **`magnitudes` is what this module (`Configure`) actually reads to build a `StructureDef`.** A row
  with no `magnitudes` is identity-registered but not yet catalog-loadable — exactly the state
  `structure-corpus`'s 17 new anchor-only rows are in today, correctly: they have no pre-existing
  numbers to preserve, and inventing plausible-looking ones now would be exactly the Law 2 violation
  ("a model has no calibrated sense of scale... a number it picks... survives review because
  nothing looks wrong with it") this whole program has been careful to avoid everywhere else.
  `structure-planner` (27) is the module that assigns each new anchor its first real `magnitudes`
  block, deterministically, per decision 33's own "extend the tier ladder before any model call."
- **`magnitudes.materialTier` is always authoritative once present — never re-derived from
  `anchor.strengthBand`.** This is what makes this spec's own §4 claim ("the \[loam\] rows author
  tier zero") actually possible: `strengthBand` is a required, non-nullable, 3-value enum
  (rubble/timber/stone) with no "zero" member — it cannot itself express "no material tier, this
  predates any notion of siege." `magnitudes.materialTier: 0` says that directly, for exactly the
  seven pre-siege loam rows (all of `loam-source-placeholder`/`well`/`waystation`/`granary`/
  `soul-conduit`/`extractor`/`hatchery`), while `moat`'s `magnitudes.materialTier: 1` matches its
  real, already-shipped `MaterialTier`.
- **§2's `Bands.cs` still gets built, narrower than this spec's own literal text.** Only
  `strengthBand → int tier` (`MaterialTierOf`, rubble=1/timber=2/stone=3) is a real, needed
  resolution — feeding straight into the ALREADY-SHIPPED `StructurePolicy.TierMultiplierMilli(int)`
  (`StructurePolicy.cs:18`, keyed by `SiegeTuningPolicy.Structure.TierMultiplierMilli`), never a
  second, parallel string-keyed multiplier table (that would be a 6th instance of this codebase's
  own recurring "N synced lists" bug class — see `siege-fog`'s and `siege-construction`'s own
  evidence for the five prior instances). `reach`/`footprint`/`coverTier`/`costProfile`/`tempo` have
  **no consuming `StructureDef` field today** — resolving them now would be building a converter
  with no reader, the exact P3-5 shape this spec's own §2b already names as a defect elsewhere.
  They stay real, validated, unconsumed ordinals until a future module (an action/battle-facing one,
  most likely `structure-pipeline`'s own downstream content) gives one of them a reader.

**Why this is a technical correction, not a product decision needing the owner's own input** (the
bar this program has applied consistently — see `siege-construction`'s own §11 for the contrasting
case that WAS taken to the owner): there is exactly one way to keep `structure-schema`'s closed,
tested "no numbers in the anchor" rule AND make `structure-catalog-import`'s byte-identity gate
achievable, and it is this sidecar. No alternative reads differently for the player or the balance
surface — it only decides where a number that must exist somewhere actually lives.

## Correction 2 (2026-09-06, found while writing the C# importer itself) — `StructureKind` is not a function of `role`, and the existing role→kind dict is already wrong

**Found by trying to implement the derivation, not by re-reading the spec.** An early draft of
`StructureCatalog`'s corpus→`StructureDef` mapping derived `Kind` from `anchor.role`, reusing
`anchor/schema.py`'s own `ROLE_TO_STRUCTURE_KIND` dict as the intended source of truth
(`Extract`/`Multiply` → `LoamSource`, `Store`/`Bank` → `Storage`). Checked against the real, shipped
`StructureCatalog.cs` rows before trusting it: **wrong for three of the eight** — `hatchery`
(role `Multiply`), `soul-conduit` (role `Bank`) and `extractor` (role `Extract`) are all real,
already-shipped `StructureKind.Yield` rows, which that dict has no entry for producing at all (it
only ever resolves to `LoamSource`/`Storage`/`Refinery`/`None`).

**This is the same shape as Correction 1, one layer up**: `StructureKind`, like `Cost`, is a
per-row AUTHORED fact, never a pure function of any ordinal — `role` groups structures by economic
verb, `StructureKind` groups them by which of the five hardcoded C# behaviours
(`LoamSource`/`Storage`/`Yield`/`Refinery`/`Obstacle`) they mechanically run through, and a single
role can and does land in more than one bucket (three `Multiply`/`Bank`/`Extract` rows all landing
in `Yield` is proof, not a coincidence to paper over).

**Resolution**: `magnitudes.structureKind` (a plain string, `Enum.Parse`'d against the real C#
`StructureKind` enum) is the authoritative source for every catalog-loadable row — set once,
directly, by whoever authors that row's `magnitudes` (today: `structure-corpus`'s own dump of the
real, already-shipped value; future: `structure-planner`, deciding a NEW row's real behaviour
alongside its real numbers, never guessed from role after the fact).

**`anchor/schema.py`'s own `ROLE_TO_STRUCTURE_KIND` dict is left exactly as it was** — this
correction does not touch or fix it, because nothing in the real import path calls it any more.
Named here as its own, separate, deferred cleanup (its tests only check the dict's own internal
consistency, not against real content, so they keep passing despite being disconnected from what
actually ships) rather than silently left for a future session to rediscover as if new.

## As-built (2026-09-06, session 5) — 25.4 closed, one deviation from its own original deferral

Module CLOSED. 25.4 ("delete the C# literal") was first landed as **DELIBERATELY DEFERRED**, reasoned
as a wide-blast-radius effort needing either a bootstrap in 5+ test assemblies or a Server copy-item
rule, "genuinely separate" from what that pass could responsibly cover. Re-examined in a later pass
rather than accepted at face value, both concerns resolved into a small, concrete, precedented plan:

- Only **2** test assemblies (`Core.Tests`, `Data.Tests`) hold any real reference to
  `StructureCatalog`, confirmed by a repo-wide grep, not assumed from the module's own file list.
  `Guard.Tests`/`Launcher.Tests`/`CheatCore.Tests` were each run to completion to confirm zero
  reachability, rather than reasoned about from their domain names alone.
- `E2E.Tests` needs no bootstrap of its own at all — it boots the real `Program.cs`
  (`WebApplicationFactory<Program>`), so the same startup wiring this task adds there covers it.
- The Server's missing copy-item rule for `data/seed/structures` is the exact same bug class
  `data/seed/dungeon`/`data/seed/items` already hit and fixed — one more `<Content Include>` block,
  same shape.

`StructureCatalog.Seed` (the ~120-line, 8-row literal) and `BuiltOnly` are deleted. `Configure` is now
called from `Program.cs` (production) and two `[ModuleInitializer]`-based `StructureCatalogTestBootstrap.cs`
files (Core.Tests, Data.Tests) — `BuildRows()` throws a named, actionable exception if neither ever ran,
rather than silently returning nothing.

**Two real, previously-undiscovered defects were found by the full-suite verification this closure's
own termination gate required, and both were fixed in the same pass**: (1) this module's own
`StructureCatalogImportTests.cs` left `Configure(null)` in `finally` blocks from before `Seed` existed
as a fallback — with `Seed` gone, that call permanently broke the shared static catalog for every
later test in the process; fixed with a `RestoreRealCorpus()` helper. (2) An unrelated, previously
latent race in `FusionRpg.Core/Effects/EffectBag.cs`'s `InMemoryEffectCatalog.Upsert`/`ReplaceAll` —
sorting a caller-supplied `EffectDef.Actions` list IN PLACE, which corrupts a `static`-cached, shared
`EffectDef` list (`ConstructionActions.CompiledEffects`) under concurrent access — surfaced as an
intermittent `ConstructionActionsTests` failure only under full-suite parallel execution. Not this
module's own bug, but found running this module's own required verification and fixed rather than
left as a known-flaky test; see `base-defense-todo.md`'s 25.4 entry for the full account.

See `base-defense-todo.md`'s own 25.4 entry for the complete verification evidence (`CORE`/`DATA`/
`Guard`/`Launcher`/`CheatCore`/`E2E`/`Server`/`BOUND`/`NUM`/magic-numbers).
