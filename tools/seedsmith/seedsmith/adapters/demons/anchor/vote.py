"""`option-permutation`'s vote resolution (demon-seed module 6, spec-option-permutation.md §4-5).
Three samples in, one resolved value out — or `unresolved`, never the first value by default.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from typing import Sequence

#: Q25 — three-way voted, load-bearing fields. Adding or removing one is an "ask first" boundary
#: (moves the call budget) — pinned here as a frozenset so a change needs deliberate code, not a
#: silent list edit. `attackTempo` joined 2026-09-04 (demon-corpus-self-heal C1, owner-approved as
#: part of the full self-heal plan): the real 833-species audit found it collapsed to a single
#: value (entropy 0.00) — the one pipeline (`kit-shape`) that had never been wired into voting or
#: permutation at all, unlike every other classified field.
VOTED_FIELDS = frozenset({"elementPrimary", "aptitudePrimary", "rarity", "threatBand", "deployMode", "attackTempo"})


@dataclass(frozen=True)
class VoteResult:
    value: "str | None"        # None only when confidence == "unresolved"
    confidence: str            # "high" (3-0) | "split" (2-1) | "unresolved" (1-1-1)
    minority: "str | None"     # the minority value, recorded — only set when confidence == "split"


def resolve_vote(values: Sequence[str]) -> VoteResult:
    """Exactly three samples in. A 1-1-1 split is a genuine ambiguity signal, not a default —
    it never silently takes `values[0]` (spec §4's explicit warning: "the obvious way to build
    this wrong")."""
    if len(values) != 3:
        raise ValueError(f"resolve_vote needs exactly 3 samples, got {len(values)}")

    counts = Counter(values)
    ranked = counts.most_common()
    top_value, top_count = ranked[0]

    if top_count == 3:
        return VoteResult(value=top_value, confidence="high", minority=None)
    if top_count == 2:
        minority_value = next(v for v in values if v != top_value)
        return VoteResult(value=top_value, confidence="split", minority=minority_value)
    # 1-1-1: every value distinct, top_count == 1.
    return VoteResult(value=None, confidence="unresolved", minority=None)


@dataclass(frozen=True)
class VoteRecord:
    """One resolved vote, kept for disagreement reporting (spec §5)."""
    species_id: str
    side: str
    field: str
    result: VoteResult


def disagreement_rate(records: Sequence[VoteRecord]) -> dict:
    """Per field, per side: the share of votes that were NOT 3-0 (i.e. `split` or `unresolved`).
    Reported separately per side because a description can be clear for zombies and vague for
    plants (spec §5) — a single pooled rate would hide that."""
    totals: "dict[tuple[str, str], int]" = {}
    disagreed: "dict[tuple[str, str], int]" = {}
    for r in records:
        key = (r.field, r.side)
        totals[key] = totals.get(key, 0) + 1
        if r.result.confidence != "high":
            disagreed[key] = disagreed.get(key, 0) + 1

    report: "dict[str, dict]" = {}
    for (field, side), total in totals.items():
        report.setdefault(field, {})[side] = disagreed.get((field, side), 0) / total
    return report
