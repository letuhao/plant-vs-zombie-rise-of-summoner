# Spec: characteristic-pool (A-S0)

**Module id:** `characteristic-pool` · **Program:** [action-corpus](../action-corpus-map.md) §4 · **Build order:** 2 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none.** Every value it emits is read from a shipped table or derived from one.

It owns the closed characteristic pool every later stage draws its brief fields from, and the
**species role lean** — the A2 hybrid: a family-level floor, a deterministic derivation that
differentiates within the family, and a measured **residue** where the derivation does not separate.
It reads the demon seed and **never invents an anchor**. Where a species has no family, no motif or no
theme, that absence is recorded as an absence.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. **A cell is a target, never an identity** — the pool this module emits names
   atom *families*, never a resolved `(family, tier, variant)` triple.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan**; this module
   spends zero of it, and its output must be reviewable before a single token is spent.
3. **The roster is 84 species, not 904.** Measured 2026-09-03: 84 `SpeciesId` rows in
   `DemonSpeciesCatalog.Generated.cs`, 84 keys in `motif-assignments.json`, **53** in
   `family-assignments.json`. Per-species counts are tunables.
4. **C1's family-access widening is gated** on three things that do not exist. So the pool this module
   emits carries **structure axes** as the tier differentiator, and its `allowedAtomFamilies` is the
   same set for every tier until those three land.

## 1. What exists today

### Built — and measured, so the joins are facts rather than hopes

| Thing | Count / evidence |
|---|---|
| Species catalog with `ElementPrimary`, `ElementSecondary`, `BaseRarity`, `Side`, `DeployMode`, `Acquisition`, `Variants`, `TraitPool` | **84 rows**, `DemonSpeciesCatalog.Generated.cs:14+` |
| Motif anchors (`motifs`, `antiMotifs`, `basis`, `tautological`) | **84 keys**, `data/seed/demons/_generated/motif-assignments.json`. Joins the catalog **100%** (0 keys outside it) |
| Family assignments | **53 keys** over **19 distinct families**, `family-assignments.json`. All 53 are catalog species; no species carries two families |
| Theme registry with `themeKey` = `demon.<speciesId>` | **84 themes**, `data/seed/demons/_registry/themes.v1.json`; `expression.action` = *"tempo and effect shape — how fast, how it lands"* |
| Closed vocabularies | 5 categories `ActionEnums.cs:26-33` · 8 tags `:39-49` · 3 kinds `:10-15` · 6 target modes `ActionTargetSpec.cs:16-32` · 4 area shapes `:41-47` · 6 elements `ActorElementTypes.cs:3-11` · 21 statuses `StatusCatalogBootstrap.cs:15-56` · 10 rungs with `structureBudget` `data/tuning/action-rungs.v1.json` |
| Rarity ladder, 10 rungs, ordinal is rank | `DemonRarity.cs:16-27` |
| Legacy band to ladder map (`common→Chaff`, `rare→Cultivated`, `epic→Heirloom`, `legendary→Sunwoven`) | `DemonRarity.cs:95-100` |

### Wiring gap

| Thing | Evidence |
|---|---|
| The classified species anchor tree covers **28** species, its ids are PascalCase (`CherryBomb`), and **0** match the lowercase motif keys directly — 19 match after `lower()` | `data/seed/demons/species/_index.json` (28 entries); measured 2026-09-03 |
| **9** of those 28 anchors are not catalog species at all (`armedgargantuar`, `balloonzombie`, `bigchomper`, `doomshroom`, `drownzombie`, `jacksonzombie`, `potatomine`, `smallpuff`, `snorklezombie`) | measured |
| The four-way join (catalog ∩ motif ∩ family ∩ anchor) is **8 species** | measured |
| `attackTempo` is `"steady"` for all 28 anchors — a field with one value carries no signal | measured |
| The themes registry restates rarity in the **legacy band** vocabulary (`common`/`rare`/`epic`/`legendary`), and the counts match the catalog ladder exactly (42/21/14/7) | measured; map at `DemonRarity.cs:95-100` |

### Real gap

There is no characteristic pool and no role lean. `type-weights.json` is named in
`spec-action-seeding.md:173` and does not exist.

