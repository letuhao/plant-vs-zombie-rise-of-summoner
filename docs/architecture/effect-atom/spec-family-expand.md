# Spec: family-expand (E43)

**Status: DRAFTED 2026-09-03** — added by the spec-coverage audit, which found the families→atoms
expansion rule **specced nowhere after W7.9 replaced its module.** Module **E43**, Wave 7. Depends on
**E30**, **E42**.

**What it owns: turning the 98 authored family definitions into atom rows.** W7.11.1's seam table assigns
*"families → atoms"* to effect-atom. W7.7.9 assigned it to E30. **W7.9.6 then replaced `atom-family-emit`
with `channel-pool`, which explicitly disclaims it** — *"Emit a corpus. No expansion, no cartesian, no
generated rows."* The rule the ideal called *"implemented nowhere"* became **specced nowhere too**, and
this module is the correction.

---

## 1. Why the gap opened, and why the fix is small now

The gap is my own: W7.9 correctly removed a **41,550-row cartesian emitter**, and in removing it also
removed the only module that owned the legitimate, much smaller expansion underneath.

**Under the pool model the expansion is a fraction of what it was**, and that is the point:

| Axis | Before W7.9 | Now |
|---|---|---|
| Channel / element | **materialised ×7** | **a pool reference** — resolved at layer 4 (E30) |
| Cell | materialised ×50 | **a target**, never an identity |
| Tier | materialised ×5 | **still materialised** — the table requires it (§3.2) |
| **Result** | ~41,550 rows | **~490 rows** — 98 families × 5 tiers |

**~490 lands inside this program's own documented sizing** (`atom-family-library.md` §310: *"~355
authored + ~980 generated"*), rather than 31× outside it.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| **98 atom families authored**, all 12 kinds, with `id`, `kindId`, `params{channel, op}` | **built, unswept** | `data/seed/items/affix-families/*.json` |
| `SeedScanner.OwnedFolders` | **does not include `items`** | `SeedScanner.cs:14-15` — so the importer never sees them |
| The expansion rule | ⛔ **real gap** — no file, no generator, no test | — |
| `AtomRow.DeriveId` → `{family}[.{variant}].t{tier}` | **built** | `AtomRow.cs:66-69` |
| `UNIQUE (family_id, tier, variant)` | **built** | `RpgStore.Atoms.cs:62-63` |
| `AffixLibraryGenerator` — atoms→affixes, 1:1 | **built, unwired** | a *different* stage; `effect-pipeline` module 3 owns it |

---

## 3. The contract

### 3.1 Input — the 98 families, reconciled not re-authored

**E43 does not author families.** It reads the existing definitions and emits rows.

**Where they live is this module's decision, and it must be made explicitly:** either
`SeedScanner.OwnedFolders` gains the folder, or the definitions move to one it already sweeps. **Leaving
them unswept is what produced the *"nobody wrote the content"* error in the first place** — an authored
corpus one directory outside the importer's view.

### 3.2 Output — one atom row per (family, tier)

**Tier is the one axis that still materialises**, and the database requires it:
`UNIQUE (family_id, tier, variant)` with `atom_id = {family}[.{variant}].t{tier}`. A tier that is not a
row has no id.

**Element does not materialise.** An element-typed family emits **one** row whose `params.channel` is a
**pool reference** (E30), not seven rows differing by variant. That is the whole point of L2, and it is
what keeps `variant` free for its real purpose — genuine variants of a family, not an axis the resolver
should own.

Each emitted row carries `"tags": { "generatedFrom": "<family file>", "generator": "E43" }`, so a
hand-edit is visible in review.

### 3.3 Naming — three CI gates make this a contract, not a convention

**The output must not be named `fx-*`.** Verified:

- `ElementEnumGen` globs `fx-*.json` **AllDirectories** and emits ~734 bytes / ~24 lines **per def inside
  one collection initializer in one method** — 490 defs would be survivable, but the glob is a filename
  convention nothing enforces.
