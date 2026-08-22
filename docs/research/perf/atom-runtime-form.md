# Atom runtime form — the measurement (E13)

**Date:** 2026-08-22 · **Module:** E13 `runtime-form-benchmark` ([spec](../../architecture/effect-atom/spec-runtime-form-benchmark.md))
**Harness:** `tests/FusionRpg.Bench` (Release) · **Guard:** `AtomBenchGuardTests` (CI)

## Environment

| | |
|---|---|
| OS | Windows 10.0.26200, X64 |
| Runtime | .NET 8.0.30 |
| Cores | 32 logical |
| GC | workstation, single thread |
| **Machine state** | **Not quiet.** Concurrent builds and test runs from other streams were live throughout. See *Confidence* below |

## Corpus

200 predicates, depths 1–4, mixed leaves across all 11 leaf ids, generated from a fixed seed —
**not clones**. 512 owner fact-sets, walked with co-prime strides so consecutive evaluations land far
apart in both arrays.

## Result

| Form | cold ns/atom | hot ns/atom | alloc |
|---|---|---|---|
| Typed object graph | 37.3 – 38.3 | 22.1 – 23.6 | **0 B** |
| **Flattened, non-recursive** | **29.9 – 32.7** | 20.8 – 23.0 | **0 B** |

**Winner: flattened, non-recursive** — wins cold by **15–20%** across runs, and is a wash hot. Decision
rule is lowest cold-cache median, because a real board walks cold memory.

Both candidates pass the equivalence fuzz against E3's reference interpreter (10⁴ trees × 4 fact sets
each), so the swap is safe by construction rather than by inspection.

## This reverses the earlier scratch benchmark, and the spec predicted why

The 2026-08-22 scratchpad run had the typed graph at **7 ns** and a flat walker at **47 ns**. That
measurement was unreliable for the two reasons the spec named before this module started:

- it used **six identical trees**, which is unrealistically kind to branch prediction and cache, and
  flatters whichever form has the tightest inner loop;
- its flat candidate was a **naive `ref int pc` span walker**, not a properly flattened encoding.

The form measured here precomputes short-circuit jump targets at compile time, so evaluation is one
loop with no call stack and no bounds-checked slicing, and negation costs nothing — it swaps the
arrows rather than emitting an instruction.

## Two methodology errors found while measuring

Recorded because both produced confident wrong answers before they were caught.

**1. The allocation probe measured itself.** The first cut timed and probed in one pass and reported
**40 B** for both candidates — that was the `Stopwatch` object, not the evaluation. A probe sharing a
scope with its own harness cannot tell you whether the hot path allocates. Separated: both are 0 B.

**2. Sequential measurement let drift pick the winner.** Measuring one candidate to completion and
then the other put all thermal and scheduler drift on whichever went first. Across three runs the
winner flipped and cold/hot inverted — variance ±45%, larger than the gap being measured.
**Interleaving the candidates within each round** cancels drift shared by both, and the result became
consistent immediately.

## Confidence

**Medium, and deliberately not higher.** The direction is consistent across every interleaved run and
the margin (15–20%) comfortably exceeds the residual spread, but this machine was busy. One run showed
59.98 / 49.04 against a typical 37 / 31 — a clear outlier, kept in the record rather than dropped.

The spec says the budget is defined **in CI, not on a developer box**. That still holds: this chose
the encoding, and CI owns the number.

## The guard

`AtomBenchGuardTests`, in the normal Core suite so it runs on every CI pass:

| | |
|---|---|
| Budget | **≤ 50 ns/atom** |
| Fails at | **> 75 ns** (1.5×) — the gap absorbs a noisy agent, not a real regression |
| Method | median of 9, raw numbers always printed |
| Observed | **26.94 ns/atom**, raw 26.39 – 29.91 |
| Allocation | **0 bytes** over 10⁵ evaluations |

A third test asserts `TryCompile` still returns `FlatPredicate`. Reverting to the typed graph should
be a decision someone makes on purpose, not a regression that slips through.

## What stays

The typed object graph is **kept, not deleted**. It is the reference the equivalence fuzz checks the
shipped form against, and the benchmark needs a second candidate to be a comparison at all.

**E11 re-runs this** against the migrated catalog to confirm the winner holds on real content rather
than generated content.
