# Loop prompt — action

Take the next unchecked task in `tasks/action-todo.md`. **One task per cycle, no pause between them.**
Specs: `docs/architecture/action/`. The map wins where they disagree. Never `git commit` / `git add`.

## 0 — Declare, before any code

Three answers, written down. These specs were audited twice and still shipped a claim the code did not have:

1. **What does the task line claim?** Quote every clause. Do not paraphrase.
2. **Who CALLS this?** Name the file. Green tests with no caller is a module that is not done.
3. **Which claim did you VERIFY against shipped code?** The spec said precompiling `TargetSpec` avoided a per-call dictionary — `FilterPool` re-parses it on every resolve. **Specs describe intent; read the code.**

## 1 — Build

RED first, and confirm it is red *for the stated reason*, not a compile error.
Then the minimum to pass. Then: the task filter → full Core suite → four guards
(`guard-single-writer` · `guard-secondary-no-unity` · `guard-funnel-delta` · `guard-dal`).

## 2 — Review your own diff

Five axes — correctness, readability, architecture, security, performance. Then this program's four:

- **Could this test pass while the thing it names is broken?** Then it is decoration. Prove it can fail.
- **Did I add a second system?** One targeting stack, one condition language, one scaling mechanism, one binding vocabulary. All four already exist.
- **Any determinism leak?** Wall clock, ambient RNG, dictionary enumeration, unstable sort, culture-sensitive compare.
- **Did allocation move?** Zero-byte paths stay zero.

Fix what you find. Add the coverage the review exposed.

## 3 — Close

Tick the task with what **actually** happened — deviations, findings, what the spec got wrong. Then start the next task.

## Stop and ask — only these

- ⛔ **Checkpoint A** (after T11) and ⛔ **Checkpoint D**. Owner sign-off.
- **A golden moved in Phases 0–4.** Do NOT re-bless. A moved golden means the model is wrong, not the baseline.
- A spec contradicts shipped code in a way that changes the design.
- A decision the specs do not cover.

## Never

- No git writes. Hand over a commit message draft and the changed paths.
- No re-blessing outside Checkpoint D.
- **No editing another stream's files.** If effect-atom or world-map breaks shared Core, **wait and retry** — never fix their code.
- No loosening a test to make it green. Diagnose. A weakened test is worse than a red one.
- No vendor or AI watermarks.

## Order traps

- **P1 (purity guard) before the first line of `Core/Actions/`** — otherwise the first file lands unguarded.
- **T8 (parity capture) before any engine change** — you cannot prove identity against a baseline you never recorded.
- **T3 and T17 look skippable and are not.** Binding is the first thing A4 and A7 ask for; the view seam erodes the moment the AI is written first.

## Report per cycle

Task id · what shipped · tests added / suite total / guards · what the review found · what the spec got wrong · next task.

Factual. If tests fail, show the output. **Not done unless the full suite and all four guards are green.**
