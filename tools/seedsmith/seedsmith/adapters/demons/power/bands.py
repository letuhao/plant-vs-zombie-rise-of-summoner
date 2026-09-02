"""`threat-band` (demon-seed module 4, spec-threat-band.md) — turns power-parse's score into one
of ten threat-noun rungs, and turns that rung into a `Theta` offset. Table load + lookup only; the
ladder is a table because the captured stat distribution is lumpy, not smooth: a fitted curve puts
most of the roster in two rungs. See spec-threat-band.md section 3.

TUNING_KEY = "demon-threat.v1"
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .model import PowerSeed

TUNING_DIR = Path(__file__).resolve().parents[6] / "data" / "tuning"


class UnoccupiedRung(ValueError):
    """A histogram request found a rung with zero occupants — reported, not hidden."""


@dataclass(frozen=True)
class ThreatThreshold:
    rung: int
    id: str
    max_score: "int | None"   # None marks the open-ended top rung
    theta_offset: int


@dataclass(frozen=True)
class ThreatTuning:
    version: int
    thresholds: "tuple[ThreatThreshold, ...]"       # rung-ordinal ascending
    toughness_milli: int
    damage_milli: int
    inferred_default_rung: int

    @classmethod
    def load(cls, version: "int | str" = 1, *, tuning_dir: Path = TUNING_DIR) -> "ThreatTuning":
        path = tuning_dir / f"demon-threat.v{int(version)}.json"
        raw = json.loads(path.read_text(encoding="utf-8"))
        thresholds = tuple(
            ThreatThreshold(rung=t["rung"], id=t["id"], max_score=t["maxScore"], theta_offset=t["thetaOffset"])
            for t in sorted(raw["thresholds"], key=lambda t: t["rung"])
        )
        return cls(
            version=raw["version"],
            thresholds=thresholds,
            toughness_milli=raw["scoreWeights"]["toughnessMilli"],
            damage_milli=raw["scoreWeights"]["damageMilli"],
            inferred_default_rung=raw["inferredDefaultRung"])

    def threshold_for_rung(self, rung: int) -> ThreatThreshold:
        for t in self.thresholds:
            if t.rung == rung:
                return t
        raise KeyError(f"no rung {rung} in threat tuning v{self.version}")


def score(toughness: "int | None", damage: "int | None", tuning: ThreatTuning) -> "int | None":
    """`(toughness * toughnessMilli + damage * damageMilli) / 1000`, widened before multiplying,
    divided by 1000 exactly once, last (CLAUDE.md rules 3-4). A species with only one of the two
    signals uses that one at full weight — a missing signal must not read as weakness (spec §5)."""
    if toughness is None and damage is None:
        return None
    t = int(toughness) if toughness is not None else 0
    d = int(damage) if damage is not None else 0
    # Python ints are already arbitrary-width; the C# port of this module is the one that must
    # widen explicitly — this line is the same shape as the future `(long)t * milli` there.
    return (t * tuning.toughness_milli + d * tuning.damage_milli) // 1000


def rung_for_score(the_score: int, tuning: ThreatTuning) -> ThreatThreshold:
    """Monotonic in score by construction: thresholds are walked in ascending rung order, and the
    first whose `max_score` is not exceeded wins; `None` (the open top rung) always matches."""
    for t in tuning.thresholds:
        if t.max_score is None or the_score <= t.max_score:
            return t
    raise AssertionError("unreachable: the top rung's max_score is always None")


def classify(seed: "PowerSeed", tuning: ThreatTuning) -> "ThreatThreshold | None":
    """`observed`/`stated` seeds score and rung directly. `inferred`/`blocked` seeds get no score
    here — `inferred` gets its rung from `classify-pipelines` reading the lore (Q26); `blocked`
    takes `tuning.inferred_default_rung` at the call site, flagged, never silently rung 1 (spec §6)."""
    if seed.basis not in ("observed", "stated"):
        return None
    s = score(seed.toughness, seed.damage, tuning)
    if s is None:
        return None
    return rung_for_score(s, tuning)


def histogram(rungs: "list[int]", tuning: ThreatTuning) -> dict:
    """Rung id -> occupant count, including **zero-occupant rungs** — an unoccupied rung must be
    visible, never silently absent from the report (spec's own histogram command)."""
    counts = {t.id: 0 for t in tuning.thresholds}
    by_rung = {t.rung: t.id for t in tuning.thresholds}
    for r in rungs:
        counts[by_rung[r]] += 1
    return counts
