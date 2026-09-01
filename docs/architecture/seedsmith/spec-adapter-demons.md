# Spec: `adapter-demons`

Module `adapter-demons` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D1**.
Depends on `corpus`, `demon-corpus-emit`.

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: APPROVED by the owner 2026-08-31. Authorized to build.**

---

## 1. Objective

Teach seedsmith what a demon is — and nothing else. Every module downstream (planner, briefkit,
pipeline, metrics) already works; they simply need an adapter that answers five questions.

**This module is the proof of a claim the program has made since day one.** `seedsmith-map.md` §1:
*"Items are the first feature; the core is feature-agnostic by construction, **because the second
feature must not rewrite it**."* The `_stub` adapter exists to assert that continuously — its own
docstring: *"If the core ever reaches into item concepts, this stops passing."* A real second feature
is the first genuine test.

**Done means:** `StubAdapter`-shaped, `ItemsAdapter`-sized, and **not one line of core code changed
to accommodate it.** If the core needs an edit, that is the finding — record it rather than patch
around it.

### ⚠️ CORRECTED post-build (2026-08-31): the claim above is false, and here is exactly how

§2.7's motif expression rules could not be represented without editing `adapters/base.py`:
`KindSpec` (the core's own dataclass) had no field for them, and `planner/ordering.py` duck-types
only on `.kind`/`.reference_fields`. Per this section's own instruction, the finding is recorded
rather than patched around:

**One core file changed, one field, additive:** `KindSpec` gained
`motif_expression: str | None = None`. `items` and `_stub` are untouched — `test_stub_adapter.py`
and the full `items` suite stayed green throughout, re-run as part of `test_adapter_demons.py`
itself so a future regression here is attributed correctly. Full reasoning for why this option was
chosen over a demons-local dict or abusing `registries()`:
[seedsmith-plan.md](../../../tasks/seedsmith-plan.md) Part 4 §D-F1.

**So the corrected claim is: `StubAdapter`-shaped, `ItemsAdapter`-sized, and exactly one core file
changed — additively, with a default, leaving every existing adapter and test unaffected.** "Not one
line" was the target; it was not what shipped, and that gap is the actual finding this module
produced, not a footnote to hide.

**A second, independent gap this build found and fixed, unrelated to §D-F1:** the first draft
hand-declared `dimensions()`'s `applies_to` for `rarity`/`element` instead of deriving it from real
`KindSpec` field membership the way `items` does. That produced a live, confirmed
`Coverage/PairwiseHole` false positive ("side×rarity: 8 of 8 legal pairs never co-occur") on the
very first `check` run against the emitted corpus — the identical "confidently wrong" trap
`adapters/items/__init__.py` already documents avoiding for its own `class` dimension. Fixed by
using the same `_applies_to()` helper unconditionally; see that file's own comment for the full
account.

---

## 2. Design

### 2.1 The five methods

`SeedAdapter` is a Protocol ([`adapters/base.py`](../../tools/seedsmith/seedsmith/adapters/base.py)):

| Method | Demons answer |
|---|---|
| `kinds()` | `demon`, `aspect`, `commander-effect`, `environment` — §2.2 |
| `dimensions()` | `side`, `rarity`, `element`, `family` — §2.4 |
| `legal_combinations()` | element × rarity legality; the `False` branch must be reachable |
| `registries()` | closed vocabularies, **inlined not cited** — §2.5 |
| `channels()` | **empty, deliberately** — §2.6 |

### 2.2 Kinds

| Kind | Reference fields | Notes |
|---|---|---|
| `demon` | — | The record `demon-corpus-emit` writes. References nothing; everything references it |
| `aspect` | `demonId` | One species yields N aspects. Needs `aspect-scope`, **approved 2026-08-31** |
| `commander-effect` | `demonId` | |
| `environment` | `demonId`, `sectorId` | ⚠️ **Ships as a kind; nothing generates into it in v1** — A7 |

**Items and actions are deliberately absent.** Audit A3: `Corpus.load` is single-root, so a demon
"item" in this corpus would be a *different thing* from a real item — unequippable, and outside the
item corpus's own role/frame/affix rules. A demon is a **theme**; items and actions stay in their own
corpora and reference it (`demon-themes`, wave D4).

`reference_fields` is what `planner.ordering` reads to derive generation order (Kahn + Tarjan), so
declaring `demonId` here is what makes demons generate before everything that references them —
structurally, with no stage label to go stale.

### 2.3 `environment` ships empty, and why that is not dead weight

With no world host, a `sector:`-scoped binding is rejected `ScopeUnsupported`, so environment content
would be flavour nothing reads — and **coverage would report those partitions "covered"**, making the
feature look more finished than it is (A7).

Declaring the kind now costs nothing and keeps the adapter shape stable for when the world host
arrives. **What must not happen is generating into it**, so the kind ships with no demand and its
partitions are excluded from coverage until a consumer exists. That exclusion is a decision with a
reason, and it belongs in code with the reason attached — not as an absence someone later "fixes".

### 2.4 Dimensions, and the one that does not exist yet

`side`, `rarity`, `element` come from the species catalog. **`family` does not exist until D2** —
it is LLM-classified from natural language (`family-extract` → `family-consolidate`).

So `dimensions()` declares `family` but its values are **empty in D1**. That is honest and it is
load-bearing: a `Dimension` with no values means the partition scheme falls back to `side/rarity`
(`demon-corpus-emit` §9 Q1) until the taxonomy lands, rather than the adapter pretending to a
grouping it cannot yet supply.

**`family` is multi-valued** — a demon belongs to zero, one or several (owner, 2026-08-31). Whatever
consumes it must not assume partition counts sum to the roster size (A5).

### 2.5 Registries — inlined, never cited

`RegistrySet.vocabularies` carries the closed sets: `side`, `rarity`, `element`, `deployMode`,
`acquisition`, `variant`, `trait`, and later `family` and `motif`.

**These are inlined into briefs literally, never referenced by filename.** *"Tags come from
`tags.v1.json`"* cost 51 invented tags historically — an agent cannot follow a filename, so it fills
the gap. `briefkit` already enforces this by grepping rendered briefs for citation-shaped text and
refusing on a match; this module's job is to supply values complete enough that inlining is possible.

**Openness is three states, not two**, matching `core.v1.json`'s own precedent:

| State | Fields |
|---|---|
| **Frozen** | `side`, `element`, `deployMode`, `acquisition`, `rarity` — all mirror locked catalogs |
| **Append-only** | `family`, `motif`, `variant`, `trait` — grow over time, **never renumber** |
| **Open** | `flavorInfo` only |

Append-only exists because **position is load-bearing**: a list index feeds derived ids and content
hashes, so reordering silently moves content already generated against it.

### 2.6 `channels()` returns empty, and that is the design

`adapter.channels()` is consumed by exactly one subsystem — `numerics`
([`numerics/model.py:68`](../../tools/seedsmith/seedsmith/numerics/model.py)). Demons have no
magnitudes to resolve: rarity is a band, `Θ` comes from the power ladder, and generated demon content
carries **no numbers at all**.

So the list is empty and `numerics` is inert for this feature. **This makes "never a number"
structural rather than a guardrail** — there is no numeric path to misuse (A4). Stated here because
an empty list looks like an omission, and the next reader's instinct will be to fill it.

### 2.7 Per-kind motif expression rules

Audit A1's fix, and the thing that makes shared motifs produce coherence rather than repetition:
five generators handed `shell, endurance, patience` otherwise produce *Shell of Patience*, *Enduring
Shell*, *Shellfield* — and **every check passes while the corpus is unreadable**.

Each `KindSpec` therefore carries how a motif is *expressed* in that kind:

| Kind | A motif is expressed as |
|---|---|
| `aspect` | a bias — what this element-typing leans toward |
| `commander-effect` | a doctrine — how the squad behaves |
| `environment` | terrain, weather, what the ground does |
| (`theme`, for items/actions in D4) | material and form / tempo and effect shape |

Shared vocabulary, different part of speech. This is data on the kind, not a rule in a prompt, so it
is inlined into every brief for that kind and cannot be forgotten by one generator.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_adapter_demons.py -q
python -m pytest -q                                  # the whole suite must stay green
```

---

## 4. Structure

```
tools/seedsmith/seedsmith/adapters/demons/
    __init__.py      → DemonsAdapter (the five methods)
    kinds.py         → the four KindSpecs + motif expression rules
    registries.py    → closed vocabularies, read from the emitted corpus + catalog
tools/seedsmith/tests/test_adapter_demons.py
```

Mirrors `adapters/items/` exactly. **No file outside `adapters/demons/` should need to change** — if
one does, §1's claim is false and that is the finding.

**✅ The finding, as promised: one file changed.** `adapters/base.py` gained `KindSpec.motif_expression`
(additive, defaulted) for §2.7's motif expression rules. See §1's corrected-claim note above for the
full account and why the other two options (a demons-local dict, or abusing `registries()`) were
rejected.

---

## 5. Testing strategy

| Case | Expect |
|---|---|
| The emitted corpus loads through `Corpus.load` with this adapter | passes, no core change |
| `channels()` | **empty** — asserted, so nobody "fixes" it |
| `legal_combinations()` | the `False` branch is reachable and exercised, not merely declared |
| `kinds()` | contains no `item` and no `action` — a test asserts their **absence** (A3) |
| `environment` partitions | excluded from coverage while no consumer exists (A7) |
| `family` dimension in D1 | declared, **values empty**, partitioning falls back to `side/rarity` |
| Every registry vocabulary | non-empty and inlinable — a brief built from it contains no citation-shaped text |
| Motif expression rules | present for every kind; a kind without one fails the adapter's own validation |
| **The seam itself** | `_stub`'s tests still pass — the core did not learn a demon concept |

That last row is the one this module exists to prove.

---

## 6. Boundaries

- **Always:** implement all five methods; keep every closed vocabulary inlinable; declare
  `reference_fields` so ordering derives; treat the species catalog as authoritative.
- **Ask first:** adding a kind; changing a frozen vocabulary; generating into `environment`; any
  edit to a file outside `adapters/demons/`.
- **Never:** add `item` or `action` kinds here (A3); populate `channels()`; renumber an append-only
  vocabulary; cite a registry by filename in anything a model reads.

---

## 7. Success criteria

1. The demons corpus loads and every existing metric runs against it — **zero model calls**.
2. **No core file changed.** The `_stub` suite still passes.
3. `channels()` empty, asserted.
4. No `item`/`action` kind, asserted.
5. `environment` declared but excluded from coverage, with the reason in a comment.
6. Full seedsmith suite green.

---

## 8. Open questions

**Both closed 2026-08-31.**

1. ~~Does `demon` need `id_pattern` / `runtime_id_fields`?~~ **DECIDED: leave both unset**, matching
   all 15 item kinds and their documented reason. `speciesId` is already stable kebab-case, so the
   patterns would encode a rule nothing checks — and no acceptance criterion in this spec exercises
   them. Adding them later is additive; a wrong pattern shipped now is not.
2. ~~Should `aspect` ship in D1 or wait?~~ **DECIDED by the owner: the demon program builds
   `aspect-scope` first, and D2's aspect generation waits for it.** The kind is still *declared* in
   D1 — that is free and keeps the adapter's shape settled — but nothing generates into it until the
   tier exists in code. `DemonSpeciesDef` still carries `ElementPrimary`/`ElementSecondary`/
   `TraitPool` on the *species*, so generating aspects today would target a tier that is approved on
   paper only.

   ⛔ **This is a cross-program dependency, not a footnote.** It is recorded in
   [`seedsmith-map.md`](../seedsmith-map.md) §3b with the demon program as its owner, because a
   dependency on another program's unscheduled work is the kind that gets discovered late (audit S2).
