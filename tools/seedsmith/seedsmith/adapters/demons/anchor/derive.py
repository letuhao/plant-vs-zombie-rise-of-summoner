"""DERIVED anchor fields and deterministic post-processing (spec-classify-pipelines.md §4).
`posture` and `pure` are computed, never authored — a model that can write them can contradict its
own primary answer (anchor-contract boundaries). `variant-count` is clamped deterministically from
`rarity`, never left to the model to decide how many.
"""
from __future__ import annotations

import json
from pathlib import Path

from .schema import APTITUDE_POSTURE

VARIANT_COUNT_TUNING_DIR = Path(__file__).resolve().parents[6] / "data" / "tuning"


def derive_posture(aptitude_primary: str) -> str:
    """`aptitude_primary == "unresolved"` is the same real, documented outcome
    `clamp_variant_count` guards against (spec-classify-pipelines.md §4: two failed repairs, then
    the field is unresolved and reported) — a posture cannot be derived from an input that was
    never itself resolved, so this propagates the same "unresolved" string rather than crashing or
    guessing. Found by the same real T2.11 run (2026-09-02) that found the `clamp_variant_count`
    gap, one species later."""
    if aptitude_primary not in APTITUDE_POSTURE:
        return "unresolved"
    return APTITUDE_POSTURE[aptitude_primary]


def derive_pure(aptitude_primary: str, aptitude_secondary: str) -> bool:
    """Q2: both aptitudes share a posture (or there is no secondary) -> pure. A flag, never a
    rejection (spec §4's `pure-flag` row).

    `pure` has no "unresolved" representation of its own (it is strictly boolean on both the
    Python schema and the C# `AnchorRow.Pure` reader) — when either aptitude is unresolved,
    `False` is written as an explicit, documented placeholder, never a guess presented as real:
    the species is already flagged unresolved through `aptitudePrimary`/`posture` themselves, and
    nothing here claims `pure` was actually determined.
    """
    if aptitude_primary not in APTITUDE_POSTURE:
        return False
    if aptitude_secondary == "none":
        return True
    if aptitude_secondary not in APTITUDE_POSTURE:
        return False
    return APTITUDE_POSTURE[aptitude_primary] == APTITUDE_POSTURE[aptitude_secondary]


def _load_variant_count_bands(version: "int | str" = 1) -> dict:
    path = VARIANT_COUNT_TUNING_DIR / f"demon-variant-count.v{int(version)}.json"
    return json.loads(path.read_text(encoding="utf-8"))["countByRarity"]


def clamp_variant_count(variants: "list[str]", rarity: str, *, version: "int | str" = 1) -> "list[str]":
    """The `variant-count` validator (spec §4): the COUNT comes from `rarity`'s band, never from
    the model. Truncates deterministically (keeps the model's own first N, preserving its
    ordering) or extends deterministically (appends the lowest-ordinal VARIANTS values not
    already present, so two runs over the same input produce the identical extension — 'normal'
    is always first to be added since every species can plausibly have it).

    `rarity == "unresolved"` is a real, documented outcome (spec-classify-pipelines.md §4: "two
    repairs, then the field is unresolved and reported") — a vote's own 1-1-1 split, never
    invented here. There is no band to clamp against without a resolved rarity, so this is a
    no-op rather than a guess: the model's own `variants` pass through unchanged, exactly as
    `emit.py` already writes an unresolved field explicitly rather than defaulting it. Found by
    a real classification run (`demons run resume`, 2026-09-02) crashing the whole run on the
    first species whose rarity vote didn't settle — the deterministic clamp's own precondition
    (a resolved rarity) was never actually guaranteed by anything upstream of it.
    """
    from .schema import VARIANTS

    bands = _load_variant_count_bands(version)
    if rarity not in bands:
        return list(dict.fromkeys(variants))  # de-dupe only; no band to clamp against
    lo, hi = bands[rarity]

    result = list(dict.fromkeys(variants))  # de-dupe, preserve order
    if len(result) > hi:
        return result[:hi]
    if len(result) < lo:
        for candidate in VARIANTS:
            if len(result) >= lo:
                break
            if candidate not in result:
                result.append(candidate)
    return result
