# Spec: `rebalance-plan` (RB4)

**Module id:** `rebalance-plan` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 4 of 6**
**Depends on:** RB3 `coverage-index` · **Model calls: none**

## Objective

**Turn the index into an ordered, reproducible correction plan.** Owner, 2026-09-05: *"the
deterministic function must rebalance it by add new or change some characteristics."* Those are
exactly the two move types this module plans — and it **proposes only**. Applying is RB5, behind a
separate gate, because mutating 841 committed rows on a computed plan is not something to do as a
side effect of measuring.

## Design

### Two move types, and a strong preference between them

| Move | What it does | Cost |
|---|---|---|
| **`add`** | Request a new species with the characteristics of an under-filled cell | Cheap and safe — nothing existing changes |
| **`reassign`** | Change one characteristic of an existing row, moving it between cells | Expensive and risky — the row already has an identity, art, tuning and possibly player exposure |

⛔ **CORRECTED 2026-09-06 — the roster is CLOSED, so `reassign` is the PRIMARY tool.** An earlier
draft said *"`add` is strongly preferred"*; checking the data is what found that wrong.
`data/seed/demons/_dump/almanac/` holds **904 rows dumped from the real game**
(`pvz-fusion-almanac-3.6.1`), and every species row carries a `gameTypeId` into that set. **A species
cannot be invented — it must be a creature that exists in PvZ Fusion.** So `add` is renamed
**`classify`** and is capped at the **~138 almanac rows not yet in the corpus**; everything beyond
that is reassignment. `classify` still runs first — it is pure gain, rewrites nothing, and may fill
empty cells on its own — it simply cannot carry the whole correction.

### Why reassignment is legitimate, and the one cost the owner accepted

`aptitudePrimary`, `posture`, `elementPrimary` are **not facts about PvZ.** The game has no notion of
an "Onslaught aptitude" or a "Bastion posture" — they are RPG-layer classifications this project
assigns, which is why each carries `_provenance.confidence` and `basis`. `(Onslaught, earth)` holding
127 species does not mean the game contains 127 earth brawlers; it means the classifier funnelled
creatures there. Re-classifying is therefore **designing our own layer better**, squarely inside
`CLAUDE.md`'s rule that every RPG feature lives in the RPG layer.

**Owner decision, 2026-09-06: *"hit the evenness targets whatever it takes."*** Reaching the targets
needs rows whose `basis` is `stated` or `observed` — grounded in the almanac's own description — not
only the `inferred` ones. That is permitted, and it is **recorded, never silent**: such a move is
stamped `divergesFromAlmanacBasis: true`, so a deliberate divergence stays auditable and reversible
instead of quietly baked in.

### Reassignment ranks candidates by measured softness

⛔ **The real confidence vocabulary, verified against the corpus 2026-09-06 — an earlier draft ranked
on `confidence == "low"` and there are ZERO such rows.** The values that actually occur are
`high` · `split` · `unresolved` · `deterministic-fallback`. Softest first:

1. **`unresolved`** — a vote outcome that leaked into real data (12 rows, `posture`). Not a
   classification at all; moving one is a pure correction.
2. **`deterministic-fallback`** — the classifier gave up and defaulted (10 rows, `threatBand`).
3. **`split`** — a 2-1 vote, and `_provenance.minorityValues` already records the runner-up. **The
   largest pool: 218 on `aptitudePrimary`, 127 on `rarity`, 87 on `elementPrimary`.**
4. **`basis: "inferred"`** — derived, never stated by a source (169 rows).
5. **`high` + `stated`/`observed`** — permitted per the owner's decision, taken **last**, always
   stamped `divergesFromAlmanacBasis`.

Measured soft headroom before step 5 is needed: **342 rows on `aptitudePrimary` (40.7%)**, 267 on
`rarity`, 235 on `elementPrimary`, 170 on `threatBand`, 169 on `posture`.

**A `split` move prefers the recorded minority value** over an invented destination — the classifier
already stated its second choice, and honouring it beats inventing a third.

### Determinism, and why it matters more here than anywhere else

