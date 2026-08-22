# Loop prompt — effect-atom

Take the next unchecked task in `tasks/effect-atom-todo.md`. One module per cycle.
Read `docs/DESIGN-GATE.md` §1 for that subsystem first. Never `git commit`/`add`.

## 0 — Declare, before any code

Write these three answers. Most defects this program has shipped were caught here or not at all:

1. **What does the task line claim?** Quote every clause. Do not paraphrase.
2. **Who must CALL this for the claim to be true?** Name the file. If nothing calls it, the module
   is not done when its tests pass — E6's `BindGate` shipped with 34 green tests and zero callers.
3. **Which dependencies must it WIRE?** E4 shipped without wiring E2 or E3, so predicate trees and
   value specs were unvalidated behind 23 passing tests.

If a clause can't be delivered here, say which module owns it and **move it** — don't relabel it
"debt" and tick the box.

## 1 — Build

Smallest slice that satisfies one clause. C# 10 in Core (no primary constructors on classes).
In `src/FusionRpg.Core/**`, write "the Writer", never the type name — `guard-funnel-delta` regexes
comments too.

## 2 — Test (RED first, and mean it)

- Run the test **before** the fix. If it passes, your hypothesis is wrong — investigate, don't patch.
- Confirm it fails for the **stated reason**, not a typo or missing seed.
- **Test absences.** "What happens to rows nothing points at?" found an unbounded instance leak.
  Nothing deletes → no code → no test → invisible.
- **Never write the oracle to match the implementation.** A 10⁴-case fuzz hid a real bug because its
  reference interpreter repeated the same mistake. Give reference and implementation *different*
  inputs where they must agree.
- Seeded tests assert **exact counts**, never tolerances.

## 3 — Review (verify, don't assert)

Five axes: correctness · readability · architecture · security · performance.
Before reporting any finding, **prove it**:

- A comment is not evidence. Open the file.
- Read the **section** a rule sits in before quoting it as a law.
- "This moves goldens" / "needs sign-off" / "this is a bug" are claims. Run the suite. Twice this
  session a "bug" was working code and a grep that lied (an invisible `0x1F` byte read as `""`).
- **Measurements:** never trust the first number. Interleave candidates or drift picks the winner.
  A probe must not measure its own harness — a `Stopwatch` reported as 40 B of hot-path allocation.
- Ask each cycle: N+1? unbounded growth? something declared and never read?

## 4 — Fix and propagate

A correction that lands in prose only has not landed. For each fix, update:
prose · that spec's Structure/Testing/Boundaries · `effect-atom-map.md` · `effect-atom-todo.md`.
Then re-run the verifier — it has caught me dropping a dependency clause mid-edit.

## 5 — Cover

Add the test that would have caught the bug you just fixed. Name it after the failure, not the method.

## Close the cycle

Run: both suites · four `scripts/guard-*.ps1` · the cross-doc verifier.
Other streams are live in this tree — check a failing file's mtime and `git status` before blaming
your change, and never edit another stream's files.

Report, in this order:
1. What the task claimed and whether **every** clause is delivered.
2. What you **could not** tick, and why.
3. Bugs found in your own prior work — these matter more than new features.
4. Numbers: tests, suites, guards.
5. A commit message and paths. Do not commit.

**Impact test for the cycle:** name the thing that can no longer silently break. If you can't, the
cycle added code but not confidence — say so.
