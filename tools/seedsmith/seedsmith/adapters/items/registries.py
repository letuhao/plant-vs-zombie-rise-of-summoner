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


def load_theme_keys() -> frozenset[str]:
    """`themeKey`'s legal vocabulary — a UNION of two append-only populations that cannot collide
    by construction (spec-demon-themes.md §2.2a, resolving audit S5): legacy `theme.*` ids, human-
    authored and frozen in `themes.v1.json` (13 registered, 5 currently referenced by 38 real
    entries — measured 2026-08-31), and `demon.*` ids the demons feature publishes at runtime.

    This is the ONE file outside `adapters/demons/` the demons feature is allowed to touch
    (spec-adapter-demons.md's own single exception) — it adds a VOCABULARY, not a concept: this
    module still knows nothing about what a demon is, only that `demon.`-prefixed strings are now
    legal `themeKey` values. Demon themes are not loaded from a committed file here (none is
    committed yet — see the demons feature's own build notes); a caller with a live demon theme
    registry unions its keys in via `demon_theme_keys`.

    ⭐ **A THIRD population landed 2026-09-04 (item module 13, `set-charm-gen`): `build.*`.** A
    `set` REQUIRES a `themeKey` (`kinds.py`'s own spec, mirroring `KindCatalog.cs`), and the 36
    build set families are keyed on `(aptitude, archetype)` and belong to no species — so without
    it a build set is unauthorable. Ruled as a third append-only namespace rather than a loosened
    `themeKey`, because `spec-demon-themes.md` §7 names making it *required* on `unique` as the
    intended direction and loosening it here would reverse that. Collision-free against `theme.*`
    and `demon.*` by construction, exactly the namespace split §2.2a already established.
    """
    legacy = frozenset(f"theme.{t['id']}" for t in _load("themes.v1.json")["themes"])
    build = frozenset(row["themeKey"] for row in _load("build-themes.v1.json")["themes"])
    return legacy | build


def load_vocabularies(
    *, demon_theme_keys: "frozenset[str] | None" = None,
) -> dict[str, frozenset[str]]:
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
        "themeKey": load_theme_keys() | (demon_theme_keys or frozenset()),
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
    "a chimera body combining both natures. Carries 12 of the 15 roles "
    "(drops ward-array, head-guard and sense); each remaining role accepts a base type from either "
    "pure frame's ladder. The commander never wears this frame — it takes humanoid or plant only."
)

# D30 (2026-09-04, core.v1.json registryVersion 2): D3 wins over the prior 13-role/895‰ shape this
# constant used to name. jewel-minor-b is now hybrid-eligible; head-guard and sense are not.
HYBRID_FRAME_EXCLUDED_ROLES = frozenset({"ward-array", "head-guard", "sense"})
COMMANDER_ROLE = "standard"
