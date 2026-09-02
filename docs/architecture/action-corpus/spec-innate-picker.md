# Spec: innate-picker (A-S6)

**Module id:** `innate-picker` · **Program:** [action-corpus](../action-corpus-map.md) §4 · **Build order:** 7 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none — permanently, not provisionally.** The escape hatch that made this "model-free for
now" was struck.

It owns one decision per species: which of that species' eligible actions is promoted to
`ActionKind.Innate`. **The innate is a free sixth slot outside `LoadoutSet.MaxSize = 5`**
(`LoadoutSet.cs:40`), so choosing it decides *how much power is free* — a magnitude, in the exact sense
Law 2 uses the word, and therefore out of a model's reach. By the time this module runs, every
candidate's identity has already been written by A-P2/A-P3; there is nothing left to author, only a
ranking to apply.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. **A cell is a target, never an identity** — so `elementMatch` below compares a
   *seed's* declared element affinity against the species' catalog element, never a rolled channel.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan**. This module
   costs zero calls and must produce a correct answer over a smoke batch — Checkpoint 5 is exactly that.
3. **The roster is 84 species, not 904.** So this module emits at most 84 picks, and a species with no
   eligible action gets `null` rather than a fabricated one.
4. **C1's family-access widening is gated.** So a family-tier candidate and a signature-tier candidate
   may draw from the same atom families, which makes the ranking's later terms load-bearing rather than
   decorative.

## 1. What exists today — every seam this module needs is built

| Thing | Evidence |
|---|---|
| The innate is stored **per species**, and is **nullable** | `ActionRow.cs:87` — `SpeciesBasicsRow(SpeciesKey, Attack, Guard, Move, string? InnateActionId)` |
| It is **validated** — must exist, and must be `kind = innate` | `ActionValidator.cs:107-115` |
| It is **assembled** into the intrinsic set, only when non-empty | `ActionSetAssembler.cs:60-61` |
| It is **persisted**, with `DBNull` for absence | `RpgStore.Actions.cs:542-549`; column at `:98` |
| `ActionKind.Innate` exists and is one of three | `ActionEnums.cs:10-15` |
| An innate is **never bound** — putting one in the equipped set is a category error, not a wasted slot | `LoadoutSet.cs:74` (`IntrinsicNotEquippable`) |
| The scarce thing it bypasses is **5 slots** | `LoadoutSet.cs:40` |
| The five categories in declared order — the tie order for the lean | `ActionEnums.cs:119-123` |
| Six elements, with `ElementPrimary`/`ElementSecondary` per species for all 84 | `ActorElementTypes.cs:3-11`; `DemonSpeciesCatalog.Generated.cs:14+` |
| Rung table with `cap: 10` | `data/tuning/action-rungs.v1.json` |

**One overclaim corrected, because the conclusion must not rest on it.** An earlier argument filed
*"the innate climbs with earn history"* under verified. It is **not built**: the innate's rung is the
authored `ActionRow.Rung` column (`ActionRow.cs:23`), and `UnlockLadder.Rung` is reachable only through
a held unlock, which an innate never is (`UnlockLadder.cs:56-61`). **The model-free conclusion survives
on the free-sixth-slot half alone**, which is independently verified above.

### Real gap

There is no picker and no per-species innate content — 84 picks, which is what the corpus is for.

## 2. Inputs and outputs

**Reads:** the accepted corpus (A-S3 survivors plus everything already accepted) ·
`role-lean.json` (A-S0) · `DemonSpeciesCatalog.Generated.cs` for element ·
`data/tuning/action-innate-picker.v1.json` (**new** — the five term multipliers, per-mille).

**Writes** `data/seed/actions/species-innate.json`, `kind: "action-innate"`, in the A-C1 envelope:

```jsonc
{
  "schemaVersion": 1, "kind": "action-innate",
  "_meta": { "partition": "innate", "corpusHash": "...", "tuningVersion": 1 },
  "entries": [
    { "id": "innate.cherrybomb", "speciesKey": "cherrybomb",
      "innateActionId": "action.species.cherrybomb.002",
      "terms": { "roleLeanMatch": 5, "motifCoverage": 2, "elementMatch": 2,
                 "categoryScarcity": 3, "rungCeiling": 7 },
      "score": 5312000, "runnerUp": "action.family.cherry.001", "eligibleCount": 4 },
    { "id": "innate.marigold", "speciesKey": "marigold",
      "innateActionId": null, "reason": "no eligible action", "eligibleCount": 0 }
  ]
}
```

It also emits the promotion list — the accepted seeds whose `kindHint` becomes `innate` — so A-C1's
corpus stays the single source and the promotion is diffable.

## 3. The algorithm

### 3.1 Eligibility

An action is eligible for species `S` iff:

