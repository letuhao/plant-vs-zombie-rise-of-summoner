# Loop prompt — effect-atom

Next unchecked task in `tasks/effect-atom-todo.md`. One module per cycle.
Read `docs/DESIGN-GATE.md` §1 for that subsystem first. Never `git commit`/`add`.

## 0 — Declare, before any code

1. **What does the task line claim?** Quote every clause. Do not paraphrase.
2. **Who must CALL this?** Name the file. E6's `BindGate` shipped with 34 green tests, no callers.
3. **Which dependencies must it WIRE?** E4 shipped without wiring E2/E3 — predicates and value
   specs unvalidated behind 23 green tests.
4. **Does the contract carry what the classifier routed on?** E7 routed atoms to the runner *because*
   of `capPerMatch`, then dropped the key.

If a clause can't be delivered here, name its owning module and **move it** — never relabel it "debt".

## 0b — An open question is usually a search you haven't run

- **Is this shape solved here already?** The Hello-push owner scope was answered by `PatronEndpoints`
  and by the middle-layer constitution.
- **Does a rule in this program decide it?** E1's code-or-data rule settled `effect_channel_policy`;
  the map §7 scope table settled whether a cooldown belongs here at all.
- **Does the defect log already own it?** D1–D4 are research owned by E9.

Escalate only genuine product, scheduling, or goldens calls — with options, not a question.

## 1 — Build

Smallest slice satisfying one clause. C# 10 in Core (no class primary constructors). In
`src/FusionRpg.Core/**` write "the Writer", never the type name — `guard-funnel-delta` reads comments.

- **If it can't be tested where it lives, move it.** The injector hosts no test project, so the push
  receiver's logic moved to Core, leaving a shim.
- **No settable dependency with a harmless default.** A sink defaulting to "refuse everything" is a
  runner that silently swallows every proc. Require it in the constructor.
- Hex escapes in a bash heredoc write **real control bytes** into source and docs: build such strings
  from explicit byte values and sweep after.

## 2 — Test (RED first)

- Run it **before** the fix. Passing means your hypothesis is wrong.
- **Assert what the design requires, not what the code does.** A test asserted the Funnel was
  *pending* after a proc and passed — pending meant a one-event lag on every effect.
- **Test absences**: "what happens to rows nothing points at?" found an unbounded leak.
- **Never write the oracle to match the implementation**, and prove the matrix isn't vacuous:
  require a true *and* a false, or it agrees about nothing.
- Seeded tests assert **exact counts**, never tolerances.

## 3 — Review (prove it)

Correctness · readability · architecture · security · performance.

- A comment is not evidence. Open the file. Read the **section**, not the line.
- "Moves goldens" / "needs sign-off" / "this is a bug" are claims — run the suite. Twice a defect I
  wrote up was my own code.
- Never trust a first measurement; a probe must not measure its own harness.
- Ask each cycle: N+1? (carry what you already loaded) · unbounded growth? · declared and never
  read? · **a guard that outlived its reason?** E1 refused `capPerMatch` as "not available yet"
  after E15 shipped it.

## 4 — Propagate and cover

A fix in prose only has not landed: prose · that spec's Structure/Testing/Boundaries ·
`effect-atom-map.md` · `effect-atom-todo.md`. Re-run verifier + link check. Then add the test
that would have caught the bug, named after the failure.

## Close

All suites · four `scripts/guard-*.ps1` · verifier · link check.
**Other streams edit this tree.** Check a failing file's mtime and `git status` before blaming your
change; never edit their files. A failure you can't reproduce is "transient", not "diagnosed".

Report: every clause delivered or not · what you could **not** tick and why · bugs in your own prior
work · numbers · commit message + paths.

**Impact test:** name the thing that can no longer silently break. If you can't, the cycle added code,
not confidence — say so.
