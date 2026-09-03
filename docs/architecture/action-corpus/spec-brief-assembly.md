# Spec: brief-assembly (A-S2)

**Status: DRAFTED 2026-09-03** — owner decision, in answer to *"`familyActions` has no producer"*:
**a new module owns P3-brief assembly.** Module **A-S2**, action-corpus. Depends on **A-S1**, **A-S3**.

**What it owns: the one brief that cannot be assembled before a model has run.** A-S1 builds every brief
from static inputs, in Phase 3, with no tokens spent. **A-P3's brief is different — it carries
`familyActions`, which does not exist until A-P2's round has been generated, validated, deduped and
assigned ids.** No module owned that step, so A-P3 raised on 100% of its input.

---

## 1. Why this exists — F15, recurring one field over

The adversarial review closed **F15**: family *motifs* had no producer. A-P2 said *"A-S1 owns it"*;
A-S1 never mentioned it; **A-P2's acceptance criterion therefore rejected 100% of A-S1's output.** The
review's own words were *"ownership passed in a circle."*

**The identical defect was live one field over and was not caught.** `spec-signature-propose.md`:

> *"A brief whose **`familyActions` key is absent** raises; a brief whose `familyActions` is an empty
> list is legal."*

**A-S1's brief schema has no `familyActions` key.** Its single mention of the field is a
cross-reference to the *discipline* (*"the same absent-versus-empty discipline A-P3 applies"*), not an
emission.

**And structurally A-S1 could never have filled it.** A-S1 is a Phase-3, model-free planner that runs
**before any A-P2 output exists**. Asking it to carry `familyActions` would ask a deterministic stage to
contain the result of a model stage that has not run.

**So this is not a missing line in A-S1. It is a missing stage.**

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| A-S1 assembles briefs from static inputs | **specced** | `spec-distribution-planner.md` |
| A-P3 requires `familyActions` and raises on its absence | **specced** | `spec-signature-propose.md` |
| Anything that emits `familyActions` | ⛔ **does not exist** | grep across all 17 specs: A-P3's own requirement, plus two cross-references that borrow the discipline without producing the field |
| A-P2's output is id-assigned before A-S3 | **specced** | ids are minted downstream; A-P2 emits none |

**Sorted: real gap.** Not inert wiring — the stage was never named.

---

## 3. The contract

### 3.1 What it reads

- **A-S1's plan** — quotas, rung windows, family-access sets, the anchor. Everything a P3 brief shares
  with a P1/P2 brief comes from there **unchanged**; this module does not re-derive it.
- **A-P2's accepted round** — after `A-S4` validation and `A-S3` dedup, **with ids assigned**.

**That ordering is the whole point.** A-P3 must differ from its family's *shipped* siblings, not from
raw proposals that may be rejected minutes later. Assembling from unaccepted output would make P3's
differentiation judgement about content that never exists.

### 3.2 What it emits

One brief per signature action, identical to A-S1's shape plus:

```jsonc
"familyActions": [
  { "actionId": "...", "name": "...", "atomFamilies": ["...", "..."], "fingerprint": "..." }
]
```

- **Sorted by `actionId`, ordinal.** A-P3 inlines this into its prompt; **an unsorted list makes the run
  order-dependent** and replay undefinable.
- **Absent versus empty is preserved.** A missing key is a defect and raises. **An empty list is legal
  and means "this species has no family"** — true for 31 of the 84 species, so it is the common case,
  not an edge case.
- `fingerprint` is A-S3's, carried through rather than recomputed — one definition, one owner.

### 3.3 The species with no family

31 of 84 have no family assignment. For those the brief carries `"familyActions": []` **and A-P3 runs
normally**, rendering its explicit no-family sentence. **This module must not skip them** — a signature
action for a family-less species is exactly as legitimate as any other, and skipping would silently drop
37% of the roster.

---

## 4. What this module must NOT do

- **Call a model.** It assembles a brief; it makes no judgement. **Model-free.**
- **Re-derive anything A-S1 owns.** Quotas, windows, anchors and access sets pass through untouched. Two
  derivations of one number is how they drift.
- **Assemble from unaccepted A-P2 output.** §3.1.
- **Invent an id.** Ids come from the accepted round.
- **Skip family-less species.** §3.3.
- **Emit an unsorted `familyActions`.** §3.2.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | Every emitted brief carries a `familyActions` **key** | The defect this module exists for |
| 2 | A family-less species gets `[]` — **present and empty**, and its brief is still emitted | §3.3, and 31 of 84 depend on it |
| 3 | `familyActions` is **sorted ordinally by `actionId`**, asserted across two runs | Replay is definable |
| 4 | The list contains **only accepted, deduped, id-assigned** actions — a rejected A-P2 proposal never appears | §3.1 |
| 5 | Every non-`familyActions` field is **byte-identical** to what A-S1 produced | No re-derivation |
| 6 | **Planted violation:** a brief missing the key **fails**, and A-P3's raise is exercised end to end | The absent-vs-empty contract is real on both sides |
| 7 | **Planted violation:** assembling from unaccepted output **fails** | §3.1 mechanically |
| 8 | Tests never call a model — the transport is stubbed so it **raises** | The repo law |

---

## 6. Acceptance criteria

1. Every A-P3 brief carries `familyActions`, present in all cases.
2. Family-less species get `[]` and are **not skipped**.
3. The list is ordinally sorted and stable across runs.
4. Only accepted, deduped, id-assigned actions appear.
5. All other brief fields are byte-identical to A-S1's.
6. Both planted violations fail.
7. **Model-free**, with a stubbed transport that raises.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **A-S1** | Supplies everything except `familyActions`. **This module never re-derives its numbers** |
| **A-S3** | Supplies the accepted, deduped, id-assigned round and the `fingerprint` definition |
| **A-P2** | Its output is the input, **after** A-S4 and A-S3 |
| **A-P3** | The consumer. Its dependency changes from *"A-P2"* to **A-S2** |
| **Phase placement** | ⚠️ **Phase 4, not Phase 3.** It is model-free but cannot run until a model round has been accepted — the one stage where "model-free" and "runs early" come apart |
| **The lesson** | F15 was closed for motifs and left open for actions, because the fix was applied to the *field* rather than to the *pattern*. **Any brief field whose value is produced by a model stage needs a stage that runs after that model** — worth checking the remaining brief fields against |
