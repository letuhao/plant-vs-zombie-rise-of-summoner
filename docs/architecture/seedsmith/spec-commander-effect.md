# Spec: `commander-effect`

Module `commander-effect` in the [seedsmith map](../seedsmith-map.md) §3d.
Depends on `motif-prose-filter`, `workflow-runtime`, `quality-gates`. **The first real generator.**

`R#` = [audit](review/audit-agent-runtime-proposal.md).

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build. BUILT 2026-09-01 (G4,
`seedsmith-plan.md` Part 5) — 84 commander effects committed, `data/seed/creatures/commander-effect/all.json`.**

**Amended and built 2026-09-06:** the sealed version had no distribution/diversity gate on
`doctrine` at corpus scale, only per-item quality checks — despite the spec's own §9 probe already
reproducing the failure mode at single-creature scale (Jaccard mean 0.52 across 3 generations). Checked
against the real, already-committed 84-entry corpus: it already had **two near-duplicate doctrine
pairs** (Jaccard 0.52 and, below the calibrated threshold, 0.41) with nothing watching it. Closed by
adding `KindSpec.dedup_fields` (`adapters/base.py`, additive, same shape as `motif_expression`) and
extending `SemanticDedup` (`seedsmith/metrics/dedup.py`) to run its near-duplicate pipeline over a
kind's declared prose field, not just `name`. `commander-effect`'s `KindSpec` now sets
`dedup_fields=frozenset({"doctrine"})`. 7 new tests in `test_constraint_exemplar_dedup.py`
(`ProseDedupTests`), including a known-answer test pinned against the live corpus. Full mechanism:
`spec-analytics.md` §6.2b.

---

## 1. Objective

Generate one **commander effect** per creature — a *doctrine*: how that creature's squad behaves in battle.

**This is the module that makes the creatures feature stop being a classifier.** D1–D4 produced 84
species sorted into families and zero content; `Coverage/CreatureUncovered` reports **84 gaps, one per
creature**, because `aspect`, `commander-effect` and `environment` are declared kinds nothing writes
into. This closes the `commander-effect` third of that.

`commander-effect` is first because it is the only content kind that is **unblocked**: `aspect` waits
on `aspect-scope` being built in the creature program, and `environment` is cancelled as a deterministic
mapping.

**Done means:** `data/seed/creatures/commander-effect/**` is committed, real, generated from real
motifs, and `Coverage/CreatureUncovered`'s count falls.

---

## 2. Design

### 2.1 The expression rule is already decided — this module consumes it

`adapter-creatures`'s `KindSpec` for `commander-effect` carries
`motif_expression = "a doctrine — how the squad behaves"`
([spec-adapter-creatures.md](spec-adapter-creatures.md) §2.7). That is the per-kind part of speech, and
audit A1 is why it exists: five generators handed the same motifs without expression rules produce
*Shell of Patience*, *Enduring Shell*, *Shellfield* — a thesaurus, with every check passing.

The rule is **inlined into the brief literally**, never cited. This module does not invent it.

### 2.2 Feasibility, measured before building

An 8-creature probe on the hard case (creatures having **both** motifs and anti-motifs) with constrained
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

Per creature: `speciesId`, display name, **motifs**, **anti-motifs**, family membership, and the
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
  "name": "…", "creatureId": "wallnut",
  "doctrine": "one sentence: how the squad behaves",
  "basis": "text|name", "_provenance": { … } }
```

⛔ **The `commander-effect.` id prefix is required, not cosmetic (audit S8).** `Corpus.add` raises
`CorpusLoadError` on a duplicate id **across all kinds** — `entries` is one global dict and only
`by_kind` is partitioned. An effect keyed `wallnut` would **collide with the creature `wallnut`** and
fail corpus load outright. §6 asserts this so a later refactor cannot lose it.

- `creatureId` is the `reference_field` `adapter-creatures` already declares, so `planner.ordering` derives
  generation order structurally.
- **No numeric field.** `audit_schema` rejects one mechanically; `channels()` is empty for creatures, so
  there is no numeric path to misuse (A4).
- `basis` propagates from the creature's motifs — content derived from `basis="name"` motifs is marked,
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

### 2.6 A `blocked` creature generates nothing

A creature whose motifs are `basis="blocked"` has nothing to build a doctrine *from*. It produces **no
commander effect**, and that is an answer, not a failure — the same rule `creature-themes` §2.4 already
applies. A doctrine invented for a creature with no motifs is content asserting a connection that does
not exist.

**`motif-prose-filter` will increase this population** (§2.3 of that spec) — expected and correct.

### 2.7 What this module does not do

No `aspect` (blocked), no `environment` (cancelled), no items or actions (that is `creature-themes`'
bridge). It writes one kind.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_commander_effect.py -q
python -m seedsmith creatures generate --kind commander-effect --dry-run   # briefs only, no calls
python -m seedsmith creatures generate --kind commander-effect             # real run, owner-approved
python -m seedsmith check ../../data/seed/creatures --adapter creatures
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/creatures/
    commander_effect.py     → brief assembly + schema + gate (kind-specific)
tools/seedsmith/seedsmith/workflow/graphs/
    commander_effect.py     → thin wiring over the shared skeleton
tools/seedsmith/tests/test_commander_effect.py
data/seed/creatures/commander-effect/<side>/<rarity>.json   → emitted, committed
```

