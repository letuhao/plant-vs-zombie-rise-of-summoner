# Spec: `motif-derive`

Module `motif-derive` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D2**.
Depends on `family-consolidate`.

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: proposed 2026-08-31, awaiting owner review. Not authorized to build.**

---

## 1. Objective

Derive each demon's **motifs** and **anti-motifs** — the shared vocabulary every generator draws from.

This is the feature's coherence mechanism. Because the aspect, commander-effect, environment and
theme generators all read the same 3–5 motifs, their output sounds like **one demon** *without any
generator knowing the others exist*. No cross-generator coordination, no shared context window, no
ordering constraint between them.

**Done means:** every demon has motifs, anti-motifs and a `basis`, derived with **no human pass**
(owner, Q1) and **no number anywhere**.

---

## 2. Design

### 2.1 Motifs alone are not enough — the expression rule is half the mechanism

Audit A1 is the finding this module must not forget:

> Give five generators `shell, endurance, patience` and they produce *Shell of Patience*, *Enduring
> Shell*, *Patient Shell*, *Shellfield*. That is not coherence — it is a thesaurus. And **no metric
> catches it**: every schema validates, every gate passes, coverage is complete, and the corpus is
> unreadable.

Motifs are shared *vocabulary*; coherence needs that vocabulary expressed **differently per kind**.
The expression rules live on each `KindSpec` (`adapter-demons` §2.7) — a motif is a *material* to an
item theme, a *tempo* to an action, a *terrain* to an environment, a *doctrine* to a commander
effect. This module supplies the noun; the adapter supplies the part of speech.

**Neither is sufficient alone**, and that is worth stating because a future reader will find the
expression rules in one file and the motifs in another and may treat one as the mechanism.

### 2.2 Derivation, and what it inherits

For each demon, in order:

1. **Family motifs** — every family the demon belongs to contributes its motifs. This is the
   inheritance channel that makes a family mean something rather than being a label.
2. **Own text** — `flavorInfo` / `flavorIntroduce` contribute demon-specific motifs.
3. **Trim to 3–5**, family-first, deterministically ordered.

A demon in two families inherits from both. A demon in none derives from its own text alone, and if
that is absent it is `blocked` — with no motifs, which is honest and visible rather than padded.

### 2.3 Anti-motifs are derived by contrast, not authored

Anti-motifs say what the demon is **not**, and they exist because a generator told only what a thing
*is* will happily add what it isn't. They are derived from the **nearest contrasting family** — the
family sharing the fewest motifs with this demon's own.

This is deliberately mechanical. Authoring anti-motifs would reintroduce the human pass Q1 removed,
and asking a model for "what is this demon not" invites answers that are true of everything.

### 2.4 `basis` propagates, and A2 is the reason

Each motif carries the `basis` of what produced it: `text`, `name` (inherited from a
`basis = name` family) or `blocked`.

Audit A2 is the sharpest finding in the ideal and this is where it lands:

> With thin text, **both** motifs and families derive from the same string — the name. Every "-nut"
> demon then gets nut-ish motifs *and* the nut family, and inheritance appears to work beautifully.
> It is a tautology: one token read twice. **And every metric reports success.**

So `basis` is not audit trail here, it is the input to a correctness check. `demon-metrics` excludes
demons whose motifs **and** families are both `basis = name` from the sharing metric, because their
apparent agreement carries no information. A module that emitted motifs without `basis` would look
identical and make that check impossible.

### 2.5 Pure derivation, and its honest cost

Owner, Q1: **pure derivation, no human pass.** Recorded plainly, because the first-pass quality is
the thing most likely to surprise:

For most types the derivation works from a name and little else — B3 measured **89 of 677** plants
carrying a cost at all, most with thin lore. **First-pass motifs for those will be visibly weak, and
they are supposed to be visibly weak.** The fix is `lore-enrich` (Q6b), which turns `basis = name`
into `basis = text` and triggers re-derivation — not a human editing pass, and not a better prompt.

This is why `basis` and `coverage` are load-bearing rather than bookkeeping: they are how the corpus
knows what to regenerate once the input improves.

### 2.6 No numbers, structurally

Nothing here is a magnitude. `adapter-demons` returns an empty `channels()` list, so `numerics` is
inert for this feature (A4) — there is no numeric path to misuse rather than a rule against using
one. A motif is a word.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_motif_derive.py -q
python -m seedsmith demons motifs           # derive and write
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/
    motifs.py     → derivation, inheritance, contrast, basis propagation
tools/seedsmith/tests/test_motif_derive.py
data/seed/demons/_registry/motifs.v1.json     → the append-only motif vocabulary
data/seed/demons/_generated/motif-assignments.json  → speciesId -> {motifs, antiMotifs, basis}
```

The motif **vocabulary** is append-only for the same reason family ids are: content is generated
against it, and renumbering silently re-parents that content.

---

## 5. Code style

Pure functions, composed: `family_motifs()`, `own_motifs()`, `trim()`, `contrast()`. No I/O below the
entry point. Deterministic ordering everywhere — `sorted()` on ties, never dict iteration order.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Same corpus derived twice | **byte-identical** assignments |
| Demon in two families | inherits from **both** |
| Demon in no family, with text | motifs from own text, `basis = "text"` |
| Demon in no family, no text | **no motifs**, `basis = "blocked"` — not padded, not an error |
| Family with `basis = "name"` | inherited motifs carry `basis = "name"`, not `"text"` |
| Motif count | always 3–5 where any motif exists |
| Anti-motifs | drawn from the contrasting family; **non-empty** wherever ≥2 families exist |
| Ordering | family-first, stable across runs |
| No numbers | assignments contain no numeric field — asserted, matching `audit_schema`'s rule |
| **A2's tautology case** | a demon whose family and motifs are both `basis = "name"` is **flagged** in the output so `demon-metrics` can exclude it — asserted here, not left to the consumer |

The last row matters: A2's mitigation is only real if this module marks the case. A consumer that had
to re-derive "were these the same string?" would be re-deriving the very thing that was lost.

---

## 7. Boundaries

- **Always:** propagate `basis`; inherit from every family; keep 3–5; derive anti-motifs by contrast;
  append new motif ids at the end.
- **Ask first:** changing the 3–5 bound; authoring any motif by hand (it reverses Q1); deriving
  anti-motifs from a model rather than by contrast.
- **Never:** emit a number; pad a `blocked` demon with invented motifs; rename or re-position a
  published motif id; treat the expression rules (`adapter-demons` §2.7) as optional.

---

## 8. Success criteria

1. Deterministic, byte-identical across runs.
2. Multi-family inheritance works; zero-family and blocked cases are legal and visible.
3. `basis` propagates correctly through inheritance.
4. Tautological demons (A2) are flagged in the output.
5. No numeric field anywhere.
6. Motif vocabulary is append-only and inlinable into briefs.

---

## 9. Open questions

1. **How many motifs does a family contribute versus own text?** Family-first is specified; the exact
   split (2 inherited + 1 own? proportional?) wants measuring against the real roster, since it
   directly controls how same-y a family reads.
2. **Anti-motifs when a demon has only one family.** Contrast needs something to contrast with. The
   nearest *other* family is the obvious fallback; whether that is meaningful or noise is an empirical
   question on the first real corpus.
