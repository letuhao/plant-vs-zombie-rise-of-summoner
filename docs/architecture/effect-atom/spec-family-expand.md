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

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — **the definitions do not move, and the importer never sees them.** The *output* is what gets swept

§3.1 offered two options and §7 said the choice was *"a shared decision, not a unilateral one"* with
the item program. **Both are satisfied by a third shape that neither section considered, and it is the
one the repo already uses for exactly this problem:**

**E43 is a generator with a `--check` mode, on the `DemonSpeciesGen` pattern.** It **reads** the 98
definitions as a tool input, and **writes** atom seed files into `data/seed/atoms/generated/` — a
directory already inside `OwnedFolders`' `atoms` root (`SeedScanner.cs:14-15`), so the importer sweeps
the **output** and never parses a family file.

**Why this resolves the §3.1-vs-§7 contradiction rather than picking a side:**

- **Nothing moves.** The item program keeps `data/seed/items/affix-families/` and everything that reads
  it. There is no shared decision left to make, because the shared thing is unchanged.
- **`OwnedFolders` is not widened either.** Adding `items` would point `AtomSeedFile` at the whole item
  seed tree, whose files carry kinds like `affix-family` that `TryKind` refuses (`AtomSeedFile.cs:457-471`)
  — every one would fail the import. Adding `items/affix-families` specifically would work, but it
  would still require `AtomSeedFile` to learn a kind it has no reason to know.
- **§4's *"no second family namespace"* holds by construction.** The generator reads the one namespace;
  it never copies it.
- **The precedent is shipped and already gated in CI:** `DemonSpeciesGen --check` regenerates the
  committed tree and fails on any difference (`ci.yml:43-51`), which is verbatim §5 test 2.

**Acceptance 1 is rewritten accordingly** — the criterion is that the **generated rows** are swept and
that a stale generation fails CI, not that the definitions are.

**What would overturn it:** the item program deciding to move the families for its own reasons. That is
their call and it costs this module one path constant, because the generator names its input explicitly
rather than discovering it.

### 3.2 Output — one atom row per (family, tier)

**Tier is the one axis that still materialises**, and the database requires it:
`UNIQUE (family_id, tier, variant)` with `atom_id = {family}[.{variant}].t{tier}`. A tier that is not a
row has no id.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — **five rows per family, 490 total.** `bands.v1.json` stays frozen and untouched

An objection was raised that `bands.v1.json`'s `tierMap` maps each `powerBand` to exactly one tier, so
98 families would give 98 rows rather than 490 — and that the file is `"frozen": true`, so the
contradiction could not be resolved by editing it.

**The objection rests on a misreading of what `tierMap` is, and the file itself says so.** `tierMap` is
a **band-name → tier-index vocabulary map** — five entries, `trivial:1 … extreme:5` — not a per-family
assignment. Its own `bandCountRationale` states the relationship in the opposite direction:

> *"5, one per tier the atom layer already has… **A sixth `powerBand` would need a sixth `.t6` atom row
> on every family** — an atom-layer change, not a bands-registry one."*
> — `data/seed/items/_registry/bands.v1.json`, `powerBand.bandCountRationale`

*"a `.t6` row on **every** family"* only makes sense if every family already has `.t1`–`.t5`.

**Two independent confirmations, both arithmetic:**

- `ssot-affixes.md`'s own section heading — *"Is a tier-5 roll a separate pool entry or a window? — **A
  separate pool entry**"* — followed by: *"`atom_id` derives as `{family_id}[.{variant}].t{tier}` and
  the unique key is `(family_id, tier, variant)`, so **every tier of every variant is its own atom row**
  and therefore its own `effect_container_pool` row with its own weight"* (`ssot-affixes.md:583-588`).
- The wave-1 sizing table three sections later: *"Prefix, live (`stat.modify`) — **14** families —
  **70** atom rows at 5 tiers"* (`ssot-affixes.md:627-631`). 70 ÷ 14 = **5**.

**And `powerBand`'s actual job is stated in the seed contract**, which settles what a family declares
it *for*: it is what an author writes **instead of the ladder's anchor**, not instead of the ladder —

| Instead of | Author writes |
|---|---|
| `t1: 10, ratio: 1750` | `powerBand: "medium"` |

— `item/seed-contract.md:99`, and `:322`: *"No tier magnitudes — generated from `powerBand` and the
channel family."*

