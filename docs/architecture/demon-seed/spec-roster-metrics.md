# Spec: `roster-metrics`

**Module id:** `roster-metrics` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 10 of 16
**Model calls:** none.

## Objective

Measure the shape of the generated roster — element pair × aptitude × threat band × rarity — and refuse
to call a run successful because it completed.

This is the module that answers *"is what we generated any good?"*, and it exists because the honest
answer to that question is not visible one species at a time.

## Design

### 1. The failure it is built to catch

Ideal §4.8's corpses, and one in particular. **Diablo II's Hammerdin**: a build that was not
individually mis-tuned — every number in it was defensible — but the *distribution* of what the game
offered made one option dominate everything. A generated 904-species roster has exactly this exposure,
and worse: an LLM classifier with an uncorrected position bias produces a roster where one element is
30% of the table and two are 2%, and **every individual species still looks right**.

`option-permutation` prevents the bias. **This module is what proves it was prevented.** Neither is
sufficient alone: permutation without measurement is an untested claim.

### 2. What is measured

| Metric | Target | Source of the target |
|---|---|---|
| element-pair grid fill | **21 combinations × 12 aptitudes = 252 cells**, ~3.59 species/cell | ideal §6.3 — the Genshin/FGO safe band |
| single-element share | not the majority | ideal §6.2 ① — single typing gives 12.6/cell, FEH's failure zone |
| aptitude distribution | no aptitude below half the mean | there are 12 by construction; a starved one is dead content |
| threat band occupancy | **no empty rung**, no rung above ~25% | §3 below |
| rarity distribution | monotone decreasing across the ten rungs | a ladder where rung 7 is commoner than rung 4 is not a ladder |
| `basis` histogram | reported, not targeted | the honest coverage number |
| `unresolved` count per field | reported, and a threshold is a finding | high means a weak description |
| `family` size spread | no family holding more than ~10% | 19 families over 904 |
| posture balance | Force/Finesse/Bastion within a stated band | derived, so a skew here is an aptitude skew |

### 3. An empty threat rung is a finding, not a curiosity

`threat-band` §3 states the reason plainly: PvZ Fusion's captured stats cluster hard around stock
values, so a naive threshold table will pile most of the roster into two or three rungs and leave the
rest empty. **The ten-rung ladder is then a lie told in ten words.**

That is a `demon-threat.v1.json` retune, and this module is the instrument that says by how much. It
reports occupancy per rung and the score quantiles that would flatten it — **not a proposed table**,
because a suggested retune that nobody reasoned about is how a balance surface stops being owned.

### 4. Every metric declares its loop kind — seedsmith P3

> *Closed-loop*: detectable and the fix is machine-verifiable. *Open-loop*: detectable, not verifiable
> by machine — produces a review queue, never a pass.

| Closed-loop | Open-loop |
|---|---|
| grid fill, band occupancy, rarity monotonicity, distribution spreads, unresolved counts | *"is this species' element actually right?"*, the `threat-audit` disagreement queue, whether a family's members feel related |

**Without this split you get a green dashboard over prose nobody read** — the observation seedsmith was
founded on. An open-loop metric never contributes to a pass verdict.

### 5. A metric without a declared target is an opinion — seedsmith P2

Every target above lives in `data/tuning/demon-roster-targets.v1.json`, not in code, so a finding is
always *actual vs declared* and a disagreement is a diff. The item corpus reached 1,438 entries with
zero referential errors while nine of its 126 partitions were empty and nobody noticed for three
waves; that is what an undeclared target buys.

### 6. It runs on anchors, not on concrete rows

Deliberately placed before `species-generator` in the build order. **If the distribution is wrong, it
is wrong in the anchors**, and expanding 904 skewed anchors into concrete rows and a database just
moves the problem somewhere more expensive to fix.

## Commands

```powershell
python -m seedsmith demons metrics                  # the report
python -m seedsmith demons metrics --gate           # exit 1 on any closed-loop finding
python -m seedsmith demons metrics --grid           # the 21 x 12 occupancy matrix
python -m seedsmith demons metrics --queue          # the open-loop review queue
python -m pytest tools/seedsmith/tests/test_roster_metrics.py
```

## Project structure

```text
tools/seedsmith/seedsmith/metrics/demon_roster.py           the checks
data/tuning/demon-roster-targets.v1.json                    the declared targets
tools/seedsmith/tests/test_roster_metrics.py
```

Registers into the existing `metrics/registry.py` rather than standing beside it, so `report`'s CI
gate picks it up without a second entry point.

## Code style

Match `metrics/distribution.py`: each check returns typed findings with a severity and a loop kind;
no check prints.

## Testing strategy

| Test | Asserts |
|---|---|
| `skewed_element_distribution_is_a_finding` | synthetic roster, one element at 30% |
| `empty_threat_rung_is_reported_with_quantiles` | and does not propose a table |
| `non_monotone_rarity_is_a_finding` | rung 7 commoner than rung 4 |
| `open_loop_metric_never_contributes_to_pass` | mechanically, over the registry |
| `every_metric_has_a_declared_target_in_tuning` | P2, mechanically |
| `grid_reports_all_252_cells_including_zeros` | an empty cell is visible |
| `gate_exits_1_on_a_closed_loop_finding` | the CI gate gates |

Fixtures are synthetic rosters with a deliberately injected defect — the only way to prove a metric
would notice.

## Boundaries

**Always:** declare every target in tuning; label every metric's loop kind; report zeros; run on
anchors.

**Ask first:** changing a target (it is a balance judgement, and the whole point is that it is
explicit).

**Never:** let an open-loop metric produce a pass; propose a retuned table automatically; hide an empty
cell behind an average; gate on anything a human has not declared.

## Success criteria

- [ ] Every metric names a target that lives in tuning, proven mechanically.
- [ ] The 21×12 grid is reported in full, zeros included.
- [ ] An injected element skew is caught by test.
- [ ] Open-loop findings appear only in the review queue.
- [ ] `--gate` exits non-zero on a real closed-loop finding.
