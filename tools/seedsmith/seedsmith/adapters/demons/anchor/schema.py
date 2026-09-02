"""The species anchor schema (demon-seed module 2, spec-anchor-contract.md). Twenty-one keys for
eighteen design variables — `speciesId`, `gameTypeId` and `pure` are bookkeeping the ideal doc's
count did not include, per §2. **The anchor holds no numbers at all** except the one allow-listed
identifier (`gameTypeId`), which is what makes `audit.py`'s numeric audit mechanical rather than a
judgement call.
"""
from __future__ import annotations

from typing import Any

from .descriptions import DESCRIPTIONS

# Real, shipped vocabularies — never invented here. Sources:
#   ELEMENTS      src/FusionRpg.Core/Combat/Element/ElementTable.cs:125-130
#   APTITUDES     src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs (AptitudeCatalog.All)
#   POSTURES      src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs (enum Posture)
#   DEPLOY_MODE   src/FusionRpg.Core/Demons/DemonRarity.cs (enum DemonDeployMode)
#   ACQUISITION   src/FusionRpg.Core/Demons/DemonRarity.cs (enum DemonAcquisition, [Flags])
#   RARITY        docs/architecture/item/ssot-rarity.md §3.3 (ten-rung ladder, demons adopted 2026-09-01)
#   THREAT_BAND   data/tuning/demon-threat.v1.json (demon-seed module 4)
#   VARIANTS      docs/architecture/demon-seed-ideal.md:274 (owner Q17)
#   RESOURCES     resource-hub-ssot.md — six actor resources incl. poise

ELEMENTS = ("fire", "ice", "air", "earth", "light", "dark")

APTITUDES = (
    "Might", "Fortitude", "Vigor", "Onslaught",
    "Agility", "Composure", "Pierce", "Focus",
    "Bulwark", "Retribution", "Precision", "Ferocity",
)

# aptitude id -> posture, ported verbatim from AptitudeCatalog.All so `posture` derives correctly.
APTITUDE_POSTURE = {
    "Might": "Force", "Fortitude": "Force", "Vigor": "Force", "Onslaught": "Force",
    "Agility": "Finesse", "Composure": "Finesse", "Pierce": "Finesse", "Focus": "Finesse",
    "Bulwark": "Bastion", "Retribution": "Bastion", "Precision": "Bastion", "Ferocity": "Bastion",
}

POSTURES = ("Force", "Finesse", "Bastion")
THREAT_BAND = (
    "nuisance", "pest", "marauder", "raider", "warden",
    "scourge", "tyrant", "harbinger", "cataclysm", "calamity",
)
RARITY = (
    "chaff", "sprout", "grafted", "cultivated", "fused",
    "chimeric", "heirloom", "firstseed", "sunwoven", "almanac",
)
DEPLOY_MODE = ("PlantAvatar", "HypnoAlly")
ACQUISITION = ("Summonable", "CaptureOnly", "EventOnly")
VARIANTS = ("normal", "ancient", "mutated", "corrupted", "blessed", "cursed", "shiny")
RESOURCES = ("hp", "stamina", "hunger", "spirit", "qi", "poise")
BASIS = ("observed", "stated", "inferred", "blocked")
ATTACK_TEMPO = ("ponderous", "slow", "steady", "quick", "flurry")
REACH = ("melee", "short", "long", "siege")
TARGET_PREFERENCE = ("frontline", "backline", "swarm", "elite", "structure", "indiscriminate")

# Ownership level per field (spec §1). A field with no entry here is a contract defect.
OWNERSHIP = {
    "side": "CAPTURED", "speciesId": "CAPTURED", "gameTypeId": "CAPTURED",
    "elementPrimary": "CLASSIFIED", "elementSecondary": "CLASSIFIED",
    "aptitudePrimary": "CLASSIFIED", "aptitudeSecondary": "CLASSIFIED",
    "posture": "DERIVED", "pure": "DERIVED",
    "threatBand": "CLASSIFIED", "rarity": "CLASSIFIED",
    "deployMode": "CLASSIFIED", "acquisition": "CLASSIFIED",
    "variants": "CLASSIFIED", "resourceProfile": "CLASSIFIED",
    "basis": "DERIVED", "family": "CLASSIFIED", "traits": "CLASSIFIED",
    "attackTempo": "CLASSIFIED", "reach": "CLASSIFIED", "targetPreference": "CLASSIFIED",
}

