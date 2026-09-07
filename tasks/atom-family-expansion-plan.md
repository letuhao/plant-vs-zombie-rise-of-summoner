# Implementation Plan: atom-family-expansion

Source specs (both audited, corrected, and owner-decided 2026-09-08):
`docs/architecture/atom-family-expansion-map.md`,
`docs/architecture/atom-family-expansion/spec-tier-bands-coverage.md`,
`docs/architecture/atom-family-expansion/spec-battle-ruleset-curve-extension.md`.

## Overview

Close the 103 refusals `tools/FamilyExpandGen -- --check` reports today (`112 families read, 45 row(s)
emitted, 103 family(ies) refused`) via two independent modules: publish the missing
`channelWeightPermille` tunable coverage for 98 families (`tier-bands-coverage`), and add real
`BattleRuleset` curves for the 5 channels with no shipped baseline yet (`battle-ruleset-curve-extension`).
No new generation engine and no LLM step in either module — both are data-authoring/wiring work on top
of already-built tools (`seedsmith numerics rebalance`, `FamilyExpandGen`, `BattleModels.ChannelLadderFor`).

## Architecture decisions

Carried forward from the specs, not re-litigated here:

- **powerBand → `channelWeightPermille` formula**: reuses `seedsmith.numerics.MAGNITUDE_RATIO_PERMILLE`
  (`= 1750`, already a public exported constant — Task 1 imports it directly rather than re-declaring a
  mirrored copy, an improvement found during this plan's own read-only pass over the spec's original
  "mirror with citation" suggestion), anchored at `medium = 1000`. Reproduces 8 of the 14 already-
  published entries exactly; the other 6 (`fortitude`/`ferocity`/`resilience`/`mending`/`bulwark`/
  `savagery`) are additively left untouched, per spec §6's owner decision: **ship the formula as
  designed, accept the known band×op bias for now**, matching `tier-bands.v1.json`'s own "not a
  validated balance decision yet" posture. Named follow-up (not in this plan): revisit `More`-op
  families once real telemetry exists.
- **Calibration data for the 5 new curves**: named resolver (whoever executes Task 4) with a named
  default (ship the mechanism without numbers, leave those 5 families honestly refused) if real vanilla
  PvZ baseline data cannot be found — spec §4's own resolution, not re-decided here.
- **`RulesetVersion`**: Task 7 runs the golden/regression suite with the change staged and records an
  explicit bump-or-not verdict in `decisions.md`, per spec §6's own resolution.

## Dependency graph

**Renumbered mid-execution 2026-09-08**: Task 2 (fixing a real, load-bearing bug found while starting
the original Task 2) was inserted between the original Task 1 and Task 2, shifting everything after it
by one. See `tasks/atom-family-expansion-todo.md`'s own "A real, larger-than-planned discovery" section
for the full account — `FamilyExpandGen` hardcoded `tier-bands.v1.json` and never resolved "latest",
so the entire versioned-rebalance mechanism this plan depends on was silently disconnected from the
generator for every publish that has ever happened, including two from an earlier, separate effort
(`v2.json`/`v3.json`, both unread until this fix).

```
Phase 1: tier-bands-coverage                Phase 2: battle-ruleset-curve-extension
  Task 1 (formula)                            Task 5 (calibration data)
  Task 2 (fix FamilyExpandGen's version bug)    -> Task 6 (BattleModels curves)
    -> Task 3 (CLI wiring, needs Task 1)          -> Task 7 (FamilyExpandGen wiring)
      -> Task 4 (real-corpus run, needs 2+3)        -> Task 8 (registration + RulesetVersion)
                                             Task 9 (stale doc-comment fix) -- independent, any time
```

The two phases have **no dependency on each other** (capability map's own finding, verified this
session by reading `FamilyExpandGen`'s regeneration behavior directly — it fully rebuilds its output
set from scratch every run, so build order between the two phases is genuinely irrelevant). Phase 1
is sequenced first below purely for value-delivery reasons (cheap, high-yield, closes 98/103 with zero
calibration risk) — not a dependency, and not a gate. Building Phase 2 first, or interleaving both,
would work identically; nothing in either spec requires the order below.

