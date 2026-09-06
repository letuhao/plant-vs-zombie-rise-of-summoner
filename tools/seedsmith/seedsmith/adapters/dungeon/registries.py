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

#: `theme`'s own vocabulary has no dungeon-registries home (spec-dungeon-seed-contract.md:44:
#: `` `themes.v1.json` (84 rows) ``; `party-dungeon-ideal.md:838`: "the frozen theme registry
#: under `data/seed/demons/_registry/`") — a frozen CROSS-PROGRAM input the dungeon adapter reads,
#: never authors (`spec-dungeon-seed-contract.md:143`'s own "frozen inputs" list names
#: `themes/motifs/families` alongside `registries.v1`), the same shape `dropBand` already crosses
#: from the item registry into `dungeon-loot` on the C# side. Second-reader propagation of
#: `spec-demon-themes.md`'s own one-way publish — see this adapter's own D1.10 todo entry for the
#: full citation trail; `adapters/items/registries.py:33-55`'s `load_theme_keys()` is the direct
#: precedent for this exact cross-program read.
DEMONS_REGISTRY_DIR = REPO_ROOT / "data" / "seed" / "demons" / "_registry"

_REGISTRY_FILES = (
    "room-kinds.v1.json", "door-kinds.v1.json", "override-tags.v1.json",
    "objective-templates.v1.json", "difficulty-rungs.v1.json", "disposition.v1.json",
    "interaction-verbs.v1.json", "raid-modes.v1.json", "bands.v1.json",
)


def _load(name: str) -> dict:
    path = REGISTRY_DIR / name
    return json.loads(path.read_text(encoding="utf-8"))


def _load_demons(name: str) -> dict:
    path = DEMONS_REGISTRY_DIR / name
    return json.loads(path.read_text(encoding="utf-8"))


def load_versions() -> dict[str, int]:
    """`registryVersion` per file, read fresh — never hardcoded (items/registries.py precedent).
    Includes the two cross-program demon registries `theme`/`motif` read as frozen inputs, prefixed
    `demons.` so a version bump on either side is independently named in provenance/`stale_ids()`
    rather than colliding with a same-named dungeon-native file."""
    versions = {name.removesuffix(".v1.json"): _load(name)["registryVersion"] for name in _REGISTRY_FILES}
    versions["demons.themes"] = _load_demons("themes.v1.json")["registryVersion"]
    versions["demons.motifs"] = _load_demons("motifs.v1.json")["registryVersion"]
    return versions


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


def load_themes() -> "dict[str, dict]":
    """The demon-seed program's own published theme registry (`spec-demon-themes.md`), read as a
    frozen cross-program vocabulary — this adapter is a SECOND reader alongside
    `adapters/items/registries.py:33-55`'s own `load_theme_keys()`, never a second publisher: no
    row here is ever written back, matching the source registry's own "publish one-way" boundary.
    A retired theme's own row still resolves (a demon that leaves the roster keeps its published
    theme, per `spec-demon-themes.md`'s own row) — legal to reference, never filtered out here."""
    return dict(_load_demons("themes.v1.json")["themes"])


def load_theme_ids() -> "frozenset[str]":
    """The legal `theme` vocabulary for a dungeon domain anchor — every id in the registry,
    `demon.*`-prefixed, retired or not (see `load_themes`'s own doc comment on retirement)."""
    return frozenset(load_themes())


def load_motifs() -> "frozenset[str]":
    """The flat union of every theme's own motifs (`data/seed/demons/_registry/motifs.v1.json`) —
    the vocabulary `dungeon-seed-contract.md`'s own planner motif briefs (D1.9) partition per cell,
    never re-derived from `load_themes()`'s own per-theme lists here (the committed file is
    already the union, kept in sync by the demon program's own `generate_motifs.py`)."""
    return frozenset(_load_demons("motifs.v1.json")["motifs"])


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
        "theme": load_theme_ids(),
        "motif": load_motifs(),
    }
    vocab.update(load_bands())
    return vocab
