# Spec: `structure-corpus`

**Module 24 of 29 · level c1 · depends on `structure-schema` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Folded in by owner decision 45.

---

## Objective

**Author the corpus by hand first, so the model extends a real distribution instead of defining one.**

This is `structure-seed-ideal.md` §3's load-bearing difference, and decision 43 confirmed the premise
it rests on:

> *"The demon pipeline **classifies** an existing corpus … A structure corpus has to be **invented**.
> There is no almanac of trenches."*

and, because static plants stay demons (decision 43):

> *"the PvZ corpus is **not** available for reuse here … **So the source material is the design
> research, not a datamine**: base-defense §5.18's seventeen historical works reduced to four obstacle
> kinds, and §5.21's ten economic roles. That is **~25–30 seed concepts before any model is called** —
> which is almost exactly §5.21's own 24–40 estimate, and it means **the corpus can be authored by hand
> first and generated second.**"*

**Zero model calls. Zero tokens.** The most valuable module in the seed set and the cheapest.

---

## Why hand-authoring first is the guard, not the shortcut

§6 names the failure this prevents, and it is the one majority-vote cannot catch:

| Mode | Guard |
|---|---|
| **Mode collapse in invention** — nine variations of "Sturdy Wall" | *"The corpus is authored by hand first, so the model extends a real distribution rather than defining one"* |
| **Generic flavour** | Open-loop by nature → **a review queue, never a pass** |

A classify pipeline gets its distribution from reality. An invention pipeline has none until someone
supplies it — and a model asked to invent from nothing regresses to the mean of its training data,
which is exactly nine Sturdy Walls.

---

## What already exists

**Built.** `StructureCatalog`'s four rows — `loam-source-placeholder`, `well`, `waystation`, `granary`
— all `LoamSource` or `Storage` kind, as a **C# literal**. The design research: §5.18's four obstacle
kinds plus Emplacement, and §5.21's ten economic roles.

**Real gap.** No corpus, no importer, no dump.

---

## The contract

### 1. Dump the four shipped rows first — the importer proof

Before authoring anything new, export `StructureCatalog`'s four rows into the schema. **That proves the
importer path end-to-end against content whose behaviour is already tested**, so a later diff in
`structure-catalog-import` attributes cleanly.

`StructureKind.Refinery` (from `siege-construction`) joins `LoamSource` and `Storage`.

### 2. The hand-authored corpus — ~36 rows, from research not invention

§4 computes the target and it is not a guess:

- **10 roles** — Extract · Refine · Multiply · Store · Move · Bank · Enable · Defend · See · Deny
- **`controlPoint` is not a second axis** — decision 25 makes it *derivable from role*, so it is a
  `DERIVED` field. Correlated, not orthogonal.
- **One axis, 10 roles, 24–40 types = 2.4–4.0 per cell.** The safe band is ~3.6 (Genshin, FGO); ~12.6
  is the failure zone (FEH). **Target the upper half: ~36 types.**

Sources, and each row must cite one:

| Source | Contributes |
|---|---|
| §5.18 | Trench (2 tiers) · Rampart · Wire · Mine · Emplacement — **the five with mechanics already specced** |
| §5.21 | the ten economic roles — mine, farm, workshop, market, granary, waystation, refinery, ward, watchtower, bank |
| Shipped | the four dumped rows |

### 3. Coverage is checked, not hoped

Per-role counts against `budget` targets. §P2: *"a metric without a declared target is an opinion."*
A role with zero rows is a **hole in the taxonomy**, not an empty cell — either author into it or cut
the role, and record which.

### 4. ⚠️ A complete anchor set is NOT a complete roster

Stated explicitly because a downstream session will otherwise think the job is done:

> *"Type + speed modes + resistances lifts creature uniqueness from **63% to 93%**. A 900-unit roster
> needs roughly **1,500–3,500 named ability instances**. **A complete anchor is not a complete
> roster.**"*

~36 structure anchors is the **identity** layer. Traits and actions per structure are a separate,
larger body of work, and `structure-pipeline` is where scale arrives.

### 5. Idempotency, proven by hash

> *"Stochastic output breaks idempotency and you will not notice. Provenance + `stale_ids()` +
> byte-identical rerun proven by hash. **This repo has already shipped this bug once.**"*

Hand-authored rows are trivially idempotent — which makes this the right module to **build and prove
the rerun harness in**, before a model is anywhere near it.

---

## Tunables

`data/tuning/structure-seed.v{n}.json` → `budget`: per-role target counts. No numbers in seed rows.

## Numeric types

None. Ordinals and ids only.

## Boundaries

**Always:** hand-author before generating · cite a source per row · commit every row · check per-role
coverage against declared targets.

**Ask first:** cutting a role · exceeding ~40 rows (it pushes grid density toward the failure zone).

**Never:** call a model in this module · a numeric field · a tier chain as separate rows · reclassify a
PvZ plant (decision 43).

---

## Testing

| Test | Asserts |
|---|---|
| `The_four_shipped_rows_round_trip_through_the_schema` | the importer path, end to end |
| `Every_row_cites_a_source` | research, not invention |
| `Per_role_counts_meet_declared_targets` | the skew guard's input |
| `No_role_has_zero_rows` | or the cut is recorded |
| `Grid_density_is_between_2_4_and_4_0` | §4's computed band |
| `Rerun_is_byte_identical_proven_by_hash` | the idempotency harness, built here |
| `Corpus_holds_no_numbers` | `structure-schema`'s audit, over real content |
| `No_pvz_plant_appears_in_the_corpus` | decision 43 |

## Success criteria

1. The four shipped rows are dumped and round-trip.
2. ~36 hand-authored rows, every one citing §5.18 or §5.21.
3. Grid density computed and in band.
4. Byte-identical rerun proven by hash.
5. **Zero model calls.**

## Open questions

None. §4 computes the target; §3 fixes the method.
