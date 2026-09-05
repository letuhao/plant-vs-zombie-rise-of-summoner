"""seedsmith.adapters.dungeon.registries — reads `data/seed/dungeon/_registry/*.json` fresh on
every call (D1.5, spec-dungeon-registries.md "The seedsmith side"): "the `dungeon` adapter reads
... fresh on every call, the `adapters/items/registries.py:1-6` discipline ('read, never
transcribed'), and never the `adapters/demons/registries.py:3-9` shape (a mirror of C# enums,
pinned by count)."

This is the ONE place the nine registry files are read on the Python side. The C# counterpart is
`src/FusionRpg.Core/Dungeon/Registry/DungeonRegistries.cs` — the two must never be allowed to
drift, which is exactly what `test_dungeon_registries.py` proves on every run: both sides load the
SAME committed JSON, so a drift is impossible by construction, not by discipline alone.
"""
from __future__ import annotations

import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[5]
REGISTRY_DIR = REPO_ROOT / "data" / "seed" / "dungeon" / "_registry"

_REGISTRY_FILES = (
    "room-kinds.v1.json", "door-kinds.v1.json", "override-tags.v1.json",
    "objective-templates.v1.json", "difficulty-rungs.v1.json", "disposition.v1.json",
    "interaction-verbs.v1.json", "raid-modes.v1.json", "bands.v1.json",
)


def _load(name: str) -> dict:
    path = REGISTRY_DIR / name
    return json.loads(path.read_text(encoding="utf-8"))


def load_versions() -> dict[str, int]:
    """`registryVersion` per file, read fresh — never hardcoded (items/registries.py precedent)."""
    return {name.removesuffix(".v1.json"): _load(name)["registryVersion"] for name in _REGISTRY_FILES}


def load_room_kinds() -> dict[str, dict]:
    """The eleven room kinds with their flags — `climateNeutral`, `secretEligible`,
    `bossRowAllowed`, `neverAdjacentTo`, `unknownResolvesTo` — exactly the row shape
    `RoomKindCatalog.Parse` reads on the C# side."""
    return dict(_load("room-kinds.v1.json")["roomKinds"])


def load_door_kinds() -> dict[str, dict]:
    return dict(_load("door-kinds.v1.json")["doorKinds"])


def load_override_tags() -> "frozenset[str]":
    return frozenset(_load("override-tags.v1.json")["overrideTags"])


def load_objective_templates() -> dict[str, dict]:
    """The nine objective templates with `targetKind` and `sinkAvoidance`."""
    return dict(_load("objective-templates.v1.json")["objectiveTemplates"])


def load_difficulty_rungs() -> dict[str, int]:
    """Rung id -> its 1-based ordinal. Ten rows, ordinals contiguous 1..10 (enforced on the C#
    side by `DifficultyRungCatalog.Validate`; this reader trusts the same committed file)."""
    rungs = _load("difficulty-rungs.v1.json")["rungs"]
    return {rung_id: row["ordinal"] for rung_id, row in rungs.items()}


def load_disposition() -> "frozenset[str]":
    return frozenset(_load("disposition.v1.json")["disposition"])


def load_interaction_verbs() -> dict[str, "int | None"]:
    """Verb id -> its base-defense decision number, or `None` where the verb resolves through a
    key-supply override or an event-deck draw rather than a base-defense structure decision
    (`open`, `disarm`, `pray`, `loot`) — `destroy` is 12, `garrison` is 15."""
    verbs = _load("interaction-verbs.v1.json")["interactionVerbs"]
    return {verb_id: row["decision"] for verb_id, row in verbs.items()}


def load_raid_modes() -> "frozenset[str]":
    return frozenset(_load("raid-modes.v1.json")["raidModes"])


# The twenty band vocabularies this registry owns (spec-dungeon-registries.md "bands.v1.json" row)
# — kept as an explicit list so a missing or an extra band in the committed file is a loud
# assertion failure in the test, never a silent `KeyError` three modules downstream.
BAND_NAMES = (
    "dangerBand", "depthBand", "widthBand", "branchiness", "density", "hazardBand", "sightBand",
    "countBand", "elementSpread", "formation", "eventKind", "outcomeOrdinal", "repeatScope",
    "entry", "phasing", "questScope", "rewardBand", "deltaBand", "hpBand", "nerveStage",
)


def load_bands() -> "dict[str, frozenset[str]]":
    """One `frozenset` of legal members per band name — the vocabulary every anchor field's enum
    is checked against. Display names are a stage-facing (delve-stage, wave 5) concern, not read
    here: this adapter cares about legal ids, never player-facing copy."""
    bands = _load("bands.v1.json")["bands"]
    return {band_name: frozenset(row["members"]) for band_name, row in bands.items()}


def load_band_display_names() -> "dict[str, dict[str, str]]":
    bands = _load("bands.v1.json")["bands"]
    return {band_name: dict(row["displayNames"]) for band_name, row in bands.items()}


def load_vocabularies() -> dict[str, "frozenset[str]"]:
    """The `RegistrySet.vocabularies` shape every other adapter exposes (`items`/`demons`
    precedent) — one legal-id set per registry, flattened. Structured registries (room kinds,
    objective templates, interaction verbs, difficulty rungs) contribute their id set here; their
    per-row detail (flags, decision numbers, ordinals) is available from the dedicated loaders
    above for callers that need more than membership."""
    vocab: dict[str, "frozenset[str]"] = {
        "roomKind": frozenset(load_room_kinds()),
        "doorKind": frozenset(load_door_kinds()),
        "overrideTag": load_override_tags(),
        "objectiveTemplate": frozenset(load_objective_templates()),
        "difficultyRung": frozenset(load_difficulty_rungs()),
        "disposition": load_disposition(),
        "interactionVerb": frozenset(load_interaction_verbs()),
        "raidMode": load_raid_modes(),
    }
    vocab.update(load_bands())
    return vocab
