# Spec: `family-consolidate`

Module `family-consolidate` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D2**.
Depends on `family-extract`.

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: proposed 2026-08-31, awaiting owner review. Not authorized to build.**

---

## 1. Objective

Turn recorded candidate labels into **the family vocabulary** — the one artifact every downstream
module inherits from.

This is where the taxonomy is actually decided, and audit A6 is the reason it is its own module:

> Merging `nut`, `wall-nut`, `defensive-nut`, `shell-type` into one family is **clustering**. An LLM
> doing it fresh each run returns a different taxonomy each run — breaking determinism, this repo's
> stated product, in the one artifact everything else inherits from.

**Done means:** an append-only `families.v1.json` exists, every demon maps to zero or more family
ids, and running consolidation again over the same candidates reproduces it exactly.

---

## 2. Design

### 2.0 ⛔ This module's core rule is blocked on a decision in `family-extract` — audit S7

§2.1's head-noun merge assumes **English labels**. The roster's names are Chinese
(`钻石套娃僵尸`), so if extraction emits native labels this algorithm does not degrade — it is
undefined, and the synonym map becomes the entire merge.

`spec-family-extract` §2.2a carries the decision. **Do not build this module before it is answered.**

### 2.1 Deterministic by construction — the default path

Consolidation runs over `family-extract`'s **committed** output, so its input is fixed. The merge is
mechanical:

1. **Normalize** each label — lowercase, strip punctuation, collapse whitespace, kebab-case.
2. **Merge by head noun.** `wall-nut`, `defensive-nut` and `nut-type` all reduce to head `nut`.
   Head extraction is a documented rule over the normalized token list, not a model call.
3. **Merge by exact synonym**, from a small committed synonym map (`shell` ≈ `armor-plated`) that a
   human edits and the algorithm only reads.
4. **Order by first appearance** in `speciesId` order, so ids are stable and reproducible.

Everything here is a pure function of `(candidates, synonym map)`. Same inputs ⇒ byte-identical
vocabulary, forever.

### 2.2 The escape hatch, and its price

Mechanical merging will not catch everything — `shambler` and `lurcher` are the same family to a
reader and share no token. A model call is permitted for the residue, on one condition:

**Its output is committed and re-run only deliberately**, exactly as
`DemonSpeciesCatalog.Generated.cs` already is — *"Do not hand-edit — rebalance via the generator, then
re-emit."*

So the pipeline is: mechanical merge → residue → optional one-off model pass → **commit the result**.
Downstream reads the committed file, never the model. Silently re-running a non-deterministic
consolidation is the failure A6 names; committing its output is the standard remedy this repo already
uses in three places.

### 2.3 Append-only, and why it can never be retrofitted

`families.v1.json` is **append-only**: a family id, once published, never changes meaning, never gets
renamed, and never moves position.

This is not caution. Family ids become the **partition key** (`demon-metrics`) and a **motif
inheritance channel** (`motif-derive`), and generated content is content-addressed against them.
Renaming a family after content exists silently re-parents everything derived from it, and no test
would notice — the content is still there, still valid, and now describing a different demon.

Superseding an id (splitting a family, retiring one) is therefore a **versioned change** —
`families.v2.json` with a documented migration — not an edit.

### 2.4 Multi-membership

A demon belongs to zero, one or several families (owner, 2026-08-31). Consolidation therefore emits
`speciesId -> [familyId]`, and two consequences must not be lost:

- **Partition counts no longer sum to the roster size.** Any metric assuming they do is wrong (A5),
  which is why `demon-metrics` carries a per-demon coverage check as well as a per-family one.
- **A demon with zero families is legal**, not an error — it is a `blocked` extraction faithfully
  carried through. It shows up as work remaining (Q6b), not as a validation failure.

### 2.5 What consolidation must not do

It must not **invent** a family that no candidate proposed. Its input is the recorded extraction; its
job is merging, not authoring. A family with no supporting candidate is unfalsifiable — nobody can
tell whether it came from the corpus or from a model's sense of what families ought to exist.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_family_consolidate.py -q
python -m seedsmith family consolidate            # mechanical pass; writes the vocabulary
python -m seedsmith family consolidate --residue  # optional model pass over unmerged residue
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/family/
    consolidate.py   → normalize, head-noun merge, synonym merge, id assignment
    synonyms.json    → human-edited, algorithm-read
tools/seedsmith/tests/test_family_consolidate.py
data/seed/demons/_registry/families.v1.json          → the vocabulary, committed, append-only
data/seed/demons/_generated/family-assignments.json  → speciesId -> [familyId], committed
```

---

## 5. Code style

A pure function per merge rule, composed — so each rule is testable alone and a surviving mutant
points at one rule rather than at "the merge". No I/O below the top-level entry point.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Same candidates consolidated twice | **byte-identical** vocabulary and assignments |
| `wall-nut`, `defensive-nut`, `nut-type` | one family, head noun `nut` |
| `shell` with `armor-plated` in the synonym map | merged |
| `shell` with an **empty** synonym map | **not** merged — proves the map is load-bearing rather than decorative |
| A demon with `basis = "blocked"` | **zero** families, and this is not an error |
| A demon with candidates from two heads | **both** families — multi-membership works |
| A family id present in `families.v1.json` | never renamed or re-positioned by a later run over new candidates |
| Adding a new demon and re-running | existing ids unchanged; new family appended at the end |
| A family with no supporting candidate | rejected — consolidation cannot invent (§2.5) |
| Vocabulary | inlinable into a brief; no citation-shaped text |

The append-only rows are the ones that must never be relaxed: they are cheap now and impossible to
add once content has been generated against the ids.

---

## 7. Boundaries

- **Always:** run over recorded candidates, never live extraction; keep merge rules pure; append new
  ids at the end; commit both outputs.
- **Ask first:** a new merge rule; using the model pass (§2.2) rather than extending the synonym map;
  splitting or retiring a family id.
- **Never:** rename or re-position a published family id; invent a family no candidate proposed;
  re-run a non-deterministic step without committing its output.

---

## 8. Success criteria

1. Byte-identical output across runs, proven by test.
2. Append-only holds when new demons are added — existing ids untouched.
3. Multi-membership and zero-membership both work and are both legal.
4. The synonym map is proven load-bearing by its empty-map contrast test.
5. Consolidation cannot invent a family.
6. The vocabulary is inlinable into briefs.

---

## 9. Open questions

1. ⛔ **Promoted to a blocker (audit S7) — see §2.0.** Not an open question: the merge rule is
   undefined until `family-extract`'s label language is decided.
2. **Is the synonym map per-feature or shared?** Items may want one too. Shared risks coupling two
   corpora through a file neither owns.
