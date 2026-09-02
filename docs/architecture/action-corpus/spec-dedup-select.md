# Spec: dedup-select (A-S3)

**Module id:** `dedup-select` · **Program:** [action-corpus](../action-corpus-map.md) §4 · **Build order:** 5 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none on the acceptance path.** Tiers 1 and 2 are hash sets. Tier 3 may consult an
embedding index and is **advisory only** — it flags a review queue and never rejects, so no acceptance
decision is ever stochastic.

It owns the decision of which candidates survive a round. It is a **pure function over the complete
candidate set**, applied in a fixed order, so the same candidates in give the same survivors out every
time. The rejection loop still exists — it just runs *between* rounds instead of inside one, which is
what keeps the run replayable: otherwise candidate #500's fate depends on #1-499 and *"a rerun over
unchanged inputs is byte-identical"* stops being definable.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. **A cell is a target, never an identity** — so the dedup fingerprint keys on
   atom *families* and never on a resolved cell, tier or element, and two candidates cannot be made
   distinct by a cell.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan.** This module
   must run identically over 12 candidates and over 3,000, and its report is part of the smoke batch's
   evidence.
3. **The roster is 84 species, not 904.** So the t2 "same anchor" partition has at most 84 species keys
   and 19 family keys, and the expected corpus is roughly 850 rows, not 3,307. Sizing claims are
   re-derived against the shipped roster, never inherited.
4. **C1's family-access widening is gated.** So a tier's atom-family set is currently the *same* set
   for every tier, which means **cross-tier near-duplicates are more likely, not less** — this module
   is the thing that has to notice, and its report must not read that as a generation defect.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `Corpus.load` and the entry graph the candidate set is expressed in | `corpus/model.py:158-200` |
| A duplicate real id already raises rather than resolving last-write-wins | `corpus/model.py:96-101` |
| Existing dedup and constraint metrics to model the finding shape on | `seedsmith/metrics/dedup.py`, `metrics/constraint.py` |
| `Finding` / `Loop` / `Severity`, and the rule that an OPEN metric may never gate | `metrics/model.py:26-49`, `:81-99` |
| Provenance with an injected clock, so a rerun is comparable | `pipeline/provenance.py:36-66` |
| The closed field vocabularies the fingerprint is built from | `ActionEnums.cs:10-49`; `ActionTargetSpec.cs:16-47` |

### Real gap

There is no dedup for actions, no fingerprint definition, and no review queue. Tier 3's index is a
genuine dependency decision — seedsmith's baseline is exact pins, a lockfile, an isolated venv and an
offline assert, so adding an embedding model is taken on its own merits.

## 2. Inputs and outputs

**Reads:** the complete candidate set for one round (A-S4's accepted output, in the A-C1 `action-seed`
envelope) · the already-accepted corpus under `data/seed/actions/` · `data/tuning/action-dedup.v1.json`
(**new** — the t3 threshold, k, and the t2 field-distance rule).

**Writes**, all through A-C1's envelope:

| Path | `kind` | Content |
|---|---|---|
| `data/seed/actions/_rounds/round-<n>/survivors.json` | `action-seed` | the accepted seeds, in fixed order |
| `data/seed/actions/_rounds/round-<n>/rejects.json` | `action-reject` | `{id, tier, reason, collidedWith}` |
| `data/seed/actions/_rounds/round-<n>/review-queue.json` | `action-review` | tier-3 flags — advisory, never a rejection |

**⛔ CORRECTED 2026-09-03 (review F14) — the round root moved under `_rounds/`, and the promotion is
a MOVE.** These files used to be written at `data/seed/actions/round-<n>/`, and A-S6 writes the
committed corpus with the **same ids under the same root**. `Corpus.load` walks
`sorted(root.rglob("*.json"))` — the whole tree (`corpus/model.py:170`) — and `Corpus.add` raises
`CorpusLoadError` on a duplicate real id (`corpus/model.py:92-101`), so **a duplicate was structurally
guaranteed the moment A-S6 promoted**, and no spec named a move, retire or exclusion step. It is named
now, in three places that must agree:

- **Here:** survivors are written under `data/seed/actions/_rounds/round-<n>/`. The leading underscore
  follows the convention `_exemplars/` already uses (`corpus/model.py:188`).
- **`spec-corpus-loader.md` §3 step 2b:** `_rounds/` is a declared prefix, listed in `_manifest.json`
  and **excluded from the committed-corpus load**. Reading a round is a separate, explicit
  `Corpus.load(root / "_rounds" / f"round-{n}")`.
