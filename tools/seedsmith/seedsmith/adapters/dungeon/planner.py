"""The dungeon planner (D1.9, spec-dungeon-seed-contract.md §4) — model-free, runs first.

**Built here:** cell enumeration from the adapter's own legality function (§3.6's (kind, climate)
rule), id minting from a high-water mark (§1's "every id is PLANNED... the planner mints
`<kind>.<cell>-<nnn>` from the cell and a sequence that continues from the high-water mark").

**Deliberately deferred, named rather than hidden (per this program's own house rule "an honest
gap costs a sentence"):** the motif-brief allocator (§4 step 2 — a disjoint partition of the motif
registry per cell with anti-motifs, which needs a dungeon motif registry that does not exist yet
— no `data/seed/dungeon/_registry/motifs*.json` is committed, and inventing one is a content-
authoring pass, not planner infrastructure) and the Hopcroft-Karp feasibility check (§4 step 5 —
`seedsmith.planner.feasibility.check_feasibility` already exists generically and is the right tool
once real slot-demand data exists to feed it). Both are wiring gaps against ALREADY-BUILT shared
infrastructure (`seedsmith/planner/feasibility.py`, `seedsmith/planner/demand.py`), not missing
algorithms — the remaining work is composing dungeon-specific demand data into them.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Mapping, Sequence


@dataclass(frozen=True)
class Cell:
    corpus: str
    dimension_values: "tuple[str, ...]"  # e.g. ("cache", "ice") for a room cell
    cell_key: str                        # the id-minting slug, e.g. "cache-ice"


def enumerate_cells(
    corpus: str,
    dimensions: "Sequence[tuple[str, tuple[str, ...]]]",  # [(dim_id, values), ...]
    legal: Callable[..., bool],
) -> "list[Cell]":
    """The full (legal) cell grid for one corpus — e.g. room's (kind x climate) 53 cells. `legal`
    is the adapter's own `legal_combinations()` result, called pairwise exactly as the Coverage
    metric does, so a cell this function yields is a cell the metric would also count."""
    if len(dimensions) == 1:
        dim_id, values = dimensions[0]
        return [Cell(corpus, (v,), v) for v in sorted(values)]

    if len(dimensions) != 2:
        raise NotImplementedError("enumerate_cells supports 1 or 2 dimensions today (every dungeon corpus is <=2-D)")

    (dim_a, values_a), (dim_b, values_b) = dimensions
    cells: "list[Cell]" = []
    for a in sorted(values_a):
        for b in sorted(values_b):
            if legal(dim_a, a, dim_b, b):
                cells.append(Cell(corpus, (a, b), f"{a}-{b}"))
    return cells


class IdMinter:
    """Mints `<namespace>.<cell>-<nnn>` ids, continuing from a supplied high-water mark per
    (namespace, cell) pair — never restarting at 1, which is exactly the four tracking-id defects
    the item build hit (seedsmith-map Appendix A row 6, cited by this module's own spec)."""

    def __init__(self, high_water_marks: "Mapping[tuple[str, str], int] | None" = None) -> None:
        self._marks: "dict[tuple[str, str], int]" = dict(high_water_marks or {})

    def next_id(self, namespace: str, cell_key: str) -> str:
        key = (namespace, cell_key)
        n = self._marks.get(key, 0) + 1
        self._marks[key] = n
        return f"{namespace}.{cell_key}-{n:03d}"

    def high_water_marks(self) -> "dict[tuple[str, str], int]":
        return dict(self._marks)


def plan_ids_for_cells(cells: "Sequence[Cell]", namespace: str, count_per_cell: "Mapping[str, int]", minter: IdMinter) -> "dict[str, list[str]]":
    """`cell_key -> [minted ids]`, `count_per_cell[cell.cell_key]` entries each — the planner's own
    §4 step 3 ("Ids minted per cell, sequence from the high-water mark"), independent of whatever
    fills each entry's content (that is the pipeline's job, §4 step ahead of this one)."""
    result: "dict[str, list[str]]" = {}
    for cell in cells:
        n = count_per_cell.get(cell.cell_key, 0)
        result[cell.cell_key] = [minter.next_id(namespace, cell.cell_key) for _ in range(n)]
    return result