Two runs over the same index emit the same plan, byte for byte. Ordering is by `(cellId, speciesId)`,
never by dict, filesystem or completion order. This is not tidiness: RB5 applies this plan to real
content, and a plan that reorders between runs cannot be reviewed, diffed, or safely re-run after a
partial apply.

Where a genuine tie remains (two equally weak rows), it breaks on a **seeded hash of
`(cellId, speciesId)`** — reproducible and independent of input order, the same convention
`Instantiator`'s own rolls already use. Never "the first one found".

### The plan states its own cost and its own limits

Every plan reports: how many `add`s and `reassign`s it proposes, which cells it **cannot** fix and
why, and the projected post-plan evenness per axis. **A plan that would not actually reach RB2's
thresholds must say so** rather than emitting moves that look like progress — the same discipline
that made the smoke-batch report its real 13.2% instead of rounding toward the gate.

## Commands

```powershell
python -m seedsmith roster plan                     # propose, print the summary
python -m seedsmith roster plan --explain <cellId>  # why this cell got these moves
python -m pytest tools/seedsmith/tests/test_rebalance_plan.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/roster/plan/derive.py      new — move selection + ranking
tools/seedsmith/seedsmith/adapters/roster/plan/cost.py        new — projected post-plan statistics
tools/seedsmith/seedsmith/adapters/roster/generate_plan.py    new — CLI entry point
data/seed/roster/_plans/rebalance-<round>.json                new — the emitted plan
tools/seedsmith/tests/test_rebalance_plan.py                  new
```

## Code style

```python
# A reassign must cite its evidence. A row whose axis value is high-confidence AND authored is a
# DECISION -- the plan proposes new content rather than overwriting somebody's call.
def reassign_reason(row: Mapping, axis: str) -> "str | None":
    prov = row.get("_provenance") or {}
    if (prov.get("confidence") or {}).get(axis) == "low":      return "low-confidence"
    if row.get("basis") == "inferred":                          return "inferred-not-authored"
    if axis in (prov.get("minorityValues") or {}):              return "vote-split"
    return None                                                 # -> not a candidate, at all
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_under_filled_cell_produces_add_moves_by_default` | `add` is the preferred move, proven not asserted |
| `PLANTED_VIOLATION_a_reassign_without_a_cited_reason_is_refused` | the module raises, naming the row and axis |
| `a_high_confidence_authored_row_is_never_a_reassign_candidate` | even when it is the only occupant of a crowded cell |
| `reassign_candidates_are_ranked_low_confidence_first` | the documented order, over a planted mixed fixture |
| `ties_break_on_a_seeded_hash_not_input_order` | shuffling the input rows produces the identical plan |
| `two_runs_over_one_index_are_byte_identical` | hashed |
| `the_plan_reports_cells_it_cannot_fix` | an unfixable cell is named with a reason, never silently dropped |
| `the_plan_reports_projected_post_plan_evenness` | and the test recomputes it independently |
| `a_plan_that_misses_the_policy_thresholds_says_so` | never emits moves that merely look like progress |
| `the_module_never_writes_to_the_species_corpus` | filesystem assertion — proposing only |
| `the_real_index_produces_a_plan_naming_the_Onslaught_earth_crowding` | run against real data, not only fixtures |

## Boundaries

**Always:** prefer `add`; cite a reason for every `reassign`; break ties on a seeded hash; report
unfixable cells and projected outcomes.

**Ask first:** proposing a `reassign` policy that would touch authored, high-confidence rows — that
is a content-ownership decision, not a balance one.

**Never:** write to the species corpus (that is RB5, behind its own gate); reassign without evidence;
break a tie on input order; invent a species name, flavour or identity — the plan describes
*characteristics wanted*, and the existing `demon-seed` generator authors the actual species.

## Success criteria

- [ ] Produces a plan against the real index that names `(Onslaught, earth, *)` as over-crowded and
      proposes `add` moves for the empty cells.
- [ ] Every `reassign` in the emitted plan cites `low-confidence`, `inferred-not-authored`, or
      `vote-split`; a planted evidence-free reassign is refused.
- [ ] Shuffling the input row order produces a byte-identical plan.
- [ ] The plan reports projected post-plan evenness per axis, and flags when it still misses RB2.
- [ ] Zero writes to `data/seed/demons/species/**`, asserted by test.