- **`spec-innate-picker.md` §3.4:** promotion **moves** the seed out of `_rounds/round-<n>/` into the
  committed corpus under the same id; the round file keeps the id only as a `promoted` marker, never
  as a second row. One id exists in exactly one place, so the duplicate raise stays a signal about
  real content rather than a scheduling artefact.

**The fingerprint**, canonical and total — **this definition is the program's only one**, and A-S1
§3 step 8 renders `avoidNeighbours` by quoting it rather than restating it:

```text
fingerprint = sorted(atomFamilies) | category | targetMode | areaShape | relation
            | sorted(structureAxes) | pairingRole
```

`areaShape` is the literal `none` when `targetMode != "area"` — a missing key is a defect, `none` is
a value. Every component is a **wire string**: `"attack"`, `"area"`, `"row"`, `"enemy"`
(`ActionCategories.Name` `ActionEnums.cs:96-104`; `ActionTargetModes.Name` /
`ActionAreaShapes.Name` `ActionTargetSpec.cs:103-112,134-141`; `RelationKinds.Name`
`RelationKind.cs:23-26`). `pairingRole` is `enabler | payoff | none`.

**⛔ CORRECTED 2026-09-03 (review, four-names finding).** `sortedAtomFamilies` was written as though
it were a field. It is not: `atomFamilies` is the seed's canonical field (A-S1 §3 step 8's table), and
`sorted(...)` is this fingerprint's byte-wise **rendering** of it. `atomPools` and
`allowedAtomFamilies` are, respectively, the old name for the same field and the *brief's permitted
set*, which is a different thing.

**⛔ Why `targetMode`/`areaShape` may be hashed at all — decided 2026-09-03 (review F5).** Hashing
them as mechanical identity only holds if they are part of the **seed**. They are: A-S1 §3 step 4a
authors them, and `ActionRow.Targeting` (`ActionRow.cs:40`) is the shipped authored field they bind
to. `ActionSeeder.cs:55`'s `WeightedChoice.Pick` is the runtime generator's own roll over a
caller-supplied pool (`:37`) on a path a corpus action does not take, so nothing re-rolls a hashed
field and no second roll is designed.

**`pairingRole` in the fingerprint keys on an ATOM FAMILY, never a status** — `pairings.json` maps
`atom.chill-punisher`/`atom.rot-punisher` to enabler atom families, and
`EnablerPayoffPairings.IsPayoff(string atomFamily)` (`EnablerPayoffPairings.cs:26`) takes families
throughout. Because the table has only two payoff keys, `pairingRole` is `none` for most candidates,
and a fingerprint component that is nearly constant **carries nearly no separating power** — stated
here rather than discovered when tier-1 collision rates come back high (A-S1 §3 step 6).

## 3. The algorithm

1. **Order the candidate set totally, before anything else.** Sort by
   `(scopeRank, scopeKey ordinal, briefId ordinal, candidateId ordinal)` where `scopeRank` is
   `general < family < species` and every ordinal comparison is byte-wise (`StringComparer.Ordinal`'s
   Python equivalent — plain `str` comparison, never locale-aware). **Every later step walks this
   order**, so no result can depend on dict or filesystem iteration.
2. **Tier 1 — mechanical identity. Hard reject.** Build a hash set of fingerprints over the accepted
   corpus, then walk the ordered candidates. An exact fingerprint match rejects with
   `tier: 1, reason: "identical fingerprint"`, naming the row it collided with. The first candidate in
   the fixed order wins; every later one is the reject. Cost: a hash set. Free.
3. **Tier 2 — near-duplicate, one field apart. Hard reject, but only within an anchor.** For each
   `(scope, scopeKey)` partition, build a per-anchor hash set. A candidate whose fingerprint matches an
   accepted one **modulo exactly one field** is rejected. **Across different anchors it is allowed** —
   a fire species and an ice species may both have "burst damage down a row", and should.
   Implementation is a hash set per field-masked projection, not an O(n²) pairwise scan.
4. **Tier 3 — semantic. Advisory only.** If an index is configured, build it **from this round's own
   candidates**, query it as a pure function of that set, and **discard it**. Similarity above the
   tuning threshold writes a `review-queue.json` row and **never changes the survivor set**. Prose
   similarity is a weak proxy in both directions — two actions can read alike and play differently, or
   read differently and be identical — so making it a hard gate would both reject genuine content and
   put a stochastic component inside an acceptance decision.
5. **Provenance.** Each round records the corpus hash, the candidate-set hash, the tuning version, and
   — when tier 3 ran — the embedding model id from `.env` and its version. Without that, a rerun cannot
   prove it saw the same neighbours.
