# Spec: `commander-effect`

Module `commander-effect` in the [seedsmith map](../seedsmith-map.md) §3d.
Depends on `motif-prose-filter`, `workflow-runtime`, `quality-gates`. **The first real generator.**

`R#` = [audit](review/audit-agent-runtime-proposal.md).

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build.**
**Amended 2026-09-06:** added a corpus-wide near-duplicate check on `doctrine` (§6, §8) — the sealed
version had no distribution/diversity gate at corpus scale, only per-item quality checks, despite the
spec's own §9 probe already reproducing the failure mode at single-demon scale.

---

## 1. Objective

Generate one **commander effect** per demon — a *doctrine*: how that demon's squad behaves in battle.

**This is the module that makes the demons feature stop being a classifier.** D1–D4 produced 84
species sorted into families and zero content; `Coverage/DemonUncovered` reports **84 gaps, one per
demon**, because `aspect`, `commander-effect` and `environment` are declared kinds nothing writes
into. This closes the `commander-effect` third of that.

`commander-effect` is first because it is the only content kind that is **unblocked**: `aspect` waits
on `aspect-scope` being built in the demon program, and `environment` is cancelled as a deterministic
mapping.

**Done means:** `data/seed/demons/commander-effect/**` is committed, real, generated from real
motifs, and `Coverage/DemonUncovered`'s count falls.

---

## 2. Design

### 2.1 The expression rule is already decided — this module consumes it

`adapter-demons`'s `KindSpec` for `commander-effect` carries
`motif_expression = "a doctrine — how the squad behaves"`
([spec-adapter-demons.md](spec-adapter-demons.md) §2.7). That is the per-kind part of speech, and
audit A1 is why it exists: five generators handed the same motifs without expression rules produce
*Shell of Patience*, *Enduring Shell*, *Shellfield* — a thesaurus, with every check passing.

The rule is **inlined into the brief literally**, never cited. This module does not invent it.

### 2.2 Feasibility, measured before building

An 8-demon probe on the hard case (demons having **both** motifs and anti-motifs) with constrained
decoding and the real tier-2 validators:

| metric | result |
|---|---|
| first-attempt validator pass | **8/8** |
| anti-motif violated on attempt 1 | **0/8** |
| mean attempts | **1.00** |
| latency mean / max | **3.2s / 3.3s** |
| JSON parse failures | **0** |

