# World-graph turn-commit write cost — measurement (base-defense 3.1, `world-graph-diff`)

Spec: [`../../architecture/base-defense/spec-world-graph-diff.md`](../../architecture/base-defense/spec-world-graph-diff.md).
Harness: `tests/FusionRpg.Bench/WorldGraphWriteBench.cs`, run via `tests/FusionRpg.Bench` (Release only —
see the project's own comment on why Debug numbers would measure the JIT, not the code).

## Method

An isolated SQLite harness — not a call into `RpgStore` — reproducing the same 7-table `CREATE TABLE`
text `EnsureWorldSchemaUnlocked` ships and the same per-row calling convention `RpgStore.Insert` uses
(one `SqliteCommand` per row, `AddWithValue` per parameter), against a synthetic world at decision 19's
scale: **18 sectors × 20 slots = 360 slot rows**, 2 factions, 20 lanes, 30 entities × 3 members = 90
member rows, 36 `rpg_world_faction_intel` rows (2 factions × 18 sectors) — **556 rows total**, matching
a real turn-commit's clear-and-rewrite shape. Median of 7 runs per phase, same discipline as the
existing E13 atom-form benchmark in the same project.

Isolated instead of calling `RpgStore` directly because every write helper (`ClearWorldGraphUnlocked`,
`WriteWorldGraphUnlocked`, `Insert`) is private, and widening that surface for a benchmark-only caller
would touch production code to answer a question production code does not need to know the answer to.
Representative because the SQL text and transaction shape are identical to the shipped code;
independent because nothing in the harness can move a golden or touch `guard-dal`.

## Results (2026-09-05, this machine — 32 logical cores, .NET 8.0.30, Release)

| phase | median ms |
|---|---|
| clear (7× `DELETE`, one command per table — today's `ClearWorldGraphUnlocked` pattern already) | 7.05–8.43 |
| write — fresh `SqliteCommand` per row (today's `RpgStore.Insert`) | 13.11–13.98 |
| write — one prepared `SqliteCommand` reused per table | 10.32–10.97 |
| — of which: `slots_json`/`forces_json` serialisation alone (**C5's named suspect**) | 0.07–0.08 |
| — of which: `SqliteCommand` construction alone (slots, fresh-per-row pattern, 360 rows) | 0.50–0.53 |
| — of which: `ExecuteNonQuery` alone (slots, fresh-per-row pattern, 360 rows) | 2.89–3.76 |
| control: an **empty** transaction's own `BeginTransaction` + `Commit` | 0.12 |

Two runs shown as ranges; both agree well within noise (see `Spread`-style variance in `AtomFormBench`'s
own convention — not repeated here since the ranges above already bound it).

## Reading the numbers against C5's two candidates

C5 named two candidate dominant costs and said "measure before choosing a diffing writer":

1. **Row count** (~556 rows at this scale, heading toward more once decision 21 grows slots) → fix: a diffing writer
2. **Per-row overhead** — a fresh `SqliteCommand` per row, plus `slots_json` re-serialised per (faction × sector) → fix: prepared-statement reuse

**A third candidate was tested and ruled out first:** transaction-commit (`fsync`) cost. An empty
transaction commits in **0.12 ms** — under 1% of either clear (7–8 ms) or write (11–14 ms). If commit
fsync dominated, clear and write would each cost close to 0.12 ms regardless of row count; they do not,
by roughly two orders of magnitude. **Fsync is not the answer, tested and rejected, not assumed.**

**`slots_json`/`forces_json` serialisation is measured at 0.07–0.08 ms total** across all 36 intel
rows — negligible, well under 1% of write cost. **C5's own named suspect for the per-row candidate is
falsified by measurement.** The comment that raised it was right to flag the fresh-`SqliteCommand`
pattern as suspicious; the JSON half of that suspicion does not hold up.

**Statement reuse recovers ~20–22% of write cost** (13.11→10.32, 13.98→10.97 — both single-digit-ms
absolute, but a consistent ~1/5 fraction across both runs), and it recovers **nothing from clear**,
because `ClearWorldGraphUnlocked` already issues one `DELETE` per table, not per row — it was never the
per-row pattern C5 was worried about. So of the ~20–22 ms a full clear-and-rewrite commit costs at this
scale, prepared statements save roughly 2–3 ms (write's fresh-vs-prepared delta), or **under 15% of the
combined clear+write total**.

**That leaves roughly 85% of the cost tracking row count directly** — actual `INSERT`/`DELETE`
execution work (B-tree page writes, index maintenance) that scales with how many rows exist, which is
exactly what decision 21 (a sector gains capacity by *growing slots*) multiplies, and exactly what a
diffing writer — writing only what changed — would cut instead of paying every turn regardless of how
many sectors actually moved.

## Decision gate (per the spec: record the outcome explicitly, don't leave the module open)

> *"If measurement says [per-row overhead] dominates, take the cheap fix and stop. […] steps 2/3 are
> cancelled if statement reuse dominates."*

**Statement reuse does not dominate — measured at ~20% of write cost and 0% of clear cost, not a
majority of either.** So per the spec's own gate:

- **Step 2 (prepared-statement reuse) is NOT cancelled.** It is a genuine, if modest, win — cheap,
  local, no logic or schema change — and worth taking regardless, since it recovers real cost for
  near-zero risk.
- **Step 3 (the diffing writer) is NOT cancelled either.** The majority of the cost (~85% of
  clear+write) tracks row count, which is precisely the term decision 21 is about to multiply.
  Shipping only step 2 would leave the module's own stated risk — *"a diffing writer stops being
  optional"* (spec-world-graph-diff.md §0, quoting the audit) — unaddressed.

**Both steps proceed.** This is the honest outcome of the measurement, not the outcome either
candidate's own advocate would have guessed: C5's specific named suspect (`slots_json` serialisation)
was wrong, but its broader instinct (per-row `SqliteCommand` construction is real overhead) was right —
just smaller in proportion to the row-count-scaling cost than either candidate framing assumed on its
own.

## What this does not answer

This measurement is at **556 rows** (18×20 slots plus the rest of decision 19's scale), not at whatever
scale a diffing writer's benefit curve actually flattens the row-count term back down to something
comparable to step 2's savings — that is `Diff_write_read_back_equals_full_write_read_back`'s job
(500 randomised mutations, per the spec's own test list) once step 3 is built, not this benchmark's.