Partitioned `side/rarity` to match the creature corpus's own scheme, so `Coverage/EmptyPartition`
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
| Brief for a creature | inlines motifs, anti-motifs and the expression rule **literally**; **no citation-shaped text** |
| Schema | passes `audit_schema` (**no numeric field**) and `audit_open_loop_schema` |
| A creature with motifs | produces a doctrine referencing at least one |
| A creature with anti-motifs | output uses none of them |
| ⛔ A `blocked` creature | **generates nothing**, and this is not an error (§2.6) |
| Draft failing tier 2, then passing | repairs; second prompt carries the named defect |
| Draft never passing | **escalates**, writes nothing |
| `creatureId` | present and resolving to a real creature; a dangling id is rejected |
| ⛔ An **unprefixed** id (`wallnut` rather than `commander-effect.wallnut`) | **fails corpus load** with `CorpusLoadError` — the collision in §2.4, asserted so it cannot regress |
| Output | contains **no** numeric field |
| `basis` | propagates from the creature's motifs, `name`-derived content marked |
| Re-run over unchanged input | **zero** new writes (G2 idempotence) |
| Same corpus generated twice (mock) | byte-identical files |
| Zero real model calls | `MockModelServer` only |
| ⛔ **Quality, measured separately** | on the first real run: report shoehorning rate by reading a **stratified sample**, not by quoting the validator pass rate (`quality-gates` §2.4) |
| ⛔ **Corpus-wide near-duplicate check on `doctrine`** | **BUILT 2026-09-06**, closing a gap this spec's own §9 probe already evidenced: 3 generations for one creature produced pairwise Jaccard 0.42 / 0.68 / 0.45 (mean 0.52) — the thesaurus failure, not resolved by "one per creature" alone, because it constrains *repeats for one creature*, not *convergence across many*. `SemanticDedup` (`spec-analytics.md` §6.2b) runs a direct all-pairs Jaccard over every committed `doctrine` string corpus-wide via `KindSpec.dedup_fields`, the same shape the item corpus's Appendix A row 16 (Semantic dedup) already owns — **run against the live corpus, it already found two real pairs** (`doublecherry`~`doubleshooter`, Jaccard 0.52) that shipped unnoticed. `ProseDedupTests` in `test_constraint_exemplar_dedup.py` pins both the synthetic case and this exact live pair as a known-answer regression test |

The **quality** and **near-duplicate** rows are the ones this module must not skip. Tier-2 pass rate is
already known to be 100% on bad content, and per-item `motif_coverage` cannot see convergence across
creatures — a doctrine can use its own creature's motifs faithfully and still be the fourth
near-copy of the same sentence.

---

## 7. Boundaries

- **Always:** inline the expression rule; propagate `basis`; treat `blocked` as an answer; record
  `_provenance`; keep the schema numeric-free.
- **Ask first:** generating for `basis="name"` creatures in bulk; changing the doctrine's shape; any
  second kind in this module.
- **Never:** generate for a `blocked` creature; invent a motif the creature does not have; emit a number;
  cite a registry by filename; run against the real model before `motif-prose-filter` has landed
  (§2.3); report the tier-2 pass rate as a quality result.

---

## 8. Success criteria

1. Every non-blocked creature has a commander effect; `Coverage/CreatureUncovered` falls by that count.
2. `blocked` creatures generate nothing, provably.
3. Schema numeric-free and verdict-free, asserted.
4. Briefs cite nothing.
5. Re-run produces zero new writes.
6. **Quality reported from a read sample, separately from the pass rate.**
7. Full seedsmith suite green; `check --adapter creatures` shows the reduced gap count.
8. **BUILT 2026-09-06.** Corpus-wide near-duplicate rate on `doctrine` is measurable via
   `SemanticDedup` (direct Jaccard, not the single-creature probe in §9, which measured repeats for one
   creature, never convergence across many). The already-shipped 84-entry corpus reports one pair —
   `doublecherry`~`doubleshooter`, Jaccard 0.52 — a known, accepted finding rather than a silent gap.

---

## 9. Open questions

**Closed 2026-09-01 by measurement** ([audit S7](review/audit-generation-runtime-specs.md)).

1. ~~One commander effect per creature, or several?~~ ✅ **CLOSED — exactly one.** Generated **three for
   the same creature** at temperature 0.9:

   ```
   1. Phalanx Shell (阵列外壳法则)
   2. 坚实防线 (Stalwart Phalanx)
   3. Phalanx Shell (方阵护壳)
   ```

   **Two of three produced literally the same name.** Pairwise character-overlap Jaccard 0.42 /
   0.68 / 0.45, **mean 0.52** — synonyms, not alternatives. This is audit **A1's thesaurus failure**
   reproduced at single-creature scale. "Several" would need a differentiation rule that does not
   exist, so **one per creature**, and the boundary in §7 is now a hard rule rather than an assumption.