**So there is no contradiction and nothing to reconcile.** `bands.v1.json` is correct, stays frozen, and
E43 emits **98 × 5 = 490** rows exactly as §1 and §6.2 already say. **The consequence of recording this
is that the next reader does not re-litigate it** — the misreading is available to anyone who opens
`tierMap` without `bandCountRationale`.

**What would overturn it:** a family declaring its own tier count or a `minTier`/`maxTier` that
narrows its emitted set. `ssot-affixes.md:575-577` describes `min_tier = 4` on `bulwark` and
`savagery` as a **pool eligibility** filter, not an emission filter — the rows still exist, they are
filtered out of low-ilvl pools — so today no family emits fewer than five. If one ever does, §6.2's
*"the exact count is derived and reported, never a literal"* already covers it.

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
- `EffectAtomCatalogGeneratedTests` fails on any new shipped atom. **A single new `fx-*.json` atom
  fails CI.**

  > **⛔ CORRECTED 2026-09-03 — the mechanism is not a count, so "assert a derived count" would not fix
  > it.** The test is `Has_the_same_sixteen_ids_as_the_retired_hand_written_catalog`, and its body is
  > `Assert.Equal(EffectSeedCatalog.CreateAll().Select(d => d.EffectId), EffectAtomCatalog.CreateAll().Select(d => d.EffectId))`
  > (`tests/FusionRpg.Core.Tests/Atoms/EffectAtomCatalogGeneratedTests.cs:21-27`). **"Sixteen" is in
  > the method name only** — the assertion is **set equality against a frozen hand-written catalog**,
  > so a 17th id fails it however the count is computed.
  >
  > **The named change is therefore a scope, not a count:** compare only the ids
  > `EffectSeedCatalog` declares — `Assert.Equal(seeded, generated.Where(id => seededSet.Contains(id)))`
  > plus `Assert.Superset` — which keeps the property the test was written for (*"the generated catalog
  > still reproduces every retired hand-written def exactly"*) while allowing the catalog to grow.
  > **Rename the method too**, or it goes on lying about what it checks.

**This module owns changing that glob to an allow-list and that assertion to a scoped comparison** — the
map says each *"needs a named change, not a rename to dodge it"*, and E43 is the module whose output
would otherwise dodge them.

### 3.4 Magnitudes come from tier bands, never from this module

A family declares a **`powerBand`**; `data/seed/items/_registry/bands.v1.json` supplies the numbers.
**E43 emits structure and references, never a magnitude it chose.**

> **⛔ CORRECTED 2026-09-03 — the field is `powerBand`, not `tierBand`.** No family declares `tierBand`;
> all 98 declare `powerBand` (e.g. `data/seed/items/affix-families/g-elem-power.json`, every entry).
> `tierBand` appears nowhere in `data/` or `src/`. An implementation written against this spec's old
> wording would have read a field that does not exist and taken the band's default on all 98.

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
| 9 | `EffectAtomCatalogGeneratedTests` still proves every retired hand-written def is reproduced, **and** passes with the catalog grown past 16 | The frozen-catalog gate is fixed, not dodged |
| 10 | The 21 pre-existing shipped atoms are **unchanged** — ids, prices, hashes | Additive |

---

## 6. Acceptance criteria

1. The 98 family definitions **stay where they are** and are read as a **generator input**; the
   generator's output under `data/seed/atoms/generated/` is what the importer sweeps, and a stale
   generation fails CI via `--check` (§3.1, decided 2026-09-03).
2. ~490 rows emitted, one per (family, tier); the exact count is **derived and reported**, never a literal.
3. Element-typed families emit a **pool reference**, one row each.
4. Every row validates and prices; none is `PowerVector.Zero` unless its kind genuinely is.
5. `--check` runs in CI and fails on drift.
6. `ElementEnumGen`'s glob is an allow-list; `EffectAtomCatalogGeneratedTests` compares only the ids
   `EffectSeedCatalog` declares, and its method name matches what it now asserts (§3.3).
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
| **item program** | Authored the 98. **Nothing of theirs moves** — §3.1's decision makes the folder a generator input, not a swept seed root, so there is no shared decision left open. Tell them the generator reads it; that is the whole coordination |
| **effect-pipeline module 3** | Consumes this module's output. 490 atoms → 490 affixes, 1:1 |
| **Stale instances** | The `catalog_revision` bump makes every rolled `effect_instance` unbindable. At 490 new rows this fires for certain — **it belongs in the rollout note, and this is the module that triggers it** |
