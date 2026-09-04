"""seedsmith.adapters.items.setgen.cells — the distinctness cell, and why it is not the capability.

⛔ **The acceptance bar this replaces measured the wrong axis.** The spec previously read *"if fewer
than ~40 of the 60 capabilities are used, the sets are reskins."* At ~904 species sets, **passing
that means 904 / 40 = 22.6 sets per capability** — worse than the 15-to-1 the same section already
flags as the honest problem. A gate whose pass condition is worse than the concern that motivated it
is measuring the wrong axis.

The repo's own research settles it
([docs/research/game-design/03-roster-scale.md](../../../../../../docs/research/game-design/03-roster-scale.md) §2):
Pokémon keyed on **type alone** is 154 cells, median 3, max 75; keyed on **type + ability set** it is
730 cells, median 1, max 7, 68% singletons. *"Type is the coarse axis and was never doing the
distinctness work."* **The capability is the type here.**

> ⭐ **Cell key = `(capability, sorted multiset of the stat families granted at every threshold above
> the lowest)`.** Median occupancy ≤ 2; singleton share and max are reported beside it.

Capability usage stays **measured and reported** — it is the diagnostic that shows a picker
collapsing onto the three or four most flattering capabilities — but it is no longer the gate,
because passing it proves nothing about distinctness.
"""
from __future__ import annotations

import statistics
from collections import Counter
from dataclasses import dataclass

#: A threshold row's stat families, whatever shape the corpus wrote them in. The shipped sets use
#: `atoms: [{family, powerBand}]`; a freshly generated draft uses `families: [id]`. Both are read
#: rather than one being normalised at the corpus boundary, because a metric that only understands
#: the shape it emitted itself is a metric that stops working the moment the corpus is real.
def threshold_families(threshold: dict) -> "tuple[str, ...]":
    atoms = threshold.get("atoms")
    if isinstance(atoms, list):
        out = [a.get("family") for a in atoms if isinstance(a, dict) and a.get("family")]
        if out:
            return tuple(out)
    families = threshold.get("families")
    if isinstance(families, list):
        return tuple(f for f in families if isinstance(f, str))
    return ()


def threshold_capability(threshold: dict) -> "str | None":
    cap = threshold.get("capability")
    if isinstance(cap, dict):
        family = cap.get("family")
        variant = cap.get("variant")
        if family:
            return f"{family}.{variant}" if variant else family
    if isinstance(cap, str):
        return cap
    return None


def cell_key(entry: dict) -> "tuple[str, tuple[str, ...]] | None":
    """`(capability, sorted higher-threshold family multiset)`, or `None` for a set with no
    thresholds at all — which is a different defect (`Linkage/SetCompletability` owns it) and must
    not be silently counted as an occupied cell here."""
    thresholds = [t for t in (entry.get("thresholds") or []) if isinstance(t, dict)]
    if not thresholds:
        return None
    ordered = sorted(thresholds, key=lambda t: t.get("pieces", 0))
    capability = threshold_capability(ordered[0]) or "(none)"
    higher: "list[str]" = []
    for t in ordered[1:]:
        higher.extend(threshold_families(t))
    return capability, tuple(sorted(higher))


@dataclass(frozen=True)
class CellReport:
    cells: int
    population: int
    median: float
    maximum: int
    singletons: int
    capability_usage: "tuple[tuple[str, int], ...]"

    @property
    def singleton_share_permille(self) -> int:
        """Integer per-mille — no float share is ever compared against a threshold here."""
        if self.cells == 0:
            return 0
        return (self.singletons * 1000) // self.cells

    def within(self, median_max: int) -> bool:
        return self.cells > 0 and self.median <= median_max


def cell_report(entries: "list[dict]") -> CellReport:
    occupancy: "Counter[tuple[str, tuple[str, ...]]]" = Counter()
    capabilities: "Counter[str]" = Counter()
    counted = 0
    for entry in entries:
        key = cell_key(entry)
        if key is None:
            continue
        occupancy[key] += 1
        capabilities[key[0]] += 1
        counted += 1
    counts = sorted(occupancy.values())
    return CellReport(
        cells=len(counts),
        population=counted,
        median=statistics.median(counts) if counts else 0.0,
        maximum=max(counts) if counts else 0,
        singletons=sum(1 for c in counts if c == 1),
        capability_usage=tuple(sorted(capabilities.items(), key=lambda kv: (-kv[1], kv[0]))),
    )