- `EffectAtomCatalogGeneratedTests` asserts the generated id set equals **exactly 16 ids**. **A single new
  `fx-*.json` atom fails CI.**

**This module owns changing that glob to an allow-list and that assertion to a derived count** — the map
says each *"needs a named change, not a rename to dodge it"*, and E43 is the module whose output would
otherwise dodge them.

### 3.4 Magnitudes come from tier bands, never from this module

A family declares a `tierBand`; the band table supplies the numbers. **E43 emits structure and
references, never a magnitude it chose.**

**⚠️ It must not run before E42.** `definitions.md` §2's units row is wrong, and E43 emits ~490 rows of
tier-banded magnitudes from it.

---

## 4. What this module must NOT do

- **Emit a cartesian.** No element axis, no cell axis. W7.9 is binding.
- **Author or edit a family.** Reconcile and expand only. If a family is wrong, that is the item
  program's finding.
- **Emit affixes.** `effect-pipeline` module 3 owns atoms→affixes and its generator already exists.
- **Choose a magnitude.** Bands do that.
- **Create a second family namespace.** If the 98 need to move, they move — **copying them into a new
  folder is the duplicate-vocabulary defect the atom program exists to prevent.**
- **Name its output `fx-*`**, or edit the CI gates to be more permissive rather than more correct.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | Expansion is **deterministic** — two runs byte-identical | The generator is a pure function |
| 2 | `--check` regenerates in memory and **fails on drift** from the shipped rows | A stale emit cannot ship |
| 3 | Every emitted id matches `AtomRow.DeriveId` **exactly** | The id contract |
| 4 | No `(family_id, tier, variant)` collision across all 98 families | The UNIQUE index is satisfied by construction, not by luck |
| 5 | An element-typed family emits **one** row with a pool reference, **not seven** | W7.9 held |
| 6 | Every emitted row **validates** through `AtomRowValidator` and **prices** through `CostFunction` | Output is real content, not plausible JSON |
| 7 | **Planted violation:** a family naming an unregistered channel or an unknown pool is **refused by id** | E29/E30's guards reach generated rows too |
| 8 | **Planted violation:** an output file named `fx-*` **fails a test** | §3.3, mechanically |
| 9 | `EffectAtomCatalogGeneratedTests` asserts a **derived** count, and adding a family updates it without a hand edit | The 16-id gate is fixed, not dodged |
| 10 | The 21 pre-existing shipped atoms are **unchanged** — ids, prices, hashes | Additive |

---

## 6. Acceptance criteria

1. The 98 family definitions are swept by the importer — moved or the folder added, decided explicitly.
2. ~490 rows emitted, one per (family, tier); the exact count is **derived and reported**, never a literal.
3. Element-typed families emit a **pool reference**, one row each.
4. Every row validates and prices; none is `PowerVector.Zero` unless its kind genuinely is.
5. `--check` runs in CI and fails on drift.
6. `ElementEnumGen`'s glob is an allow-list; `EffectAtomCatalogGeneratedTests` asserts a derived count.
7. No `fx-*` output; a test plants one.
8. No second family namespace exists when this lands.
9. The 21 shipped atoms are untouched.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **E30** `channel-pool` | Element-typed families emit pool references — **without E30 this module has no way to express them but the cartesian W7.9 forbade** |
| **E42** `units-correction` | ⛔ **Hard prerequisite.** ~490 rows of tier-banded magnitudes come from a document with a known-wrong units row |
| **E28** `param-parity` | Families across all 12 kinds include params E28 unblocks; `spawn.entity` families would price at zero without its `atk` fix |
| **item program** | Authored the 98. **Moving or sweeping their folder is a shared decision**, not a unilateral one |
| **effect-pipeline module 3** | Consumes this module's output. 490 atoms → 490 affixes, 1:1 |
| **Stale instances** | The `catalog_revision` bump makes every rolled `effect_instance` unbindable. At 490 new rows this fires for certain — **it belongs in the rollout note, and this is the module that triggers it** |
