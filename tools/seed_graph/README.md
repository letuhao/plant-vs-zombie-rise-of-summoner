# Seed reachability checker

```bash
python tools/seed_graph/check_reachability.py          # gate the corpus
python tools/seed_graph/test_reachability.py           # 16 tests, stdlib only
```

Python 3.9+, no third-party packages, no database.

## Why this exists next to `ItemSeedValidator`

The C# validator proves **referential integrity**: every id resolves, every tag is in the
vocabulary, every band is real. It was reporting `PASS — 1,438 entries, 0 errors` at the moment all
thirty sets in the corpus were impossible to complete.

Nothing was wrong with any reference. The problem was a reference that *did not exist*:
`item_set_member` is keyed on a specific base type, and every authored member named only a role. A
reference checker cannot see an absent row — there is nothing to look at. So this tool asks the
other question:

> Can a player actually get this, and can they finish it?

Referential integrity and reachability are independent properties. Neither implies the other, and
the corpus has now demonstrated that in the expensive direction.

## What it checks

| Code | Severity | Question |
|---|---|---|
| `SetUncompletable` | GAP | Does anything actually belong to this set? |
| `SetShortOfThreshold` | GAP | Are there enough members to reach the top bonus? |
| `Unobtainable` | GAP / NOTE | Does any drop table or recipe yield this? |
| `SlotUncovered` | GAP | Does every role/frame with items have a drop path? |
| `IngredientUnsatisfiable` | GAP | Does a gem exist carrying the family this word needs? |
| `RecipeInputUnobtainable` | GAP | Can a player get what this recipe spends? |
| `FeatureUnbound` | GAP | Does anything grant this whole feature? |
| `MaterialNeverSpent` | NOTE | A drop with no sink |
| `SetMemberFrameless` | NOTE | `item_set_member.frame` is NOT NULL |

**GAP** means content ships and cannot be reached or finished. **NOTE** means a shape worth a
glance that may be intentional — partial coverage of a kind is a NOTE, total absence is a GAP.

## The one trap worth knowing about

Acquisition comes in two shapes and conflating them gives false findings in both directions:

- **Specific** — a drop table names `gem.g3-001`. One row becomes reachable.
- **Categorical** — `{entryKind: equipment, role: girdle, frame: plant}` yields *any* base type in
  that role and frame. Six hundred base types are reachable this way and not one is named anywhere.

Check only for specific grants and all 740 base types report as unobtainable, which is alarming and
wrong. Check only categorically and you miss that 30 of 40 gems really are unreachable, because
inserts are granted by id and nothing grants them by category. `Acquisition` in `corpus.py` models
both, and `test_reachability.py` pins each direction.
