# Spec: `family-extract`

Module `family-extract` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D2**.
Depends on `adapter-demons`, `pipeline`.

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: APPROVED by the owner 2026-08-31. Authorized to build.**

---

## 1. Objective

Extract **candidate family labels** for each demon from its name and description.

Owner, 2026-08-31: *"family don't exist, we only crawling it from almanac natural language text, need
a classify llm pipeline… a demon maybe have some family."*

This is the feature's first pipeline and a **different shape from anything seedsmith has built**:
every existing pipeline *generates* content; this one *reads text and proposes labels*. It does not
decide the taxonomy — `family-consolidate` does. Keeping those apart is A6's finding, and the reason
is determinism, not tidiness.

**Done means:** every demon has zero or more candidate labels, each recording **what it was derived
from**, and no label has been merged, renamed or promoted to a vocabulary yet.

---

## 2. Design

### 2.1 Open vocabulary, and the tension that creates

Every other seedsmith brief **inlines a closed vocabulary literally** — the rule that exists because
*"tags come from `tags.v1.json`"* cost 51 invented tags. Here there is no vocabulary to inline: the
family set is exactly what this pipeline is trying to discover.

That is not a licence to skip the rule, it is a reason to be careful about what *else* gets inlined.
The brief carries, in full: the demon's own name and description, the **sibling demons in the same
batch** (§2.2), and the per-kind expression rules. It cites nothing.

### 2.2 Batched extraction — the design decision that matters most

**Extracting one demon at a time produces a unique label per demon.** A model shown "Wall-nut" alone
and asked for its family will answer "wall-nut family"; shown "Tall-nut" alone it answers "tall-nut
family". The result is 24 families of one member each — a taxonomy with zero sharing, which is
exactly the failure §3.1 and Q7 of the ideal exist to prevent, arriving before consolidation ever
runs.

So extraction is **batched**, and the batch is where sharing becomes possible: shown Wall-nut,
Tall-nut and Giant Wall-nut together, a model proposes one label for all three.

Batching must not introduce order dependence:

- Batches are formed by **sorting on `speciesId`** and taking fixed-size windows. Same corpus ⇒ same
  batches, always.
- Batch size is a **structural constant with a comment**, not a tunable: it trades context against
  the number of calls, and changing it changes which demons see each other — a balance pass would
  never touch it, but it is not arbitrary either.
- A demon appears in exactly one batch. Overlapping windows would let the same demon receive
  different labels from different contexts, which is a merge problem invented for no gain.

### 2.2a ✅ Label language — RESOLVED (owner, 2026-08-31): both, and the pair is the artifact

**The roster's display names are Chinese** — `钻石套娃僵尸`, `黄金套娃僵尸`, checked in
`DemonSpeciesCatalog.Generated.cs`. `family-consolidate`'s central merge rule is **head-noun
extraction**, which is English-shaped: over those tokens it is not inaccurate, it is *undefined*.
Audit S7 raised this as a blocker on the next module's core algorithm.

**The decision: every candidate carries both `label` and `nativeLabel`.**

| Field | Language | Role |
|---|---|---|
| `label` | English, kebab-case | what `family-consolidate` merges on — head-noun extraction works as specced, unchanged |
| `nativeLabel` | as read from the source text | what the model actually saw; preserved for display, `lore-enrich`, and audit |

**Why the pair rather than either alone.** English-only makes the merge work but performs a
translation that no artifact records — the original wording becomes unrecoverable, and nobody can
later check whether `钻石套娃僵尸` really was a "nesting-doll" family or whether a model invented the
reading. Native-only preserves fidelity but leaves `family-consolidate` §2.1 undefined, pushing the
entire merge into a hand-edited synonym map.

Carrying both costs **one string per candidate** and makes the translation *inspectable*, which is
the same reasoning `basis` (§2.3) already rests on: the point is not to be confident, it is to record
what was read so a later reader can disagree.

**Consequence for the schema:** `nativeLabel` is a string and never participates in merging. A
`blocked` candidate carries neither.

### 2.3 Output — candidates, not decisions

Per demon: zero or more `{ label, nativeLabel, basis }` (§2.2a covers the label pair), where `basis`
is the audit's honesty mechanism:

| `basis` | Meaning |
|---|---|
| `text` | proposed from real flavour/description content |
| `name` | proposed from the name pattern only — a prior, not evidence |
| `blocked` | neither was sufficient; this demon has no candidate |

**`blocked` is an answer, not a failure.** G1's rule already requires every schema to carry it, and
here it is load-bearing: a demon with no usable text **must** be allowed to have no family, because an
invented family propagates into every generator that inherits motifs from it — one wrong label
becoming five wrong pieces of content.

`basis` is not decoration. `demon-metrics` uses it to exclude tautological pairs (A2), and
`lore-enrich` uses it to know what to revisit. A pipeline that emitted labels without it would look
identical and be unusable.

