# Spec: `dump-preflight`

**Module id:** `dump-preflight` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 5 of 16
**Model calls:** one, and only to prove the model answers.

## Objective

Refuse to start a generation run unless every prerequisite is present — and **ask the human for
whatever is missing** rather than guessing or degrading silently.

Owner, Q13: *"option 1, and add new skill to setup before run seedsmith. that agent will check
requirement and ask human to prove them if missing."*

The failure this prevents is specific and has already happened twice in this repo: a long unattended
run that produced plausible output from a stale or partial input, discovered hours later. **A run that
cannot be trusted is more expensive than a run that never started.**

## Design

### 1. A committed check command, with a skill as a thin wrapper over it

**⛔ `.claude/` is gitignored in its entirety** (`.gitignore:106`; `git ls-files .claude` returns
nothing). A check that lives only in a skill file is **local to one machine, absent from CI, and
invisible to every other clone.** An earlier draft of this spec made the skill the primary artifact;
that was wrong, and it is the kind of wrong that is discovered when a preflight silently does not
exist.

So the split is:

| Artifact | Where | Committed |
|---|---|---|
| the nine checks, and every verdict | `tools/seedsmith/.../preflight.py` | **yes** |
| the conversation that resolves a failure | `.claude/skills/seedsmith-preflight/SKILL.md` | no — local convenience |

**No check may exist only in the skill.** The skill's whole job is the part a library cannot do: a
missing model is not an error to print, it is a question to ask, and the answer ("start LM Studio",
"point at a different pack", "the dump is stale, re-export it") is a human decision. It asks; it never
detects.

### 2. The checks

| # | Check | How it is verified | If it fails |
|---|---|---|---|
| 1 | dump exists | `data/seed/demons/_dump/_manifest.json` present and parses | ask: run `DemonCorpusDump`? |
| 2 | **dump is current** | re-hash the four payload files, compare with `_manifest.contentHash` | ask: re-export, or proceed against the recorded hash? |
| 3 | dump is complete | row counts match the manifest's declared counts per side | refuse — a truncated dump is never proceeded against |
| 4 | contract audits clean | `demons contract --audit` exits 0 | refuse — a numeric field in the schema poisons every downstream row |
| 5 | model answers | one real call, trivial prompt, schema-constrained | ask: is LM Studio up? is the model id right? |
| 6 | **model honours the schema** | that same call requests a two-field object and the reply is checked | refuse — constrained decoding not working turns every guardrail off |
| 7 | venv + lock current | `requirements.lock` matches the installed set | ask: install? |
| 8 | tuning present | `demon-threat.v1.json` loads and validates | refuse |
| 9 | disk headroom | enough for the run's expected output | ask |

**Check 6 is the one that would be skipped and must not be.** `llm_caller.call_model` passes
`response_format: json_schema`, which llama.cpp enforces through GBNF grammar sampling — *for GGUF
models*. A server or model that quietly ignores it returns prose, and every schema guarantee in
`anchor-contract` becomes decorative. Proving enforcement costs one call at the start; discovering it
absent costs a whole run.

### 3. Refuse vs ask — the split is deliberate

**Refuse** where proceeding produces confidently wrong data with no signal (3, 4, 6, 8). **Ask** where
proceeding is a legitimate choice the human might make (1, 2, 5, 7, 9) — re-deriving against a
deliberately pinned older dump is a real workflow, and a preflight that forbids it is a preflight
people route around.

Every refusal names the check, the observed value, the expected value, and the one command that fixes
it. A refusal that says only "preflight failed" is a defect in this module.

### 4. What it records

On pass it writes `data/seed/demons/_dump/_preflight.json`: the dump hash it validated, the model id
and server that answered, the lock hash, and a UTC timestamp. `run-control` reads it and refuses to
start a run whose preflight record does not match the dump it is about to read. **The preflight is
evidence, not a ritual.**

## Commands

```powershell
python -m seedsmith demons preflight                  # human-readable, exit 1 on any refusal
python -m seedsmith demons preflight --json           # machine-readable, for run-control
python -m seedsmith demons preflight --skip-model     # checks 1-4, 7-9 only; never allowed before a real run
```

`--skip-model` exists for CI, which has no local model. It is refused by `run-control`.

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/preflight.py   the checks - committed, CI-visible
tools/seedsmith/tests/test_preflight.py
.claude/skills/seedsmith-preflight/SKILL.md              the conversation - local only, gitignored
```

## Code style

Each check is a small function returning a typed result — `(id, ok, observed, expected, fix_command)`
— never a bare bool. The report is a rendering of those records, so a check cannot exist without a
stated fix.

## Testing strategy

| Test | Asserts |
|---|---|
| `stale_dump_is_detected_by_hash_not_mtime` | touching a file does not pass; changing a byte does not fail-open |
| `truncated_dump_refuses_never_asks` | check 3 is in the refuse set |
| `schema_ignoring_model_refuses` | a stubbed server returning prose fails check 6 |
| `every_failure_names_a_fix_command` | mechanically, over all nine checks |
| `preflight_record_is_written_only_on_full_pass` | no partial record |
| `every_check_is_reachable_from_the_committed_module` | no check exists only in the gitignored skill |
| `skip_model_record_is_rejected_by_run_control` | the CI escape hatch cannot leak into a real run |

## Boundaries

**Always:** verify the dump by hash; prove schema enforcement with a real call; name a fix for every
failure; write the record only on a full pass.

**Ask first:** adding a check that can refuse (it can block the owner's own run).

**Never:** put a check in the skill instead of the module; compare dumps by mtime; let `--skip-model` reach a real run; print a failure without its
fix command; auto-repair anything — this module detects and asks, it does not act.

## Success criteria

- [ ] A stale dump is caught by content hash, proven by a test that changes one byte.
- [ ] A model that ignores `response_format` is caught before any species is classified.
- [ ] Every one of the nine checks names its own fix command.
- [ ] `run-control` can read the record and match it against the dump it is about to consume.
- [ ] The skill asks rather than assumes wherever proceeding is a legitimate human choice.