## 2. Inputs and outputs

**Reads (all committed, all offline):**

| Path | For |
|---|---|
| `src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs` | the roster, element, rarity, side, deploy mode, acquisition, traits |
| `data/seed/demons/_generated/motif-assignments.json` | motifs, antiMotifs, basis |
| `data/seed/demons/_generated/family-assignments.json` | family |
| `data/seed/demons/_registry/themes.v1.json` | `themeKey`, `expression.action` |
| `data/seed/demons/species/**/*.json` | the optional 19-species enrichment (aptitude, posture, reach, targetPreference) |
| `data/tuning/action-rungs.v1.json` | rung windows and `structureBudget` |
| `data/tuning/action-role-lean.v1.json` | **new** — the derivation weights, per-mille |

**Writes** (envelope per [A-C1](spec-corpus-loader.md)):

- `data/seed/actions/_generated/characteristic-pool.json` — `kind: "action-characteristic-pool"`,
  one entry per closed group A–F of `action-corpus-ideal.md` §12.
- `data/seed/actions/_generated/role-lean.json` — `kind: "action-role-lean"`, one entry per species:

```jsonc
{
  "id": "lean.cherrybomb",
  "speciesKey": "cherrybomb",
  "family": "cherry",                 // null when unassigned — 31 of 84 today
  "themeKey": "demon.cherrybomb",
  "element": { "primary": "fire", "secondary": "none" },
  "rarity": "cultivated",             // the LADDER id, never the legacy band
  "motifs": ["樱桃", "爆炸"], "antiMotifs": [],
  "leanOrder": ["attack", "status", "movement", "support", "defense"],
  "leanSource": "derived",            // floor | derived | derived-nofloor | residue
  "separation": 3,                    // rank distance from the family floor, 0..4;
                                      // null when the species has no family — §3's F12 correction
  "signals": ["trait:berserker", "trait:soul-eater", "element:fire", "rarity:cultivated"]
}
```

**No numbers leave this module except integer ranks and counts.** The weight vector is
[A-T1](spec-type-weights.md)'s job; this module emits an **ordering**, not a magnitude.

## 3. The algorithm

1. **Key normalisation.** The canonical species key is the catalog `SpeciesId`, lowercase. The anchor
   tree is joined on `speciesId.lower()`. An anchor whose lowered id is not a catalog species is
   recorded in an `unjoined` list with its id — **never dropped silently, never renamed to fit**. Today
   that list has exactly 9 members.
2. **Anchor assembly, per species, in catalog order.** Element and rarity come from the **catalog**,
   because the catalog covers all 84 while the anchor tree covers 19. Motifs come from
   `motif-assignments.json`. Family comes from `family-assignments.json` or is `null`. `themeKey` comes
   from the theme registry. If the theme registry's `rarity` is a legacy band, it is mapped through
   `DemonRarity.cs:95-100` and never carried forward in the legacy vocabulary.
3. **Family floor.** For each of the 19 families, the floor lean is the category ordering produced by
   summing its members' signal scores (step 4) and ranking.

   **⛔ CORRECTED 2026-09-03 (review F12). A family-less species is derived, not floored.** The
   earlier rule sent a species with no family to a **uniform floor** — *"all five categories tied,
   `leanSource: "floor"`, `separation: 0`"* — which discarded a derivation this module already has
   for **31 of 84 species, 37% of the roster**. Step 4's signals exist for **all 84**: `TraitPool` is
   populated on every catalog row (measured 2026-09-03 — 84 rows, **zero** with an empty pool,
   `DemonSpeciesCatalog.Generated.cs:14+`), `ElementPrimary` is set on every row, and `BaseRarity` is
   set on every row. Absence of a *family* is not absence of a *signal*.

   The rule, restated:

   - **A species with a family:** floor from its family, then step 4's derivation on top;
     `leanSource: "derived"`, `separation` measured against the floor (step 5).
   - **A species with no family (31 today):** step 4's derivation runs **unchanged**, over the same
     signals, with **no floor to differentiate from**. `family: null` stays null — no family is
     invented, which is what §4's first rule actually forbids. `leanSource: "derived-nofloor"`, and
     `separation: null` — **not `0`** — because separation is *distance from a floor* and there is
     no floor to be distant from. A `0` there is indistinguishable from "derived and identical to its
     family", which is a genuinely different fact.
   - **The uniform floor survives as a real case, and only as a real one:** a species whose signals
     produce a five-way tie after step 4 gets `leanSource: "floor"` and `separation: null`. Measured
     against today's data that case is expected to be empty, and §6 AC4b asserts the ordering rule
     that makes it representable anyway.

   A uniform floor is a declared absence, not a guess — but declaring an absence that is not there
   is its own kind of guess.