6. **Canonical write** — sorted keys, fixed indent, `\n`, explicit nulls, CJK unescaped.

**The `--no-semantic` switch is a first-class mode, not a debug flag.** With tier 3 off the survivors
must be byte-identical to a run with it on; that equality is the mechanical proof that tier 3 is
advisory.

## 4. What it must NOT do

- **Never auto-reject on tier 3.** An advisory tier that quietly gates is how a non-reproducible run
  gets built by accident.
- **Never query a live mutable store mid-generation.** The index is built from the round's candidates
  and discarded; anything else reintroduces the order-dependence this module exists to remove.
- **Never call a generation model.** The optional embedding call is not on the acceptance path, and in
  tests the transport is stubbed to **raise** so "makes no call" is provable.
- Never let acceptance depend on enumeration order, a timestamp, a clock, or a set's iteration order.
- Never reject a cross-anchor tier-2 match. That is content the design wants.
- Never dedup on a cell, tier or element. Constraint 1.
- Never write the committed corpus's `kind: innate` promotion — that is [A-S6](spec-innate-picker.md)'s.
- **Never write outside `data/seed/actions/_rounds/round-<n>/`.** The committed corpus is A-S6's, and
  writing a survivor beside it is what guarantees the duplicate-id raise (§2's F14 note).
- Never treat a tier-2 collision rate as a generation defect while constraint 4 keeps every tier on the
  same atom-family set. Report the rate; do not diagnose it.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | the same candidate set, shuffled into a different input order, produces byte-identical `survivors.json`, `rejects.json` and hashes |
| **Purity** | running the same round twice writes identical bytes; no clock, no random seed, no network |
| **Planted violation — t1** | two candidates with identical fingerprints: exactly one survives, the reject names the survivor, and which one survives is decided by the fixed order, not by input order |
| **Planted violation — t2 within an anchor** | two same-species candidates differing in exactly one field: the later is rejected with `tier: 2` |
| **Planted violation — t2 across anchors** | the same one-field-apart pair on two *different* species: **both survive**, and the test fails if either is rejected |
| **Planted violation — t3 tries to gate** | a stubbed index returning similarity 1.0 for every pair changes **no** survivor; only the review queue grows |
| **`--no-semantic` equivalence** | survivors are byte-identical with tier 3 on and off, over the same candidate set |
| **`areaShape` absence** | a candidate with `targetMode != "area"` and no `areaShape` key is refused; the literal `none` is required |
| **Casing** | every fingerprint component round-trips through the matching `TryParse`; a candidate carrying `"Area"`/`"Row"`/`"Enemy"` is refused before it is hashed |
| **Round isolation (F14)** | survivors land under `_rounds/round-<n>/`; a committed-corpus load over a tree containing both a survivor and its A-S6-promoted twin raises **no** `CorpusLoadError` |
| **Offline guarantee** | the suite passes with the transport stubbed to raise; tier-3 tests inject a fixed vector function instead |

## 6. Acceptance criteria

1. Survivors, rejects and the review queue are written through A-C1's envelope under
   `data/seed/actions/_rounds/round-<n>/`, and load back through an explicit round load.
1b. A committed-corpus load over a tree holding both a round survivor and its promoted twin raises no
   duplicate-id error — the move step of §2's F14 note, asserted rather than assumed.
2. Every reject carries a tier, a reason, and the id of the row it collided with.
3. Shuffling the candidate input order changes nothing in any output file, proven by hash.
4. Tier 2 rejects within an anchor and never across anchors, asserted by a planted pair of each.
5. Tier 3 never changes the survivor set, asserted with an index stubbed to maximum similarity.
6. `--no-semantic` and the full run produce byte-identical survivors.
7. Provenance records the corpus hash, candidate-set hash, tuning version and, when tier 3 ran, the
   embedding model id and version.
8. A rerun over unchanged inputs is byte-identical by hash.
9. Zero model calls on the acceptance path, proven by a stub that raises.

## 7. Dependencies

**Depends on:** **A-S4** (`validate-heal`) for its candidate set (map §4 and §5), and A-C1's envelope
for every file it writes. Its fingerprint fields come from A-S1's brief slot, so the two schemas move
together.
**Depended on by:** **A-S5** (coverage over the accepted corpus) and **A-S6** (the innate picker runs
after dedup and writes the committed corpus).
**Cross-program (map §7):** the tier-3 index is a real dependency decision against seedsmith's locked
`dependency-baseline`; because tier 3 never blocks, it can be built last or replaced by a cheaper
token-overlap heuristic without changing anything above.
