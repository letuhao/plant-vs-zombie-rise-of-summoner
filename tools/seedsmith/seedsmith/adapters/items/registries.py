"""seedsmith.adapters.items.registries — reads `data/seed/items/_registry/*.json` fresh on every
call. Registry facts are read, never transcribed (tasks/seedsmith-plan.md verification
discipline) — the one exception is the allocated-partitions ledger, which is a DERIVED fact
sourced from `tools/ItemSeedValidator --list-partitions` (see `_registry_snapshot/`'s own
docstring for why that one is snapshotted rather than re-read live).
"""
from __future__ import annotations

import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[5]
REGISTRY_DIR = REPO_ROOT / "data" / "seed" / "items" / "_registry"
SNAPSHOT_PATH = Path(__file__).resolve().parent / "_registry_snapshot" / "allocated_partitions.json"

_REGISTRY_FILES = ("bands.v1.json", "core.v1.json", "naming.v1.json", "tags.v1.json",
                   "classes.v1.json", "themes.v1.json")


def _load(name: str) -> dict:
    path = REGISTRY_DIR / name
    return json.loads(path.read_text(encoding="utf-8"))


def load_versions() -> dict[str, int]:
    """`registryVersion` per file, read fresh — not hardcoded. Measured 2026-08-23: naming and
    tags are at v4, classes at v3, the rest at v1 — a single assumed constant would already be
    wrong for half of them."""
    return {name.removesuffix(".v1.json"): _load(name)["registryVersion"]
           for name in _REGISTRY_FILES}


def load_vocabularies() -> dict[str, frozenset[str]]:
    core = _load("core.v1.json")
    tags = _load("tags.v1.json")
    classes = _load("classes.v1.json")

    roles = frozenset(r["roleId"] for r in core["roles"]["list"])
    commander_roles = frozenset(r["roleId"] for r in core["roles"]["commanderOnly"])
    elements = frozenset(e["id"] for e in core["elements"]["concrete"]) | {core["elements"]["omni"]["id"]}
    rarities = frozenset(r["id"] for r in core["rarity"]["ladder"])
    tag_ids = frozenset(t["id"] for t in tags["tags"])
    # NOT `classLadders.keys()` (armour/weapon/offhand/jewel/standard) — those are LADDER names,
    # never a literal `class` field value. A base-type's real `class` value is a per-frame rung
    # id nested two levels down (classLadders[ladder][frame][i].id, e.g. "cloth", "leather") —
    # found only by loading a real entry and checking (base-types/footing/humanoid/a.json has
    # class="cloth"), after the ladder-name version produced a 100%-missing pairwise finding for
    # every (dimension, class) pair — a confidently wrong metric, not a real gap.
    class_values = frozenset(
        rung["id"]
        for ladder in classes["classLadders"].values()
        for frame_key in ("humanoid", "plant")
        for rung in ladder.get(frame_key, [])
    )

    snapshot = json.loads(SNAPSHOT_PATH.read_text(encoding="utf-8"))
    partitions = frozenset(snapshot["partitionKind"].keys())
    power_band = frozenset(_load("bands.v1.json")["powerBand"]["enum"])

    return {
        "role": roles | commander_roles,
        "frame": frozenset({"humanoid", "plant", "hybrid"}),
        "band": frozenset({"a", "b"}),
        "powerBand": power_band,
        "element": elements,
        "rarity": rarities,
        "tags": tag_ids,
        "class": class_values,
        "partitions": partitions,
    }


def partition_kind_map() -> dict[str, str]:
    snapshot = json.loads(SNAPSHOT_PATH.read_text(encoding="utf-8"))
    return dict(snapshot["partitionKind"])


# The one fact in this module transcribed rather than parsed: "hybrid drops these roles, and
# the commander never wears this frame" lives only inside core.v1.json's frame vocabulary as
# free-text prose (its `meaning` string for the "hybrid" entry), not a structured field —
# unlike everything else here, there is no key to read it from. `HYBRID_FRAME_CITATION` is the
# exact source sentence; `test_items_adapter.py` asserts it is still substring-present in the
# live registry, so a future registry edit that changes this rule cannot silently drift away
# from what this module assumes without a test noticing.
HYBRID_FRAME_CITATION = (
    "a chimera body combining both natures. Carries 13 of the 15 roles "
    "(drops ward-array and jewel-minor-b); each remaining role accepts a base type from either "
    "pure frame's ladder. The commander never wears this frame — it takes humanoid or plant only."
)

HYBRID_FRAME_EXCLUDED_ROLES = frozenset({"ward-array", "jewel-minor-b"})
COMMANDER_ROLE = "standard"
