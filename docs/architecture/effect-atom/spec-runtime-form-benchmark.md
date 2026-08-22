# Spec: runtime-form-benchmark (E13)

Module **E13** in the [atom effect map](../effect-atom-map.md). Depends on **E4**, **E2** (it rolls `OnApply` ranges), **E3** (it diffs against the reference interpreter). Chooses the compiled representation; ships a permanent regression guard.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Decide the **compiled runtime form** by measurement against real content, and leave behind a benchmark that fails CI if the hot path regresses. The ideal explicitly refused to settle this on paper, and it was right to.

## Design (locked on approval)

### What is already settled — and what surprised us

A micro-benchmark (2026-08-22, scratchpad `atombench`) ran three representations doing identical work: a 3-leaf predicate tree, a chance gate, and an `OnApply` range roll. 6 atoms × 200 000 hits.

| Representation | ns/atom |
|---|---|
| `Dictionary<string,object>` + nested-dict tree | **179.4** |
| Typed object graph, virtual dispatch | **7.0** |
| Int opcode span, **recursive** walker | **47.2** |

**Settled, robustly:** dictionaries and string comparison are out — 25× a typed graph, and against the perf plan's own rule that the record path allocates *"no dictionaries or strings"*.

**The surprise:** the "baked" flat encoding **lost**, and the first reading of why was wrong. The 7 ns winner is a typed object graph — and `AndNode.Evaluate` calling `child.Evaluate` **is** mutual recursion. So "no recursion" would have disqualified the form the measurement chose. The 47 ns loss is better explained by `ref int pc` plus span bounds checks defeating inlining.

**Corrected rule: no dictionaries, no strings.** Recursion is not banned; it is measured like anything else.

**Deliberately unsettled:** the exact encoding. The benchmark used six *identical* trees, which is unrealistically kind to branch prediction and cache, and the flat candidate was a naive recursive interpreter rather than a properly flattened one. That is this module's job.

### What this module actually decides

Re-run the comparison **against representative content**, not six clones:

- **Shape:** ~200 **representative** atoms across all 12 kinds, predicate trees at depths 1–4, mixed leaves — hand-authored from the family library, because no real content exists at this position. **E11 adds an acceptance row** re-running the bench against the migrated catalog to confirm the winner holds.
- **Cache pressure:** atoms interleaved across owners so the evaluator walks cold memory, as it will on a real board.
- **Candidates:** (a) typed object graph (the current 7 ns holder, and it recurses — that is allowed), (b) flattened non-recursive encoding with precomputed short-circuit jump ranges, (c) any third the author wants to defend.

**Decision rule:** lowest **cold-cache** median wins. If a candidate wins cold by less than 10% but loses hot by more than 20%, the two are re-run at higher iteration counts and the result is escalated rather than guessed.

The winner becomes the compiled form E7 bakes into.

### Budget — the number that makes this pass or fail

Injector budget is **≤ 1.0 ms/frame average, ≤ 2 ms p99** for *all* systems at 60 fps ([perf-probe-plan.md](../../runbook/perf-probe-plan.md) §0). The atom layer gets a slice, not the budget.

**Acceptance: ≤ 50 ns/atom-evaluation on the CI reference machine**, recorded with CPU and runtime in the result file. The guard runs in CI, so the budget is defined there — not on a developer box.

**Method:** median of **9** runs; fails at **> 1.5×** the budget; raw numbers always printed.

**Where 6 × 500 came from:** it is an *assumption*, not a measurement. 6 atoms/hit is plausible for a geared actor; 500 hits/frame is a worst-case guess. Before this budget is treated as load-bearing, ground it against the perf stream's stress figures (200+ entities at max speed) and record the result here.

For calibration: the interpreted form reaches 0.54 ms at that load, half the entire injector budget on its own. The typed graph reaches 0.02 ms.

### The permanent guard

A benchmark test in CI, not a one-off script:

- Runs the chosen encoding over the fixed content corpus.
- Fails if ns/atom exceeds the budget by more than a stated tolerance.
- Asserts **zero allocation** over 10⁵ evaluations.

Benchmarks are noisy on a dev box. The guard therefore fails on a **sustained** regression — median of N runs — not on one slow sample, and it prints the raw numbers so a human can judge.

## Commands

```powershell
dotnet run -c Release --project tests\FusionRpg.Bench\FusionRpg.Bench.csproj
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Bench"
```

## Structure

```
tests/FusionRpg.Bench/                                   (new — Release-only harness)
tests/FusionRpg.Bench/AtomFormBench.cs                   (the candidate comparison)
tests/FusionRpg.Bench/Corpus.cs                          (~200 atoms, mixed kinds and depths)
src/FusionRpg.Core/Effects/Atoms/CompiledAtom.cs         (the winning form)
tests/FusionRpg.Core.Tests/Atoms/AtomBenchGuardTests.cs  (CI regression guard)
docs/research/perf/atom-runtime-form.md                  (result file — numbers, CPU, runtime, date)
```

## Testing strategy

| Case | Expect |
|---|---|
| Candidate comparison over the real corpus | a winner with numbers, written to the result file |
| Chosen form, 10⁵ evaluations | zero bytes — `GC.GetAllocatedBytesForCurrentThread()` delta after 1 000 warmup iterations, workstation GC, single thread |
| Chosen form vs the E3 reference interpreter | identical results on the equivalence fuzz corpus |
| Budget guard | ≤ 50 ns/atom sustained; prints raw numbers on failure |
| Cold-cache corpus vs hot | both recorded — a form that only wins hot is not the winner |

## Boundaries

**Always:** benchmark against representative content, never clones; record CPU and runtime alongside every number; keep the equivalence fuzz green when swapping encodings.

**Ask first:** raising the ns/atom budget; changing the corpus shape.

**Never:** decide the encoding from an argument instead of a measurement; ship a form that allocates per hit; let the guard fail on a single noisy sample.
