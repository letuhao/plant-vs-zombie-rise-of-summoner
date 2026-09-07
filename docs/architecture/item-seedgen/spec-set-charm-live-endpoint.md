# Spec: `set-charm-live-endpoint`

**Module id:** `set-charm-live-endpoint` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Phase:** 4 (parallel with `recipes-gen`, `combination-write-unblock`)
**Depends on:** `generator-harness`, `base-types-gen`

⚠ **Corrected 2026-09-07 — this is not an independent side branch.** A set's `members[].role`/`.frame`
are a CATEGORICAL reference into the base-type corpus (`setgen/schema.py:104-107`: enums, never a
container id) — real coverage for `(role, frame)`, not just a schema-shaped brief, is what makes a
generated set's member slots fillable at all. Running this before `base-types-gen` has real coverage
means a live run can generate a set with a member combination zero base-types satisfy — a silent,
unfillable set, exactly the failure the owner named ("cannot have bonus for item that not exist").

## Objective

Wire the ALREADY-BUILT, architecturally-correct `setgen`/`charmgen` machinery to a real model endpoint.
This is finishing, not building from scratch: the brief-and-answer design is proven correct (model picks
identity only, code resolves every number), but `items generate` has never actually called a live model
— every "verified" sample used `ReplayTransport`, a hand-written stand-in for what a model would say
(`setgen/answers.py`: *"reads answers a model already authored, from a file... imports nothing from
pipeline.llm_caller"*). A real live-model transport already exists and is proven elsewhere:
`pipeline/llm_caller.py:call_model` does a real HTTP POST to a chat-completions endpoint, and sibling
commands `effects generate`/`demons generate` already expose `--endpoint`/`--model` wired to it. `items
generate` doesn't. `cmd_items` (`report/cli.py:365-490`) has no live-endpoint flag at all — `--model` is
metadata-only, and `--write` without `--answers` refuses outright.

**Target users:** whoever runs a real set/charm generation pass going forward — including the held
~904/36/~904 full run, once authorized separately.

## Acceptance criteria

1. `items generate --kind set|charm` gains `--endpoint`/`--model` flags, wired to
   `pipeline.llm_caller.call_model`, matching the exact pattern `effects generate`/`demons generate`
   already use — no new transport design, a direct port of an existing, working one.
2. `--write` without `--answers` no longer unconditionally refuses when `--endpoint` is supplied — it
   calls the live endpoint instead of requiring a replay file.
3. `ReplayTransport`/`--answers <file>` stays exactly as-is — it remains the deterministic-testing path
   (CI, dry runs), not replaced. This module adds a second real transport, not a replacement for the
   test one.
4. A real, small, explicitly-authorized live run (a handful of sets/charms, NOT the full ~904/36 —
   that's a separate, held decision) produces output identical in shape to what the replay-transport
   samples already proved importable, confirming the live path produces the same schema the replay path
   already validated.
5. **New, per the corrected dependency above.** Before a live run, `items validate --deps` (per
   `generator-harness`) runs against `base-types-gen`'s real output and confirms every `(role, frame)`
   combination the run's own brief could plausibly request has ≥1 satisfying base-type — a live run
   that would generate an unfillable member combination is refused before it spends a model call, not
   discovered after import.

## Commands

```
python -m seedsmith items generate --kind set --endpoint <url> --model <name> --write
python -m seedsmith items generate --kind charm --endpoint <url> --model <name> --write
# unchanged, still the deterministic test path:
python -m seedsmith items generate --kind set --answers <file> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/report/cli.py        EDIT — cmd_items gains --endpoint/--model, wires to
                                                llm_caller.call_model when --answers is absent
tools/seedsmith/seedsmith/adapters/items/setgen/run.py     EDIT — accept the live call function,
                                                            same shape ReplayTransport already satisfies
```

## Code style

Match `effects generate`'s own `--endpoint`/`--model` wiring in `cli.py` line-for-line where reasonable
— this is the third command adopting the same pattern (after effects, demons), and a fourth generator
later should be able to copy THIS module's diff as its own template.

## Testing strategy

- CLI test: `--write` with `--endpoint` and no `--answers` does not refuse (the current refusal
  condition narrows correctly).
- CLI test: `--write` with neither `--endpoint` nor `--answers` still refuses (the safety net stays).
- A live-endpoint integration test (skipped in CI without a reachable endpoint, matching however
  `effects generate`'s own live tests are gated today — find and reuse that skip convention).

## Boundaries

**Always:** keep `--answers`/`ReplayTransport` fully functional — it is the deterministic test path this
program's CI depends on.

**Ask first:** running the full ~904/36/~904 corpus through the newly-live endpoint — that authorization
is separate from, and does not follow automatically from, wiring the endpoint itself.

**Never:** let a live-endpoint failure (timeout, malformed response) partially write a set/charm entry —
the harness's atomic-write discipline applies here exactly as it does to every deterministic generator.