- `scope == "species"` and `scopeKey == S`, **or** `scope == "family"` and `scopeKey == family(S)`; and
- it is not already promoted for another species; and
- its `kindHint` is not `basic` (a basic occupies one of the three named slots, `ActionRow.cs:87`).

**A `general`-scoped action is never eligible.** The innate is the species signature; a shared floor
row cannot be one.

### 3.2 The ranking tuple

```text
rank = (roleLeanMatch, motifCoverage, elementMatch, categoryScarcity, -rungCeiling)
tie-break: byte-wise ordinal on actionId
```

Term by term, each an integer with a stated maximum so the positional weights below are derivable:

| Term | Definition | Range |
|---|---|---|
| `roleLeanMatch` | `5 - index` of the action's `category` in the species' `leanOrder` from A-S0. **A species whose A-S0 `leanSource` is `floor` — a genuine five-way tie — scores 0 for every candidate**, so the term goes inert and the next one decides: an absence, never an invented preference. ⛔ **CORRECTED 2026-09-03 (review F12):** the trigger was *"`separation == 0`, no family"*, which would have made this term inert for **31 of 84** species that now carry a real derivation (`spec-characteristic-pool.md` §3 step 3). A family-less species has `separation: null` and a derived `leanOrder`, and this term reads it normally | 0..5 |
| `motifCoverage` | how many of the species' `motifs` appear in the action's recorded `motifsUsed`. Read from what the generator recorded against the brief's anchor, **never re-derived by matching prose** | 0..len(motifs) |
| `elementMatch` | 2 if the action's element affinity equals `ElementPrimary`; 1 if it equals `ElementSecondary`; 0 otherwise | 0..2 |
| `categoryScarcity` | `eligibleCount - (count of eligible actions sharing this action's category)`. Scarcer inside the species' own set ranks higher | 0..eligibleCount-1 |
| `-rungCeiling` | the **negated** upper bound of the action's `rungBand`. Lower ceiling wins, because the innate is a permanent grant outside the budget that prices every other action, and it must not be the biggest thing the species owns | -10..-1 |

### 3.3 Making the tuple tunable without making it float

Lexicographic order is the shipped default; the **weights are the tunable**, because a balance pass
will absolutely want to change how much role-lean match outweighs motif coverage.

For one species, with `M_t` the observed maximum of term `t` over that species' own eligible set:

```text
base_5 = 1
base_t = base_{t+1} * (M_{t+1} + 1)          # each term outranks everything below it
score  = Σ_t ( (long)base_t * (term_t + offset_t) * w_t ) / 1000     # ONE division, last
```

- `offset_t` shifts `-rungCeiling` into non-negative territory (`+cap`), nothing else.
- `w_t` are per-mille multipliers from `data/tuning/action-innate-picker.v1.json`, **defaulting to
  1000**, at which the score reproduces the lexicographic tuple exactly.
- Maxima are observed per species rather than fixed, so the scheme stays exact with no cap on the
  eligible count — there is no progression ceiling here to remove.
- `long` throughout, **widen before multiplying** (`(long)base_t * term`, never `(long)(base_t * term)`),
  divide by 1000 **last, exactly once**, and let overflow **throw** rather than wrap. Never `float`.

### 3.4 The pick

1. Enumerate species in catalog order (`DemonSpeciesCatalog.Generated.cs`), lowercased key.
2. Collect the eligible set, sorted byte-wise on `actionId` — a total order, so the result cannot depend
   on enumeration order.
3. Score each, take the maximum; **ties break on the `actionId` ordinal**, ascending.
4. Empty eligible set → `innateActionId: null` with `reason: "no eligible action"`. `InnateActionId` is
   already nullable and already validated for exactly this, so absence is a legal recorded state, never
   a fabricated pick.
5. Emit the pick, the five terms, the score, the runner-up and the eligible count — so a reviewer can
   see *why*, and so a re-tune can be argued against numbers rather than vibes.
6. Emit the promotion: the picked seed's `kindHint` becomes `innate`, which is what
   `ActionValidator.cs:107-115` will later check.
6b. **⛔ Promotion is a MOVE, not a copy — added 2026-09-03 (review F14).** This module writes the
   committed corpus, and A-S3 writes its survivors with the **same ids**. `Corpus.load` walks
   `sorted(root.rglob("*.json"))` — the whole tree (`corpus/model.py:170`) — and `Corpus.add`
   raises `CorpusLoadError` on a duplicate real id (`corpus/model.py:92-101`), so **a duplicate was
   structurally guaranteed** and no spec named the step that avoids it. The step, in three parts, all
   of them this module's:

   - Every seed this module commits **leaves** `data/seed/actions/_rounds/round-<n>/survivors.json`.
     The round file keeps the id only as a `promoted` marker — `{"id": "…", "promoted": true}` —
     never as a second full row. **One id exists in exactly one place.**
   - The committed corpus is written at the seed root; the round tree is under the declared `_rounds/`
     prefix that `spec-corpus-loader.md` §3 step 2b excludes from a committed load. The exclusion and
     the move are **belt and braces on purpose**: the exclusion keeps a mid-run tree loadable, the
     move keeps the committed tree honest.
   - A seed that is **not** promoted moves the same way — it is committed unchanged, without the
     `innate` `kindHint`. "Retire" is not a third state: a candidate A-S3 rejected never reaches this
     module, and a rejected row lives only in `rejects.json`.