# The model authors CLASSIFIED fields only — CAPTURED is echoed from the dump, DERIVED is
# computed by code (never let the model author posture/pure/basis — boundaries).
CLASSIFIED_FIELDS = frozenset(k for k, v in OWNERSHIP.items() if v == "CLASSIFIED")
DERIVED_FIELDS = frozenset(k for k, v in OWNERSHIP.items() if v == "DERIVED")

# The one legal integer field — an identifier, never a magnitude (spec §4).
ALLOWLISTED_INTEGER_FIELDS = frozenset({"gameTypeId"})


def _desc(field: str) -> str:
    try:
        return DESCRIPTIONS[field]
    except KeyError:
        raise KeyError(f"anchor field {field!r} has no description in descriptions.py") from None


def _enum_prop(field: str, values: "tuple[str, ...]", *, nullable: bool = False) -> dict:
    enum_values = list(values) + (["none"] if nullable else [])
    return {"type": "string", "enum": enum_values, "description": _desc(field)}


def _flag_array_prop(field: str, values: "tuple[str, ...]") -> dict:
    return {
        "type": "array",
        "items": {"type": "string", "enum": list(values)},
        "minItems": 1,
        "uniqueItems": True,
        "description": _desc(field),
    }


def _open_array_prop(field: str) -> dict:
    return {
        "type": "array",
        "items": {"type": "string"},
        "minItems": 1,
        "description": _desc(field),
    }


def build_anchor_schema() -> dict:
    """The resolved JSON Schema for one species anchor. Consumable directly as an LM Studio
    `response_format: {"type": "json_schema", "json_schema": {"schema": build_anchor_schema()}}`.
    """
    properties: "dict[str, Any]" = {
        "side": {"type": "string", "enum": ["plant", "zombie"], "description": _desc("side")},
        "speciesId": {"type": "string", "description": _desc("speciesId")},
        # The one allow-listed integer — captured, an identifier, never arithmetic (spec §4).
        "gameTypeId": {"type": "integer", "description": _desc("gameTypeId")},

        "elementPrimary": _enum_prop("elementPrimary", ELEMENTS),
        "elementSecondary": _enum_prop("elementSecondary", ELEMENTS, nullable=True),

        "aptitudePrimary": _enum_prop("aptitudePrimary", APTITUDES),
        "aptitudeSecondary": _enum_prop("aptitudeSecondary", APTITUDES, nullable=True),

        "posture": _enum_prop("posture", POSTURES),
        "pure": {"type": "boolean", "description": _desc("pure")},

        "threatBand": _enum_prop("threatBand", THREAT_BAND),
        "rarity": _enum_prop("rarity", RARITY),

        "deployMode": _enum_prop("deployMode", DEPLOY_MODE),
        "acquisition": _flag_array_prop("acquisition", ACQUISITION),

        "variants": _flag_array_prop("variants", VARIANTS),
        "resourceProfile": _flag_array_prop("resourceProfile", RESOURCES),

        "basis": _enum_prop("basis", BASIS),

        "family": _open_array_prop("family"),
        "traits": _open_array_prop("traits"),

        "attackTempo": _enum_prop("attackTempo", ATTACK_TEMPO),
        "reach": _enum_prop("reach", REACH),
        "targetPreference": _enum_prop("targetPreference", TARGET_PREFERENCE),
    }

    return {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "title": "DemonSpeciesAnchor",
        "type": "object",
        "properties": properties,
        "required": sorted(properties.keys()),
        "additionalProperties": False,
    }
