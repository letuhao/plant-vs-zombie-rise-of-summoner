# Spec: `fill-runner`

**Module id:** `fill-runner` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** post-foundation operational repair
**Depends on:** `generator-harness` and every generator it dispatches

## Objective

Make one `seedsmith items fill --full` a safe resumable walk of the item-seedgen generators. The
runner owns orchestration only: discovery, deterministic order, checkpoint handling, and exit
reporting. A generator still owns its schema, brief, corpus write, and deterministic validity check.

This closes a gap between the harness's per-subject resume contract and the CLI's cross-kind walk.
The existing `items fill` command stops after an ordinary `EXIT_GAP`; graph-backed set, charm, and
combination generation currently use that exit for an `escalated` subject. Thus a recorded problem
can prevent independent later work from running.

## Contract

### Terminal subject outcomes

The ledger distinguishes three terminal outcomes:

| Outcome | Seed row | Resume default | Meaning |
|---|---|---|---|
| `persisted` | yes | skip after reconcile succeeds | Content was accepted and written. |
| `blocked` | no | skip | The model made a coherent, usable refusal. |
| `escalated` | no | skip | Validation/transport exhausted its bounded retries; preserve diagnostic evidence for review. |

`escalated` is not `done`. It must be stored through a typed terminal-outcome API (or an equivalent
separate ledger section), with subject id, intended entry id, attempts, defects, and a schema version.
The planner treats it as terminal for ordinary resume. A future explicit retry command may requeue
only selected escalations; ordinary `--full` never silently retries them.

### Walk and exit behavior

The fill runner continues after a step reporting *recorded escalation*. It still stops on a refusal,
dependency preflight failure, process error, or an unclassified corpus gap unless `--continue-on-error`
is set. Therefore an escalation cannot starve later independent steps, while safety failures remain
fail-closed.

Use a distinct, named generator exit result for `recorded escalation`; do not overload the generic
content-gap exit code. The fill report exposes `status: "escalated"`, including its exit code and
diagnostic note. A run with one or more newly observed escalations exits non-zero only after every
runnable later step has been attempted. A repeat run that finds only previously recorded escalations
has no fresh escalation and exits zero.

### Corpus/ledger checkpoint ordering

For open-ended generators, an accepted row is written to its corpus partition before its draw is
marked `done` in the ledger. The writer is additive and atomic. If the corpus write fails or the
process exits between model resolution and persistence, no `done` row is committed, so the same
draw remains retryable on resume. A ledger row must never advance the draw counter past content
that is not present on disk.

### `--full` depth

`--full` means all discovered partitions and all unbounded closed-grid subjects. Explicit `--limit`,
`--count`, and `--batch-size` always win; `--limit` is the operator's checkpoint size for set,
charm, and combination walks even when `--full` is present. When omitted:

For set and charm, an omitted limit uses the runner's bounded checkpoint internally and repeats until
the deterministic completion check reaches zero. This keeps bare `--full` resumable without changing
its all-subjects meaning; an interruption resumes from the on-disk ledger and corpus.

- The gem corpus has one global unauthored-family pool even though it is split across `gN.json`
  files; fill assigns that pool to the first discovered slot with work, so one family cannot be
  planned concurrently for multiple destinations. The selected gem slot draws the deterministic
  remaining count.
- Base type, enhancement milestone, recipe, and drop-table are intentionally open-ended; they run one
  documented elevated pass, not a fictional "exhaustion" claim.
- Set runs both `species` and `build` populations; charm remains `species` only.

All discovery and depth decisions are deterministic code. The model receives only the individual
subject brief and cannot choose partitions, depth, retries, ledger state, or what gets written.

`--verify` is an explicit post-run corpus gate. It first reconciles the finite generator populations
(set/charm subjects, combination cells, the global gem-family pool, and affix-family target
partitions), then runs the items health registry after all planned steps finish. It returns
non-zero when a finite population is pending/held or when any promoted health gate still has a gap.
The live CLI output includes this generation-completion summary. A clean
generator walk without `--verify` means only that the scheduled generators returned clean; it is not
evidence that the existing corpus is complete or runtime-importable.

Per-kind `items generate --dry-run` reports `toGenerate`, `alreadyDone`, `held`, and `complete`.
`complete` is true only when both `toGenerate` and `held` are zero; a clean process exit does not
override pending subjects or held design cases.

Affix-family discovery additionally applies the registry's `affixFamilies.agentsEach` sizing target:
partitions already at or above that target are reported as skipped, even when their quarantined
kind still has mechanically unused channel/op combinations. This prevents a full walk from
minting surplus families in a content kind that has no wave-1 runtime consumer.

## Interfaces

```text
seedsmith items fill --full
seedsmith items fill --full --count N --batch-size N
seedsmith items fill --full --continue-on-error
seedsmith items fill --full --verify
```

`FillStepResult.status` adds `escalated`. `FillReport` reports fresh versus previously-recorded
escalations separately. The generic `--continue-on-error` remains the deliberate override for real
refusals/errors; it is not required merely to get past a recorded escalation.

## Implementation boundaries

- `pipeline/run_ledger.py` owns typed persistence and backward-compatible reading of old outcome rows.
- `setgen`/`charmgen` and `combogen` call that shared terminal-outcome mechanism; neither calls a
  success-named method to store an escalation.
- `report/cli.py` maps graph batch results to the distinct escalation result.
- `adapters/items/fill.py` owns only dispatch classification, plan depth, and stable ordering.
- Socket-word retirement and consumable action/cooldown reconciliation are out of scope.

## Acceptance tests

1. A set, charm, and combination escalation writes a typed terminal ledger row; a second plan omits
   that subject without marking it persisted.
2. A fill whose first graph batch escalates still dispatches later kinds by default, reports an
   escalation, and exits non-zero only after the walk finishes.
3. Re-running that same fill performs no new call for the recorded subject and exits zero when no
   fresh escalation occurs.
4. A generic gap/refusal still stops by default; `--continue-on-error` remains the explicit override.
5. `--full` plans both set populations, every independent discovered partition, the gem global
   remaining-family count assigned to one destination slot, and the documented elevated count for
   each open-ended generator. Explicit depth flags win.
6. The dry-run plan and dispatch order are byte/order stable for identical corpus and ledger input.
7. `--verify` distinguishes a clean generator walk from finite populations that are pending/held
   and from a corpus with remaining promoted health gaps; it never changes corpus data itself.

## Boundaries

**Always:** append/reconcile existing corpus files; report terminal diagnostics; keep the model outside
of planning, ledger, and write authority.

Set and charm identity names share one player-facing namespace. A new batch compares its names with
both existing sibling corpora before writing; a collision is an `escalated` terminal subject, not an
overwrite or silently accepted duplicate. Legacy collisions already on disk remain explicit repair
work and are never renamed implicitly by fill.

**Never:** treat an escalation as accepted content, retry it forever, invent a new partition, or claim
an open-ended generator can be exhausted.
