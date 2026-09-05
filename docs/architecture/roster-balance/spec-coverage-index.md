# Spec: `coverage-index` (RB3)

**Module id:** `coverage-index` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 3 of 6**
**Depends on:** RB1 `distribution-stats`, RB2 `balance-policy` · **Model calls: none**

## Objective

**Emit the index: every coverage cell with its target and actual occupancy.** This is the artefact
the whole program exists to produce — the *distribution direction* the current pipeline is missing.
Downstream stages stop sampling blind and start filling a known shape.

Owner, 2026-09-05: *"the indexing will ensure the diversity before we go to current loop"* — the
index is what makes that guarantee, and it is guaranteed **by construction**, not by hoping a model
spreads out.

## Design

### Why an index fixes what tuning never could

Measured the same day, over a real full round: the action pipeline used **52 of 98** atom families,
its top-5 families were **34.8%** of all picks, and **46.8%** of accepted bundles were distinct. Five
rounds of prompt and aggregation work moved the failure rate but never the coverage, because
**coverage is not something sampling can be asked to guarantee.** An enumerated index cannot
under-cover: a cell either has its target occupancy or it is in the under-filled set.

### What one index entry is

```jsonc
{
  "cellId": "aptitude=Onslaught|element=earth|posture=Force",
  "axes": { "aptitudePrimary": "Onslaught", "elementPrimary": "earth", "posture": "Force" },
  "actual": 127,
  "target": 4,
  "state": "over",            // "under" | "at" | "over"
  "gap": -123
}
```

`cellId` is a deterministic string built from the grid axes in the policy's declared order — stable
across runs, usable as a sort key and as a join key by RB4/RB6.

### Target occupancy is derived, never assigned

`target = round(rosterSize / cellCount)`, clamped into RB2's density band. Today: `841 / 234 = 3.59`
→ target 4 per cell. **No cell target is ever hand-written**, so the index re-derives correctly when
the roster grows — the owner's binding *"we don't care how many real species and family."*

**Uniform targets are the deliberate default**, not a simplification: a per-cell weighting scheme is
exactly the kind of hand-tuned surface that drifts and that nobody re-derives. **What would overturn
it:** a decided design reason some cells should be denser (e.g. a signature archetype), which becomes
a weight column in RB2's tuning, not a special case here.

### It names the tails, because a pass that evaluated nothing is not a pass

The index always reports, explicitly:

- **under-filled** cells and by how much — what RB6 directs generation toward;
- **empty** cells as a distinct class from merely thin — today **17 of 78** on the coarse grid, and
  an empty cell is a stronger signal than an under-target one;
- **over-crowded** cells — today `(Onslaught, earth)` at **127 against a target of 4**, which is where
  RB4 will look for re-assignable rows.

This mirrors A-S5's own rule that `NOT_MEASURED` stays distinct from a pass.

### Determinism is load-bearing here

Every collection is sorted on `cellId`; nothing iterates a dict or a filesystem. Two runs over an
unchanged corpus emit a byte-identical index, and the index carries a `corpusHash` so a downstream
consumer can prove which corpus it was built from. **The program's parallel workers make this
non-negotiable** — a worker pool consuming a non-deterministic index would produce unreproducible
content, and byte-identical replay is a gate this repo currently passes.

## Commands

```powershell
python -m seedsmith roster index                    # emit the index
python -m seedsmith roster index --under            # only the under-filled set (what needs work)
python -m pytest tools/seedsmith/tests/test_coverage_index.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/roster/index/derive.py     new — cells, targets, states
tools/seedsmith/seedsmith/adapters/roster/generate_index.py   new — CLI entry point
data/seed/roster/_index/coverage-<round>.json                 new — the emitted index
tools/seedsmith/tests/test_coverage_index.py                  new
```

## Code style

```csharp
// cellId joins the grid axes in the POLICY's declared order, never dict order -- it is a sort key and
// a join key for RB4/RB6, so a reordering would silently repartition the index.
```

```python
def cell_id(axes: "Mapping[str, str]", grid: "Sequence[str]") -> str:
    return "|".join(f"{a}={axes[a]}" for a in grid)   # policy order, never dict order
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_grid_cell_appears_even_when_empty` | 234 entries for the real grid, not 61 — an absent cell is the finding |
| `target_is_derived_from_roster_size_never_hard_coded` | doubling a fixture roster doubles the targets with no code change |
| `target_is_clamped_into_the_policy_density_band` | a roster small enough to push target below the band floor clamps, and says so |
| `empty_is_a_distinct_state_from_under` | both appear, and are not merged |
| `over_crowded_cells_are_reported_with_their_gap` | `(Onslaught, earth)` reports 127 against target 4 |
| `cell_id_is_built_in_policy_order` | reordering the policy's grid list changes the ids, provably |
| `two_runs_over_unchanged_input_are_byte_identical` | hashed, not eyeballed |
| `the_index_carries_the_corpus_hash_it_was_built_from` | a consumer can prove provenance |
| `an_illegal_axis_value_never_creates_a_cell` | `posture: "unresolved"` produces no cell of its own |
| `PLANTED_VIOLATION_a_cell_target_written_by_hand_is_refused` | targets are derived only |
| `the_real_corpus_produces_234_cells_at_the_shipped_policy` | the map's own measured number, re-derived |

## Boundaries

**Always:** enumerate every cell including empty ones; derive targets; sort on `cellId`; stamp the
`corpusHash`.

**Ask first:** introducing per-cell target weights — that is a design surface, and it belongs in RB2's
tuning if it happens at all.

**Never:** hand-write a target; create a cell for an illegal axis value; emit a collection in
non-deterministic order; call a model.

## Success criteria

- [ ] Emits 234 cells for the real corpus at the shipped policy, with 17+ empty cells named.
- [ ] `(Onslaught, earth, *)` is reported over-crowded against its derived target.
- [ ] Targets change automatically when the roster grows; no code edit, no hand-written number.
- [ ] Byte-identical across two runs, with a `corpusHash` stamped.
- [ ] Empty, under, at and over are four distinguishable states in the emitted index.