4. **Deterministic derivation.** Each species accumulates an integer score per category from closed
   signals, using per-mille weights read from `data/tuning/action-role-lean.v1.json`:
   - **traits** — the 14-member closed pool measured on the catalog (`soul-eater` 28, `guardian` 27,
     `coward` 21, `berserker` 21, `critical-hunter` 20, `regenerator` 20, `loyal` 20, `swift` 17,
     `greedy` 15, `genius` 14, `bloodthirsty` 14, `chaos-marked` 12, `void-touched` 9, `immortal` 7).
     Each trait carries one weighted category row in the tuning file.
   - **element**, primary and secondary (`ActorElementTypes.cs:3-11`).
   - **rarity rung ordinal** (`DemonRarity.cs:16-27`), as a tie-shaping term only.
   - **anchor enrichment where present** — `posture`, `reach`, `targetPreference`. `attackTempo` is
     **excluded by measurement**: it is `"steady"` on all 28 anchors and therefore carries no signal.

   Arithmetic: `long` throughout, widen before multiplying, **divide by 1000 last, exactly once**.
   Ranking is by descending score; ties break on the declared category order
   (`ActionEnums.cs:119-123`), which is a total order, so the result cannot depend on enumeration
   order. **This step runs for all 84 species, family or not** (step 3's F12 correction) — the trait,
   element and rarity signals are catalog-wide, and the anchor enrichment is the only one that is
   present for a subset (19 species) rather than for all.
5. **Residue measurement (Checkpoint 2).** `separation` is the rank distance between a species'
   derived order and its family floor, and it is **defined only for a species that has a family**. A
   species with `separation == 0` inside a family of two or more is **residue** — the derivation did
   not differentiate it. The count, the list and the per-family histogram are reported over the **53**
   family-assigned species; the other 31 carry `separation: null` and are reported as a separate
   count, never folded into the residue as if the derivation had failed on them. A2's *"model only for
   the residue"* is a later stage's option; **this module never calls one and never hands the residue
   to one.**

   **A-T1 reads `separation` and must handle `null`.** Its §3 step 2 indexes
   `separationMilli[separation]` over 0..4; a `null` takes the **flattest** row, the same treatment
   `separation == 0` gets, because "no floor to differentiate from" and "did not differentiate" are
   both honest representations of *we cannot spread this one further* — but they stay distinguishable
   in the file, which is the point of the correction.
6. **Canonical write.** Sorted keys, fixed indent, `\n` line endings, explicit nulls, CJK unescaped —
   so the hash is stable and staleness keeps meaning something.

## 4. What it must NOT do

- **Never invent an anchor.** A species with no family gets `null`, not a guessed family. A species
  with no motifs (0 of 84 today) would get an empty list, not a fabricated one. 31 of 84 have empty
  `antiMotifs`; that stays empty.
- Never call a model, and never import the transport.
- Never emit a weight, probability or duration — those are A-T1's and the tuning file's. **The model
  writes identity; deterministic code writes magnitude**, and this module is on the deterministic side
  of that line in both directions.
- Never treat the 904-row almanac dump as the roster. Constraint 3.
- Never carry the legacy rarity band (`common`/`rare`/`epic`/`legendary`) past step 2 — that would be a
  second rarity vocabulary, which is the exact defect `spec-action-seeding.md:98` names.
- Never narrow `allowedAtomFamilies` per tier. Constraint 4.
- Never rename or drop an unjoined anchor to make a join succeed.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | two runs over unchanged inputs produce byte-identical `role-lean.json` and `characteristic-pool.json`, asserted by hash |
| **Join counts** | asserted as literals against today's measured data: 84 catalog · 84 motif · 53 family · 19 families · 28 anchors · **9** unjoined · 8 in the four-way join. A change in any of these is a real content change and should fail loudly |
| **Planted violation — invented anchor** | a species stripped of its family assignment must come out with `family: null`, `leanSource: "derived-nofloor"` and a **non-empty** `signals` list. If any code path substitutes a neighbour's family, the test fails — and so does a path that drops the derivation and returns a five-way tie (review F12) |
| **Family-less species are derived** | over the 31 family-less species today, the count whose `leanOrder` is **not** the bare declared order is asserted to be greater than zero; a build where all 31 come out identical means step 4 did not run for them |
| **Planted violation — legacy rarity leak** | a role-lean entry carrying `"rarity": "epic"` is refused; only the 10 ladder ids are legal |
| **Planted violation — degenerate signal** | re-adding `attackTempo` as a scoring signal changes no species' lean, which the test asserts, so a future contributor sees why it was excluded |
| **Tie determinism** | two species with identical signals produce identical `leanOrder`, and shuffling the input file order changes nothing |
| **Overflow** | scores are computed in `long`; a synthetic species with the maximum trait count and the top rarity rung does not overflow, and a forced overflow **throws** rather than wrapping |
| **Offline guarantee** | the suite passes with the transport stubbed to raise |

## 6. Acceptance criteria

1. `role-lean.json` has exactly one entry per catalog species — **84** — with no entry for any of the
   9 unjoined anchors.
2. Every entry's `rarity` is one of the 10 ladder ids; no legacy band appears anywhere in the output.
3. Every entry with `family: null` has `leanSource: "derived-nofloor"` and `separation: null`; there
   are 31 such entries today (84 − 53), and each one's `signals` list is **non-empty** — asserted, so
   the F12 defect cannot come back silently. ⛔ **CORRECTED 2026-09-03 (review F12):** this criterion
   required `leanSource: "floor"` and `separation: 0` for all 31, which is what discarded the
   derivation.
4. `leanOrder` is a permutation of the five categories in `ActionEnums.cs:119-123` for every entry.
4b. **The permutation is total even under a tie.** ⛔ **CORRECTED 2026-09-03 (review F12):** a
   five-way tie cannot serialise as *"a permutation of the five categories"* without inventing an
   order, and an invented order reads as a preference. The rule, stated: on a score tie the order is
   the **declared** category order (`ActionCategories.All`, `ActionEnums.cs:119-123`), which is
   already this module's total tie-break — so the serialisation is the declared order, not a
   preference, and `leanSource` (`floor` / `derived-nofloor` / `derived`) is what tells a reader which
   it is. A consumer that treats a `floor` entry's `leanOrder` as a ranking is reading a tie as a
   preference; A-S6's `roleLeanMatch` term states its own handling of that
   (`spec-innate-picker.md` §3.2).
5. The residue count and the per-family separation histogram are printed and written, which is what
   Checkpoint 2 asks for: *"the role lean is reported with its separation."*
6. All derivation weights live in `data/tuning/action-role-lean.v1.json`; a grep over the module's
   source finds no bare numeric literal other than indices and `0`/`1`.
7. A second run over unchanged inputs is byte-identical by hash, and provenance records the corpus
   hash and the tuning file version each entry was derived from.
8. The whole run makes zero model calls, proven by a stub that raises.

## 7. Dependencies

**Depends on:** nothing in this program (map §5 — `A-S0` is a root alongside `A-C1`). It writes through
A-C1's envelope, so building A-C1 first is a convenience, not a hard order.
**Depended on by:** **A-T1** (weight vector), **A-S1** (every brief's group B anchor), **A-S6**
(`roleLeanMatch` is the first term of the ranking tuple).
**Cross-program (map §7):** species anchors come from **seedsmith D2/D5** — 84 motif, **53** family;
rarity for the unassigned rest is unspecced, and this module records that as `family: null` rather
than waiting on it.
