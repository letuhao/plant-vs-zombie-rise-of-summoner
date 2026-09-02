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
    return APTITUDE_POSTURE[aptitude_primary]


def derive_pure(aptitude_primary: str, aptitude_secondary: str) -> bool:
    """Q2: both aptitudes share a posture (or there is no secondary) -> pure. A flag, never a
    rejection (spec §4's `pure-flag` row)."""
    if aptitude_secondary == "none":
        return True
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
    """
    from .schema import VARIANTS

    bands = _load_variant_count_bands(version)
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
