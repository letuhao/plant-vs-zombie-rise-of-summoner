"""seedsmith.metrics.coverage — Coverage/EmptyPartition (spec-analytics.md §2.1).

The check that would have caught all nine known-empty item partitions on day one: set difference
between allocated partitions and partitions holding >=1 entry. O(n), and its absence in the
agentic build cost three authoring waves.

"Allocated" is adapter knowledge, not corpus knowledge (corpus only ever sees partitions that
already hold something) — so this metric reads the allocated set from the adapter's own
`registries().vocabularies["partitions"]`, a convention any adapter can publish without the core
knowing what a partition even represents semantically.
"""
from __future__ import annotations

from .model import Ctx, Finding, Loop, Metric, Severity


class EmptyPartitionMetric(Metric):
    id = "Coverage/EmptyPartition"
    family = "Coverage"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus", "adapter"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        allocated = ctx.adapter.registries().vocabularies.get("partitions", frozenset())
        occupied = ctx.corpus.partitions
        empty = sorted(allocated - occupied)
        return [
            Finding(
                metric=self.id,
                severity=Severity.GAP,
                subject=partition,
                message=f"partition '{partition}' is allocated but holds no entries",
                evidence={"allocated": True, "occupiedEntryCount": 0},
                assertion=f"len(corpus.by_partition({partition!r})) > 0",
                remedy="planner: schedule generation for this partition",
            )
            for partition in empty
        ]
