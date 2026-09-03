"""seedsmith.adapters.actions.vocab — closed vocabularies transcribed from the C# code of record's
own `Name` functions (spec-corpus-loader.md §3 step 5), never from the enum member names — the
wire strings are what a loaded entry actually carries, and citing the declaration instead of the
`Name` function is the exact F10 mistake this spec's own first envelope example made ("Area" /
"Row" / "Enemy" instead of "area" / "row" / "enemy").

Every citation below was checked against the live file while this module was built (2026-09-03),
not copied from the spec's own citations — several of which point at the wrong file or a stale
line range (see corpus-loader's build report for the full corrected list; the short version: this
module's spec was written against an older `ActionEnums.cs` that was 24 lines shorter before A-E1
inserted `EligibilityScope`/`PairingRole`, and it names `RelationKind.cs` under `Actions/` when the
type actually lives in `FusionRpg.Contracts`).

`atomFamilies`/`pairedPayoffFamily`/family-scoped `scopeKey` are NOT transcribed here — they are
read fresh from the files that own them (`load_family_ids`, `load_pairing_keys`,
`load_family_map_keys`), same discipline as `adapters/items/registries.py`'s own docstring: a
registry fact is read, never re-typed, except the one C#-only fact (there is no JSON export of an
enum's `Name` switch to read instead).
"""
from __future__ import annotations

import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[5]

# ActionKinds.Name — src/FusionRpg.Core/Actions/ActionEnums.cs:96-102 (spec cites :72-78 — stale,
# see module docstring).
ACTION_KINDS = frozenset({"basic", "innate", "skill"})

# ActionCategories.Name — ActionEnums.cs:120-128 (spec cites :96-104 — stale).
CATEGORIES = frozenset({"attack", "defense", "support", "movement", "status"})

# ActionTags.Name — ActionEnums.cs:152-163 (spec cites :128-139 — stale).
TAGS = frozenset({
    "offensive", "defensive", "heal", "buff", "debuff", "movement", "summon", "utility",
})

# ActionTargetModes.Name — src/FusionRpg.Core/Actions/ActionTargetSpec.cs:103-112 (matches the
# spec's own citation exactly).
TARGET_MODES = frozenset({"self", "single", "multi", "rolledTarget", "all", "area"})

# ActionAreaShapes.Name — ActionTargetSpec.cs:134-141 (matches).
AREA_SHAPES = frozenset({"row", "column", "square", "rectangle"})

# RelationKinds.Name — src/FusionRpg.Contracts/RelationKind.cs:21-28. The spec cites
# `.../Actions/RelationKind.cs:23-26`; the type lives in `FusionRpg.Contracts`, aliased into
# `FusionRpg.Core.Actions` by a `global using` at the top of `ActionTargetSpec.cs` — which is
# exactly why the spec's own prose elsewhere hedges with "or wherever `RelationKinds.Name` lives".
RELATIONS = frozenset({"self", "ally", "enemy", "any"})

# StatusCatalogBootstrap.RegisterAll — src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:16-58
# (matches the spec's citation exactly — 21 Register/RegisterWithOptions calls).
STATUSES = frozenset({
    "butter", "freeze", "cold", "poison", "hypno", "ember", "jala", "kelp",
    "wither", "bond", "rally", "leech", "expose", "command", "shatter", "charm_pulse",
    "blight", "rot", "spark", "pact_mark", "spore",
})

# EligibilityScopes.Name — ActionEnums.cs:228-234. Not part of acceptance #6's counted list (that
# one names exactly 7 groups), kept anyway because `scope` is a required action-seed field and a
# real closed vocabulary the loader must still refuse an unknown member against.
SCOPES = frozenset({"general", "family", "species"})

# PairingRoles.Name — ActionEnums.cs:250-256. Same status as SCOPES above — `pairingRole` is
# required (F7 correction: `none` is a value, never an omission).
PAIRING_ROLES = frozenset({"none", "enabler", "payoff"})


def _load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_family_ids() -> "frozenset[str]":
    """The 98 authored atom-family ids `atomFamilies` / `pairedPayoffFamily` draw from
    (`data/seed/items/affix-families/*.json`, `entries[].id`) — read fresh every call, never
    transcribed (spec-corpus-loader.md §2's "DECIDED" section; measured 2026-09-03: 15 files, 98
    entries, zero overlap with the 17 demo families under `data/seed/atoms/`)."""
    families_dir = REPO_ROOT / "data" / "seed" / "items" / "affix-families"
    ids: set[str] = set()
    for path in sorted(families_dir.glob("*.json")):
        doc = _load_json(path)
        for row in doc.get("entries", []):
            if isinstance(row, dict) and row.get("id"):
                ids.add(row["id"])
    return frozenset(ids)


def load_pairing_keys() -> "frozenset[str]":
    """The payoff-family keys of `data/seed/actions/pairings.json` — the only legal values for
    `pairedPayoffFamily` (`EnablerPayoffPairings.IsPayoff`,
    `src/FusionRpg.Core/Actions/Seeding/EnablerPayoffPairings.cs:26`). Read fresh — this loader
    must never touch that file, only read it (§4's first bullet)."""
    path = REPO_ROOT / "data" / "seed" / "actions" / "pairings.json"
    doc = _load_json(path)
    return frozenset(doc.keys())


def load_family_map_keys() -> "frozenset[str]":
    """The family ids a `family`-scoped entry's `scopeKey` may name — the VALUES of
    `data/seed/actions/_generated/family-map.json` (species -> family id), A-S0's projection and
    §3 step 5's eleventh vocabulary. Returns an empty set if the file is absent so a caller in a
    checkout where A-S0 has not yet run degrades to "nothing known" rather than raising — but as of
    this module's build (2026-09-03) the file already exists in the live tree: 53 species over 19
    family ids, matching the spec's own measured numbers exactly."""
    path = REPO_ROOT / "data" / "seed" / "actions" / "_generated" / "family-map.json"
    if not path.is_file():
        return frozenset()
    doc = _load_json(path)
    return frozenset(doc.values())
