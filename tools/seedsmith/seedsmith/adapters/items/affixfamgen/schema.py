"""seedsmith.adapters.items.affixfamgen.schema — the closed-enum answer schema.

⛔ Same discipline as `setgen/schema.py`: identity only, every magnitude resolved afterwards by
`seedsmith.numerics`. `pipeline.model.audit_schema` (run by `brief.AffixFamilyBrief.__post_init__`)
is the mechanical proof this file cannot regress into offering a bare number — this module's own
job is just to make sure that proof always has something to check, by never being imported without
also being audited.

⚠ Two field names the `MAGNITUDE_DENY_NAMES` deny-list would refuse on sight, and how this schema
avoids them: `tier` never appears (tiers come from `numerics`, exactly like `setgen/schema.py`'s
own note); `powerBand` is NOT on the deny list (it is a closed enum of category words, the same
"author writes this INSTEAD of a magnitude" role `bands.v1.json` itself documents) and is kept as
the field name real shipped families already use, rather than inventing a different spelling to
dodge a check it does not trigger.

`op`'s enum is built from `opvocab.legal_ops(kind_id)` — the ONE place that vocabulary lives — so
this schema and `AtomKindRegistry.cs`'s real per-kind op set cannot silently drift apart the way two
hand-typed copies of the same list eventually do.
"""
from __future__ import annotations

from typing import Any

from . import opvocab

#: `naming.v1.json`'s own family-id rule: a NEW family's `word` is "the agent's own free choice of
#: a short mechanical identifier (NOT a display word)". Enforced here as a pattern, not just prose:
#: lowercase kebab, 2-24 chars, matching the shipped `word` tokens already seen (`hardening`,
#: `riveting`, `life-graft`'s own `graft`, `fixation`, `truesight`).
WORD_PATTERN = r"^[a-z][a-z0-9]*(-[a-z0-9]+)*$"

NAME_KEY_PATTERN = r"^[a-z][a-z0-9]*(\.[a-z0-9]+(-[a-z0-9]+)*)+$"


def _identity_fields() -> "dict[str, Any]":
    return {
        "name": {"type": "string", "minLength": 3, "maxLength": 48},
        "nameKey": {"type": "string", "pattern": NAME_KEY_PATTERN},
        "displayTemplate": {"type": "string", "minLength": 3, "maxLength": 160},
    }


def _blocked() -> "dict[str, Any]":
    return {
        "type": "string",
        "description": "Set this INSTEAD of the content fields if the brief cannot be satisfied — "
                       "say why. A blocked answer writes nothing and is reported, not retried "
                       "forever.",
    }


def affix_family_schema(kind_id: str, *, channels: "tuple[str, ...]", roles: "tuple[str, ...]",
                        tags: "tuple[str, ...]", power_bands: "tuple[str, ...]") -> "dict[str, Any]":
    """The `affix-family` answer schema for ONE partition + ONE declared `kindId`.

    `channels` is the partition's own already-used channel set (see `brief.py`'s "why channel is
    closed to the partition" note) — never the full C# `PrimaryChannels`/`DerivedChannels`
    vocabulary, which this generator does not re-transcribe.
    """
    if not channels:
        raise ValueError("affix_family_schema requires at least one legal channel")
    if not roles:
        raise ValueError("affix_family_schema requires at least one legal role")
    if not tags:
        raise ValueError("affix_family_schema requires at least one legal tag")
    if not power_bands:
        raise ValueError("affix_family_schema requires at least one legal powerBand")

    ops = opvocab.legal_ops(kind_id)

    return {
        "type": "object",
        "additionalProperties": False,
        "required": [],
        "properties": {
            **_identity_fields(),
            "blocked": _blocked(),
            "word": {"type": "string", "pattern": WORD_PATTERN, "minLength": 2, "maxLength": 24,
                     "description": "a short MECHANICAL identifier, never a display word — the "
                                    "family id is minted as atom.{stem}-{word}"},
            "channel": {"type": "string", "enum": list(channels),
                        "description": "one of this partition's own already-registered channels"},
            "op": {"type": "string", "enum": list(ops),
                   "description": f"the real, closed op vocabulary for kindId {kind_id!r}"},
            "roles": {
                "type": "array",
                "minItems": 1,
                "maxItems": len(roles),
                "items": {"type": "string", "enum": list(roles)},
            },
            "tags": {
                "type": "array",
                "minItems": 1,
                "maxItems": len(tags),
                "items": {"type": "string", "enum": list(tags)},
            },
            "powerBand": {"type": "string", "enum": list(power_bands)},
        },
    }