7. Canonical write — sorted keys, fixed indent, `\n`, explicit nulls.

## 4. What it must NOT do

- **Never call a model.** Permanently. A model here would make the final committed artifact
  non-reproducible — the exact defect this repo has already shipped once, where a generator rewrote all
  84 entries every run and only a byte-comparison found it.
- **Never pick a `general`-scoped action**, and never pick one already promoted for another species.
- **Never fabricate a pick.** No eligible action means `null`.
- **Never put a weight in code.** All five `w_t` are rows in `data/tuning/action-innate-picker.v1.json`.
- **Never use `float` or `double`.** Every term and the score are `long`.
- **Never introduce a second rung curve.** The picker reads `rungBand`; it does not shift, lag or scale
  a rung. A lagging climb was already rejected as *"a third curve for a small gain"*.
- Never run before A-S3. It writes the committed corpus, and running it on unfiltered candidates would
  promote a row that dedup later rejects.
- Never re-derive `motifCoverage` from prose. It reads what the generator recorded.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism (Checkpoint 5)** | same accepted corpus in, byte-identical `species-innate.json` out, asserted by hash; shuffling the candidate order changes nothing |
| **Planted violation — five-way tie** | two candidates identical on all five terms: the lower `actionId` ordinal wins, asserted, and the test fails if the result tracks input order |
| **Planted violation — general leaks in** | a `general`-scoped action planted into the eligible set is **refused**, naming the scope |
| **Planted violation — empty set** | a species with zero eligible actions gets `null` and a reason; a fabricated pick fails the test |
| **Planted violation — a weight in code** | a bare numeric multiplier in the module source is caught by `python scripts/audit-magic-numbers.py`, which must report zero targets for this module |
| **Uniform floor** | a species whose A-S0 `leanSource` is `floor` scores 0 on `roleLeanMatch` for every candidate, and the pick is decided by `motifCoverage` onward. **A family-less species is NOT that case** — a test asserts one of the 31 gets a non-zero `roleLeanMatch` spread across its candidates (review F12) |
| **Weight default** | with every `w_t = 1000` the score ordering equals the lexicographic tuple ordering, over a generated set of candidate permutations |
| **Overflow** | a synthetic species with a large eligible set and maximal terms does not overflow `long`; a forced overflow **throws** |
| **Validator round trip** | the emitted picks pass `ActionValidator`'s innate check (`ActionValidator.cs:107-115`) — the picked id exists and is `kind = innate` |
| **Planted violation — duplicate after promotion (F14)** | a tree holding a round survivor and its committed twin loads through `Corpus.load` with **no** `CorpusLoadError`; the test fails if the round file still carries the full row rather than the `promoted` marker |
| **Offline guarantee** | the suite passes with the transport stubbed to raise |

## 6. Acceptance criteria

1. `species-innate.json` is written through A-C1's envelope, loads back, and has at most one entry per
   catalog species.
2. Every non-null pick is a `species`- or `family`-scoped action whose `scopeKey` matches that species
   or its family; no `general` pick exists.
3. Every species with an empty eligible set has `innateActionId: null` and a stated reason.
4. Two candidates tied on all five terms resolve on the `actionId` ordinal, asserted by a planted pair.
5. The same input produces byte-identical output, and the output is independent of candidate ordering.
6. All five term multipliers live in `data/tuning/action-innate-picker.v1.json`; the magic-number audit
   reports zero targets for this module.
7. Every arithmetic path is `long`, widened before multiplying, divided by 1000 once; no `float` or
   `double` appears in the module.
8. Each entry records its five terms, its score, the runner-up and the eligible count, so a pick can be
   argued with rather than trusted.
9. Every promoted seed passes `ActionValidator`'s innate check.
9b. Every committed seed has **left** its round file (§3.4 step 6b), which keeps only a `promoted`
   marker — so a `Corpus.load` over the whole seed root raises no duplicate id
   (`corpus/model.py:92-101`).
10. Zero model calls, proven by a stub that raises.

## 7. Dependencies

**Depends on:** **A-S3** (map §4 and §5 — the picker runs on the accepted corpus), **A-S0** (the role
lean supplies term 1), and A-C1's envelope.
**Depended on by:** the committed corpus under `data/seed/actions/`, and downstream, the action
program's `SpeciesBasicsRow` population.
**Cross-program (map §7):** none blocking. Every seam it needs — column, validation, assembly,
persistence — is already built; the only gap is content.
