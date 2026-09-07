"""seedsmith.adapters.items.setgen.schema — the closed-enum output schema, audit_schema-clean by
construction.

⛔ P1, unamended: **the model writes identity, deterministic code writes magnitude.** The model picks
which capability, which stat families, which member roles, the name and the flavour. It never emits a
number — and that is enforced mechanically, by `pipeline.model.audit_schema`, which
`Pipeline.__post_init__` runs at CONSTRUCTION. A numeric field cannot reach a model call at all.

⚠ `pieces` is the one place a number is legal, and only as a closed enum. Written as a bare
`{"type": "integer"}` the schema is rejected before the first call. Said in the schema, not in a
comment — which is why `threshold_pieces` reads its enum from `distribute.threshold_ladder` rather
than repeating it: which piece counts a set may carry is one fact, and a second copy here is where
it goes stale. It did: the enum offered `{2,3,4,6}` while the ladder derived `(2,4)`, and the first
real batch lost all three sets to it.

⚠ Two field names the deny-list would refuse, and how this schema avoids them rather than
allow-listing its way past them: `tier` (a magnitude by convention) never appears — tiers come from
`numerics`; and `cost` never appears — a charm's AP cost is DERIVED from its class.
"""
from __future__ import annotations

from typing import Any

from .distribute import threshold_ladder
from .roles import HYBRID_CORE_ROLES
from .tuning import SetCharmGenTuning

#: The exact spelling `audit_schema` allows: an enum of numbers is a vocabulary, not an invention.
#: Built from tuning so the schema and the distributor cannot disagree about what is legal.
def threshold_pieces(tuning: SetCharmGenTuning,
                     ladder: "tuple[int, ...] | None" = None) -> "dict[str, Any]":
    """The `pieces` node. `ladder` narrows the enum to the counts THIS set can carry.

    ⚠ **Offering the whole `legalThresholdPieces` vocabulary was a real defect** (module 13
    defect 3, 2026-09-06): the enum said `{2,3,4,6}` and the brief said *"the piece counts"*, while
    `distribute.threshold_ladder` derived `(2, 4)` from the member count and refused everything
    else. All three sets in the first real batch picked 3 and were refused. The ladder is the
    single fact; the enum reads it.
    """
    return {"type": "integer", "enum": list(ladder if ladder is not None
                                            else tuning.legal_threshold_pieces)}


def _identity_fields() -> "dict[str, Any]":
    return {
        "name": {"type": "string", "minLength": 3, "maxLength": 48},
        # ⛔ maxLength added 2026-09-08: `pattern` alone is NOT decode-time-enforced (llama.cpp's
        # grammar-from-schema converter does not support it), so without a length bound this field
        # was completely unconstrained at generation time — the real cause of an incident where a
        # quantized model looped past 20K tokens inside it. `name`'s own 48 plus headroom for a
        # dotted/kebab-cased id derived from it.
        "nameKey": {"type": "string", "pattern": r"^[a-z][a-z0-9]*(\.[a-z0-9]+(-[a-z0-9]+)*)+$",
                    "maxLength": 96},
        "flavor": {"type": "string", "minLength": 8, "maxLength": 400},
    }


def _blocked() -> "dict[str, Any]":
    return {
        "type": "string",
        "description": "Set this INSTEAD of the content fields if the brief cannot be satisfied — "
                       "say why. A blocked answer writes nothing and is reported, not retried "
                       "forever.",
    }


def set_schema(tuning: SetCharmGenTuning, *, frames: "tuple[str, ...]" = ("humanoid", "plant"),
               member_count: "int | None" = None) -> "dict[str, Any]":
    """The `set` output schema. Identity only — every magnitude is resolved afterwards.

    `members[].role` is the twelve-role cap **inside the schema**, not a validation afterthought:
    the model is never offered `head-guard`, so `SetRoleNotUniversal` cannot be produced by a
    well-formed response at all. That is the whole reason the cap is a generator input
    (ssot-sets §3.7 fires at LOAD, so ~1,000 sets checked after the fact is a re-run).

    ⭐ **`thresholds` is sized from the ladder for the same reason.** `member_count` defaults to the
    typical size, which is what `run.plan_run` briefs; the ladder it produces fixes both the legal
    `pieces` values and how many threshold rows there are. Before this, `minItems` was a flat 2 and
    a five-member set (ladder `(2,)`) could not satisfy the schema at all — nor could a two-member
    one, at the other end.
    """
    ladder = threshold_ladder(tuning, member_count or tuning.typical_members)
    return {
        "type": "object",
        "additionalProperties": False,
        "required": [],
        "properties": {
            **_identity_fields(),
            "blocked": _blocked(),
            "capability": {
                "type": "object",
                "additionalProperties": False,
                "required": ["family"],
                "properties": {
                    "family": {"type": "string",
                               "description": "one capability family id from the brief's closed "
                                              "list — a non-stat.* kind (ssot-sets §3.2)"},
                    "variant": {"type": "string",
                                "description": "only for a family the brief marks as element-"
                                               "generated; omit otherwise"},
                },
            },
            "members": {
                "type": "array",
                "minItems": 2,
                "maxItems": tuning.max_roles,
                "items": {
                    "type": "object",
                    "additionalProperties": False,
                    "required": ["role", "frame"],
                    "properties": {
                        "role": {"type": "string", "enum": list(HYBRID_CORE_ROLES)},
                        "frame": {"type": "string", "enum": list(frames)},
                    },
                },
            },
            "thresholds": {
                "type": "array",
                "minItems": len(ladder),
                "maxItems": len(ladder),
                "items": {
                    "type": "object",
                    "additionalProperties": False,
                    "required": ["pieces"],
                    "properties": {
                        "pieces": threshold_pieces(tuning, ladder),
                        "families": {
                            "type": "array",
                            "minItems": 1,
                            "maxItems": 3,
                            "items": {"type": "string",
                                      "description": "a stat.modify / stat.derived family id from "
                                                     "the brief's closed list; omitted on the "
                                                     "lowest threshold, which carries the "
                                                     "capability instead"},
                        },
                    },
                },
            },
        },
    }


def charm_schema(tuning: SetCharmGenTuning) -> "dict[str, Any]":
    """The `charm` output schema.

    `charmClass` is an enum and `apCost` is absent on purpose: the cost is derived from the class
    (ssot-charms §3.4), and a model asked for both would eventually disagree with itself. `axis`
    reuses the five power categories the family library already uses — no new vocabulary.
    """
    return {
        "type": "object",
        "additionalProperties": False,
        "required": [],
        "properties": {
            **_identity_fields(),
            "blocked": _blocked(),
            "charmClass": {"type": "string",
                           "enum": [c.id for c in tuning.charm_classes]},
            "axis": {"type": "string",
                     "enum": ["offense", "survivability", "control", "utility", "economy"]},
            "frameHint": {"type": "string", "enum": ["any", "humanoid", "plant"]},
            "families": {
                "type": "array",
                "minItems": 1,
                "maxItems": 2,
                "items": {"type": "string",
                          "description": "an always-on Flat family id from the brief's closed "
                                         "list — never Increased, never More (ssot-charms §3.4)"},
            },
            "drawback": {
                "type": "object",
                "additionalProperties": False,
                "required": ["family"],
                "properties": {
                    "family": {"type": "string",
                               "description": "signet only: the authored negative atom's family"},
                },
            },
        },
    }