**No hosted model tier is needed** (closing the proposal's Q3). `spec-pipeline.md:108` routes
cross-file reasoning to a stronger model; that routing stays available and is **not required by
evidence** here.

⚠️ **And that 8/8 is not a quality claim** — the same run produced visibly shoehorned output
(`quality-gates` §1). Tier 2 proved feasibility, not goodness. This module ships **behind
`quality-gates`' tier 3**, and its own acceptance measures quality separately (§6).

### 2.3 Input — and why the build order is what it is

Per demon: `speciesId`, display name, **motifs**, **anti-motifs**, family membership, and the
per-kind expression rule.

**Motifs must come from `motif-prose-filter`, not from today's committed data.** The current
`motif-assignments.json` contains `一类` ("armour-class one"), `僵尸` ("zombie"), `优先` ("priority") —
stat vocabulary from a stat table (R1). Generating doctrine from those produces exactly the
shoehorned text §2.2 warns about. **Building this before `motif-prose-filter` would bake bad input
into committed content**, which is the expensive kind of mistake.

### 2.4 Output shape

```json
{ "id": "commander-effect.wallnut",
  "nameKey": "commanderEffect.wallnut",
  "name": "…", "demonId": "wallnut",
  "doctrine": "one sentence: how the squad behaves",
  "basis": "text|name", "_provenance": { … } }
```

⛔ **The `commander-effect.` id prefix is required, not cosmetic (audit S8).** `Corpus.add` raises
`CorpusLoadError` on a duplicate id **across all kinds** — `entries` is one global dict and only
`by_kind` is partitioned. An effect keyed `wallnut` would **collide with the demon `wallnut`** and
fail corpus load outright. §6 asserts this so a later refactor cannot lose it.

- `demonId` is the `reference_field` `adapter-demons` already declares, so `planner.ordering` derives
  generation order structurally.
- **No numeric field.** `audit_schema` rejects one mechanically; `channels()` is empty for demons, so
  there is no numeric path to misuse (A4).
- `basis` propagates from the demon's motifs — content derived from `basis="name"` motifs is marked,
  because `lore-enrich` will later want to regenerate exactly those.
- `_provenance` per G2: pipeline id, model, prompt version, timestamp, finding closed.

### 2.5 Graph shape

Straight reuse of `workflow-runtime`'s skeleton, no new control flow:

```
START → brief → generate → validate ─(defects, attempts<3)→ generate
                              │                 │
                              │                 └─(attempts exhausted)→ escalate → END
                              └─(clean)→ cove_verify → persist → END
```

Bounded three ways (`attempts`, `recursion_limit`, terminal `escalate`), per `workflow-runtime` §2.3.

### 2.6 A `blocked` demon generates nothing

A demon whose motifs are `basis="blocked"` has nothing to build a doctrine *from*. It produces **no
commander effect**, and that is an answer, not a failure — the same rule `demon-themes` §2.4 already
applies. A doctrine invented for a demon with no motifs is content asserting a connection that does
not exist.

**`motif-prose-filter` will increase this population** (§2.3 of that spec) — expected and correct.

### 2.7 What this module does not do

No `aspect` (blocked), no `environment` (cancelled), no items or actions (that is `demon-themes`'
bridge). It writes one kind.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_commander_effect.py -q
python -m seedsmith demons generate --kind commander-effect --dry-run   # briefs only, no calls
python -m seedsmith demons generate --kind commander-effect             # real run, owner-approved
python -m seedsmith check ../../data/seed/demons --adapter demons
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/
    commander_effect.py     → brief assembly + schema + gate (kind-specific)
tools/seedsmith/seedsmith/workflow/graphs/
    commander_effect.py     → thin wiring over the shared skeleton
tools/seedsmith/tests/test_commander_effect.py
data/seed/demons/commander-effect/<side>/<rarity>.json   → emitted, committed
```

Partitioned `side/rarity` to match the demon corpus's own scheme, so `Coverage/EmptyPartition`
reports on the same strata.

---

## 5. Code style

Brief assembly is a pure function (testable without a model). The model call is behind
`workflow-runtime`'s injected seam. Schema is a module-level constant audited by `audit_schema` at
import time, matching `Pipeline.__post_init__`'s existing "an unusable schema cannot be registered"
rule.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Brief for a demon | inlines motifs, anti-motifs and the expression rule **literally**; **no citation-shaped text** |
| Schema | passes `audit_schema` (**no numeric field**) and `audit_open_loop_schema` |
| A demon with motifs | produces a doctrine referencing at least one |
| A demon with anti-motifs | output uses none of them |
| ⛔ A `blocked` demon | **generates nothing**, and this is not an error (§2.6) |
| Draft failing tier 2, then passing | repairs; second prompt carries the named defect |
| Draft never passing | **escalates**, writes nothing |
| `demonId` | present and resolving to a real demon; a dangling id is rejected |
| ⛔ An **unprefixed** id (`wallnut` rather than `commander-effect.wallnut`) | **fails corpus load** with `CorpusLoadError` — the collision in §2.4, asserted so it cannot regress |
| Output | contains **no** numeric field |
| `basis` | propagates from the demon's motifs, `name`-derived content marked |
| Re-run over unchanged input | **zero** new writes (G2 idempotence) |
| Same corpus generated twice (mock) | byte-identical files |
| Zero real model calls | `MockModelServer` only |
| ⛔ **Quality, measured separately** | on the first real run: report shoehorning rate by reading a **stratified sample**, not by quoting the validator pass rate (`quality-gates` §2.4) |
| ⛔ **Corpus-wide near-duplicate check on `doctrine`** | added 2026-09-06, closing a gap this spec's own §9 probe already evidenced: 3 generations for one demon produced pairwise Jaccard 0.42 / 0.68 / 0.45 (mean 0.52) — the thesaurus failure, not resolved by "one per demon" alone, because it constrains *repeats for one demon*, not *convergence across many*. `metrics` runs the shared `spec-analytics.md` §6.2 pipeline (5-gram shingles → MinHash → Jaccard, LSH-banded) over every committed `doctrine` string corpus-wide, the same shape the item corpus's Appendix A row 16 (Semantic dedup) already owns. A synthetic fixture with two doctrines above the similarity threshold fails the check naming both ids |

The **quality** and **near-duplicate** rows are the ones this module must not skip. Tier-2 pass rate is
already known to be 100% on bad content, and per-item `motif_coverage` cannot see convergence across
demons — a doctrine can use its own demon's motifs faithfully and still be the fourth
near-copy of the same sentence.

---

## 7. Boundaries

- **Always:** inline the expression rule; propagate `basis`; treat `blocked` as an answer; record
  `_provenance`; keep the schema numeric-free.
- **Ask first:** generating for `basis="name"` demons in bulk; changing the doctrine's shape; any
  second kind in this module.
- **Never:** generate for a `blocked` demon; invent a motif the demon does not have; emit a number;
  cite a registry by filename; run against the real model before `motif-prose-filter` has landed
  (§2.3); report the tier-2 pass rate as a quality result.

---

## 8. Success criteria

1. Every non-blocked demon has a commander effect; `Coverage/DemonUncovered` falls by that count.
2. `blocked` demons generate nothing, provably.
3. Schema numeric-free and verdict-free, asserted.
4. Briefs cite nothing.
5. Re-run produces zero new writes.
6. **Quality reported from a read sample, separately from the pass rate.**
7. Full seedsmith suite green; `check --adapter demons` shows the reduced gap count.
8. **Corpus-wide near-duplicate rate on `doctrine` is reported before the real run ships**, using the
   shared MinHash/Jaccard pipeline — not inferred from the single-demon probe in §9, which measured
   repeats for one demon, never convergence across many.

---

## 9. Open questions

**Closed 2026-09-01 by measurement** ([audit S7](review/audit-generation-runtime-specs.md)).

1. ~~One commander effect per demon, or several?~~ ✅ **CLOSED — exactly one.** Generated **three for
   the same demon** at temperature 0.9:

   ```
   1. Phalanx Shell (阵列外壳法则)
   2. 坚实防线 (Stalwart Phalanx)
   3. Phalanx Shell (方阵护壳)
   ```

   **Two of three produced literally the same name.** Pairwise character-overlap Jaccard 0.42 /
   0.68 / 0.45, **mean 0.52** — synonyms, not alternatives. This is audit **A1's thesaurus failure**
   reproduced at single-demon scale. "Several" would need a differentiation rule that does not
   exist, so **one per demon**, and the boundary in §7 is now a hard rule rather than an assumption.