### 2.4 Non-determinism is admitted, then contained

A model call is not reproducible. seedsmith's answer is already built: the output is
**content-addressed and recorded** (`briefkit` hashes inputs; G2 records `promptVersion` and the
finding closed). Re-running with the same brief and prompt version is expected to produce the same
*decision*, but is not guaranteed to — so **the recorded extraction is the artifact**, and everything
downstream reads that record rather than re-running.

This is why consolidation is a separate module: it must be reproducible over a fixed input, and that
input is this module's committed output.

### 2.5 Schema guardrails

`pipeline`'s existing audits apply unchanged, and two of them matter here:

- **No numeric field** — `audit_schema` rejects one mechanically. Nothing about a family is a
  magnitude.
- **No verdict field** — this schema carries `label` and `basis`, never a confidence *score*. A score
  is a number (rejected above) and a self-assessment (`audit_open_loop_schema`'s concern). `basis`
  is a categorical statement of *what was read*, which is checkable; "confidence 0.8" is not.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_family_extract.py -q
python -m seedsmith family extract --dry-run     # briefs only, no model calls
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/family/
    extract.py        → batching, brief assembly, schema, gate
    schema.py         → the extraction JSON schema (audited by pipeline.model)
tools/seedsmith/tests/test_family_extract.py
data/seed/demons/_generated/family-candidates.json   → recorded output, committed
```

---

## 5. Code style

Match `pipeline/run.py`: a pure function for anything decidable without a model (batching, brief
assembly, verification), and the model call behind the injected seam so tests never reach the
network. `MockModelServer` is reused from the existing suite, never re-rolled.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Batching over the same corpus, run repeatedly | **identical batches**, byte for byte |
| A demon with rich description | candidate with `basis = "text"` |
| A demon with only a name | candidate with `basis = "name"` — recorded, not silently promoted |
| A demon with neither | `basis = "blocked"`, **no label**, and it is not a failure |
| Three sibling demons in one batch | able to receive **one shared label** — asserted against a scripted response, proving the batch is actually presented together |
| Single-demon batching (falsifier) | the same fixture produces three distinct labels — this is what §2.2 exists to prevent, and the test proves the prevention is real rather than assumed |
| A demon with a Chinese name | candidate carries an English `label` **and** the `nativeLabel` it was read from (§2.2a) |
| A `blocked` demon | carries **neither** `label` nor `nativeLabel` |
| `nativeLabel` | never participates in merging — asserted by feeding two candidates that differ only in `nativeLabel` and getting one family |
| A demon receiving two labels from one batch | **both** recorded — multi-membership starts here, not at consolidation |
| Schema | passes `audit_schema` (no numbers) and `audit_open_loop_schema` (no verdict) |
| Brief text | contains no citation-shaped string |
| Model returns a label for a demon not in the batch | rejected, not accepted into the record |

The falsifier row is the one worth keeping: a batching test that only asserts "labels were produced"
would pass with batch size 1, which is precisely the broken configuration.

---

## 7. Boundaries

- **Always:** record `basis` and both labels on every candidate; batch deterministically; treat
  `blocked` as an answer; commit the recorded output.
- **Ask first:** changing batch size (it changes which demons see each other); overlapping batches;
  any schema field beyond `label`/`nativeLabel`/`basis`.
- **Never:** merge, rename or promote labels here — that is `family-consolidate`'s job and mixing
  them hides a non-deterministic step inside a deterministic artifact (A6); emit a confidence score;
  cite a registry by filename.

---

## 8. Success criteria

1. Deterministic batching, proven by repeat runs.
2. Every candidate carries `basis`, `label` and `nativeLabel`; `blocked` demons carry no label at all.
3. Siblings in a batch can share a label — proven by the fixture, and its falsifier.
4. Schema passes both audits.
5. Zero real model calls in the test suite.
6. Output committed and readable by `family-consolidate` with no re-run.

---

## 9. Open questions

**All three are closed (2026-08-31).** Kept with their resolutions because the reasoning is what a
later reader needs, not the fact that a question once existed.

1. ~~Label language.~~ **RESOLVED by the owner — both `label` and `nativeLabel`; see §2.2a.** This
   was audit S7's blocker; the pair keeps `family-consolidate`'s merge rule working *and* makes the
   translation inspectable.
2. ~~Batch size.~~ **DECIDED: 8**, as a structural constant with a comment saying what it trades
   (context against call count) and that changing it changes which demons see each other. It is not a
   tunable — a balance pass would never touch it. Revisited against the first real run, not guessed
   further now.
3. ~~More than one candidate label per demon per batch?~~ **DECIDED: allow it.** Families are
   multi-valued by the owner's 2026-08-31 decision, so forbidding it here would make consolidation
   *infer* a second membership that extraction was not permitted to state — inventing exactly what
   `family-consolidate` §2.5 forbids. Multi-membership starts where it is observed.