## No pre-work gates in this plan

Checked against `planning-and-task-breakdown`'s "Gates vs. checkpoints" section, per `/plan`'s own
standing instruction. Two candidate gates were considered and both resolve to reversible defaults
already named in their specs, not hard stops:

- **Task 5's calibration data might not be findable.** Not a gate — spec §4 already names the default
  (ship the mechanism without numbers; those 5 families stay refused, honestly, exactly as they are
  today). Task 5 cannot block the plan; it can only narrow Task 6's own scope.
- **Task 8's `RulesetVersion` question might require owner sign-off if a golden moves.** Not a
  pre-work gate — it's a checkpoint that reviews Task 8's own output (did a golden move, yes/no) and
  only escalates if the answer is yes, which spec §6 states is not expected for a purely additive
  `channels` entry. Nothing upstream of Task 8 waits on this.

Every checkpoint below reviews work already done, per the skill's own distinction — none of them
block a phase from *starting*.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Real vanilla-PvZ calibration data for the 5 new channels can't be found or reconstructed | Medium — Phase 2 ships mechanism-only, 5 families stay refused | Named default already in spec §4: ship without numbers rather than guess. Not a blocker for Phase 1. |
| A golden moves when the new `channels` rows are added | Low (spec argues this shouldn't happen for a purely additive entry) | Task 8 runs the suite before publishing and records a `RulesetVersion` verdict either way |
| The 6-family band×op divergence (already accepted, not a new risk) causes visible confusion later | Low | Named explicitly in spec §6 and in Task 1's own test suite — discoverable, not silent |
| Concurrent sessions touching `data/seed/items/affix-families/*.json` or `tier-bands.v*.json` mid-plan | Low-medium (this repo runs many concurrent streams); **materialized once already** — v2/v3 already existed from an unrelated earlier effort, discovered via Task 2 | Task 4 and Task 8 both re-run `FamilyExpandGen --check` immediately before publishing to catch drift; `git status` checked before either publish step |
| **Realized, not hypothetical**: `FamilyExpandGen` hardcoded `tier-bands.v1.json`, disconnecting the whole versioned-rebalance mechanism from the generator | High if unfixed — Task 4 could never succeed | **Fixed as Task 2**, inserted mid-execution; see `tasks/atom-family-expansion-todo.md`'s "A real, larger-than-planned discovery" section for the full account, including 82 newly-visible refusals in 2 reason classes explicitly named as OUT of this plan's own scope |

## Task List

See `tasks/atom-family-expansion-todo.md` for the full breakdown with acceptance criteria and
verification steps.

### Phase 1: `tier-bands-coverage`
- [x] Task 1: `channel_weight_backfill.py` — the pure formula + `missing_channel_weights`
- [x] Task 2 (inserted mid-execution): fix `FamilyExpandGen`'s hardcoded `tier-bands.v1.json`
- [ ] Task 3: `write_set_file` + CLI `main()` (dry-run wiring)
- [ ] Task 4: Real-corpus run — publish + re-run `FamilyExpandGen` + verify

### Checkpoint: Phase 1 complete

### Phase 2: `battle-ruleset-curve-extension`
- [ ] Task 5: Calibration data-gathering (named resolver + default, per spec §4)
- [ ] Task 6: `BattleModels.cs` — 5 new `Base*` functions + `power-scale.v2.json` rows
- [ ] Task 7: `FamilyExpandGen`'s `FlatReferenceBase` — 5 new arms
- [ ] Task 8: `ssot-power-scale.md` §10 registration + `RulesetVersion` check + `decisions.md` entry
- [ ] Task 9: Fix the stale "not bindable yet" doc-comment in the 5 affected family JSON files

### Checkpoint: Phase 2 complete — plan complete
