"""seedsmith.adapters.items.combogen.schema — closed-enum output, audit_schema-clean by construction.

⛔ P1: **the model writes identity, deterministic code writes magnitude.** The model picks the name,
the flavour, the four ingredient FAMILIES and the granted atom families. It never emits a number,
and that is enforced mechanically — `Pipeline.__post_init__` runs `audit_schema` at CONSTRUCTION, so
a numeric field cannot reach a call at all.

Four names this schema avoids rather than allow-listing its way past
(`pipeline.model.MAGNITUDE_DENY_NAMES`): `tier` (the granted tier comes from tuning plus D22's
attunement bonus), `cost`, `duration` and `chance`. `minTier` never appears either — the P1 table
assigns it to code, and offering the model a per-ingredient tier is the shape most likely to be
mistaken for a magnitude the model may choose.

⚠ **`ingredients` is a flat array of four family strings, and repeats are legal.** D41 makes a
recipe an unordered MULTISET, so "two `atom.bulwark` and two `atom.vitality`" is a legal answer and
must be expressible; a schema with four distinct named slots would have re-introduced position by
the back door. `emit.ingredient_rows` folds the array into `(family, minTier, qty)` rows.
"""
from __future__ import annotations

from typing import Any

from .tuning import ComboTuning

#: The frames a combination may pin its host to. `""` is not offered — an unset host frame means
#: "any", and it is expressed by OMITTING the field, which is how the shipped corpus already
#: expresses it (18 of the 25 legacy entries leave `hostFrame` unset).
HOST_FRAMES: "tuple[str, ...]" = ("humanoid", "plant")


def _identity_fields() -> "dict[str, Any]":
    return {
        "name": {"type": "string", "minLength": 3, "maxLength": 48},
        "nameKey": {"type": "string", "pattern": r"^[a-z][a-z0-9]*(\.[a-z0-9]+(-[a-z0-9]+)*)+$"},
        "flavor": {"type": "string", "minLength": 8, "maxLength": 400},
    }


def _blocked() -> "dict[str, Any]":
    return {
        "type": "string",
        "description": "Set this INSTEAD of the content fields if the brief cannot be satisfied — "
                       "say why. A blocked answer writes nothing and is reported, not retried "
                       "forever.",
    }


def combination_schema(tuning: ComboTuning, *,
                       supplied_families: "tuple[str, ...]",
                       host_roles: "tuple[str, ...]",
                       granted_families: "tuple[str, ...]") -> "dict[str, Any]":
    """The `combination` output schema.

    `ingredients` is fixed at exactly `tuning.ingredient_count` items — D20 as amended (§2f.2) — so
    a three-ingredient answer is not a lint finding, it is an invalid response the local schema
    check rejects before anything is written.

    `hostRole` is closed to the roles whose socket ceiling actually reaches the ingredient count.
    The model is therefore never offered a chassis that could not hold the combination it is
    authoring, which is the same "cap inside the schema" discipline module 13 applied to the twelve
    roles: a constraint enforced at LOAD is a re-run, a constraint enforced in the schema is free.
    """
    if not supplied_families:
        raise ValueError("no supplied ingredient families — run the gem-supply precheck first")
    if not host_roles:
        raise ValueError(
            "no role's socket ceiling reaches the ingredient count, so no combination could ever "
            "fire; refusing to build a schema for content nothing can host")
    return {
        "type": "object",
        "additionalProperties": False,
        "required": [],
        "properties": {
            **_identity_fields(),
            "blocked": _blocked(),
            "ingredients": {
                "type": "array",
                "minItems": tuning.ingredient_count,
                "maxItems": tuning.ingredient_count,
                "items": {
                    "type": "string",
                    "enum": list(supplied_families),
                    "description": "an ingredient gem family. Repeats are legal — a recipe is an "
                                   "unordered multiset, so four of the same family is a real "
                                   "answer, not a mistake.",
                },
            },
            "grants": {
                "type": "array",
                "minItems": 1,
                "maxItems": 2,
                "items": {
                    "type": "string",
                    "enum": list(granted_families),
                    "description": "the atom family this combination grants. Prefer a MECHANISM — "
                                   "a proc, a rider, a spawn — over a bigger number on a stat the "
                                   "ingredients already carry.",
                },
            },
            "hostRole": {
                "type": "string",
                "enum": list(host_roles),
                "description": "omit for any role; naming one pins the combination to that chassis",
            },
            "hostFrame": {
                "type": "string",
                "enum": list(HOST_FRAMES),
                "description": "omit for any frame",
            },
        },
    }


def schema_field_names(schema: "dict[str, Any]") -> "tuple[str, ...]":
    """Every property name in the schema, for a test that wants to assert an ABSENCE by name."""
    return tuple(sorted(schema.get("properties", {})))
