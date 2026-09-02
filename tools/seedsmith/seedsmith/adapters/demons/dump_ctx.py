"""`DemonDumpCtx` — the loaded `corpus-dump` tree plus its `power-parse` classification, bundled
once so `metrics/corpus_coverage.py` (T1.10) doesn't re-read and re-parse the dump per metric.
Lives beside `preflight.py` rather than inside `metrics/`, because loading demon-specific JSON is
adapter knowledge, not something the generic metrics package should know how to do (the same split
`demon_coverage.py`'s own docstring states for its metric/adapter boundary).
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .power.model import PowerSeed
from .power.parse import parse_power_seed


@dataclass(frozen=True)
class DemonDumpCtx:
    dump_dir: Path
    manifest: dict
    seeds: "tuple[PowerSeed, ...]"

    @property
    def total(self) -> int:
        return len(self.seeds)


def load_demon_dump_ctx(dump_dir: Path) -> "DemonDumpCtx | None":
    """Returns None (never raises) when the dump tree isn't there or doesn't parse — the caller
    (a metric's `run`, via `needs={"demon_dump"}`) turns that into NOT_MEASURED, never a pass."""
    manifest_path = dump_dir / "_manifest.json"
    plant_path = dump_dir / "almanac" / "plant.json"
    zombie_path = dump_dir / "almanac" / "zombie.json"
    if not (manifest_path.exists() and plant_path.exists() and zombie_path.exists()):
        return None
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        rows = (json.loads(plant_path.read_text(encoding="utf-8"))
                + json.loads(zombie_path.read_text(encoding="utf-8")))
    except json.JSONDecodeError:
        return None

    seeds = tuple(
        parse_power_seed(
            side=r["side"], type_id=r["typeId"], stats_observed=r["statsObserved"],
            hp=r["hp"], attack=r["attack"], flavor_text=r["flavorInfo"])
        for r in rows
    )
    return DemonDumpCtx(dump_dir=dump_dir, manifest=manifest, seeds=seeds)
