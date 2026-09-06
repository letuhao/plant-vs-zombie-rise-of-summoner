"""The uniques briefs (D4.29, spec-unique-pipeline.md §1) -- the boundary. Turns one planner
`Cell` into a per-call JSON Schema (PLANNED fields pinned `const`, VALIDATED fields a closed real
enum, AUTHORED fields free text) and the prompt text asking the model to fill in identity. Every
number the anchor will ever carry is resolved later in C# (`UniqueContainerBuild.From`) -- nothing
here is a magnitude, a weight or a probability (Law 2).
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .. import registries as _reg
from .planner import Cell

REPO_ROOT = Path(__file__).resolve().parents[6]
BASE_TYPES_DIR = REPO_ROOT / "data" / "seed" / "items" / "base-types"

#: `unique.counterPressure.kind` -- restricted to `narrow` only (core.v1.json:504-519's three
#: kinds are `narrow`/`drawback`/`conditional`, but `drawback` needs a `severityBand` + a
#: channel/family reference and `conditional` needs a named `condition`; every sampled real
#: anchor in the 144-corpus this session read used `narrow`). Named, not silently narrowed: a
#: `drawback`/`conditional` anchor is a real future enrichment, not required by this task's own
#: acceptance line, and picking a made-up severity/condition id here would be worse than not
#: shipping one.
COUNTER_PRESSURE_KIND: "tuple[str, ...]" = ("narrow",)

#: `unique.varianceSlot.variance` -- spec §1's own vocabulary (`narrow · normal · wide`).
VARIANCE: "tuple[str, ...]" = ("narrow", "normal", "wide")

#: How many `fixedAtoms[]` a first-ship anchor carries. `maxIdentityAtoms` (`uniques.v1.json`)
#: allows 1-3; every sampled real anchor in the 144-corpus carries exactly 2, so the new 30 match
#: the existing texture rather than exploring the tunable's own ceiling in the same batch.
FIXED_ATOM_COUNT = 2


def acquisition_for_band(band: str) -> str:
    """ssot-uniques.md §4.5: "ordinal >= 90 is never plain drop." `firstseed` (80) is the only
    band `drop` is legal at; `sunwoven`/`almanac` (90/100) get `deterministic` instead of
    `source-locked` because `source-locked` requires a REAL domain table to bind to
    (`dungeonBinding`, both fields refused `none`) and `data/seed/dungeon/domains/` is empty --
    `deterministic` needs no table reference at all (it is claimed later by a domain anchor's
    `firstClearRef`, D4.28), so it is the only >= 90 acquisition this batch can ship without
    minting a dangling reference to content that does not exist yet."""
    if band not in ("firstseed", "sunwoven", "almanac"):
        raise ValueError(f"unknown band {band!r}")
    return "drop" if band == "firstseed" else "deterministic"


def load_base_types_by_frame() -> "dict[str, frozenset[str]]":
    """`unique.baseType`'s real VALIDATED vocabulary, grouped by frame -- read fresh from
    `data/seed/items/base-types/**/*.json` (62 files), never hand-transcribed (this registry is
    large and grows independently of the uniques corpus). Only `plant`/`humanoid` are ever
    populated -- the grid excludes `hybrid` (planner.py's own FRAMES) so a hybrid base type, if
    any existed, would never be looked up."""
    by_frame: "dict[str, set[str]]" = {}
    for path in sorted(BASE_TYPES_DIR.rglob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        for entry in doc.get("entries") or ():
            frame, entry_id = entry.get("frame"), entry.get("id")
            if isinstance(frame, str) and isinstance(entry_id, str):
                by_frame.setdefault(frame, set()).add(entry_id)
    return {frame: frozenset(ids) for frame, ids in by_frame.items()}


#: One line per field the model actually sees, each with a negative clause (AI-native contract:
#: "every description needs a negative clause saying what the field is not") -- the dungeon
#: adapter's own `descriptions.py` precedent, kept local here rather than a fifth file since this
#: package briefs exactly one kind.
DESCRIPTIONS: "dict[str, str]" = {
    "id": "The minted tracking id for this anchor. NOT authored -- fixed by the planner before this call.",
    "nameKey": "The minted i18n key for this anchor's name. NOT authored -- fixed by the planner.",
    "iconKey": "The minted i18n key for this anchor's icon. NOT authored -- fixed by the planner.",
    "flavorKey": "The minted i18n key for this anchor's flavor text. NOT authored -- fixed by the planner.",
    "frame": "Which body this unique is built for. NOT a stat -- fixed by the planner's own grid cell.",
    "powerAxis": "Which of the five power vectors this unique leans into. NOT a strength value, only a category, and fixed by the planner's own grid cell.",
    "rarity": "The rung this unique ships at. NOT a strength claim -- on a unique the rung buys only the drop gate, fixed by the planner's own grid cell.",
    "acquisition": "How a player comes to hold this unique. NOT a weight or a chance -- fixed by the planner from the rung.",
    "name": "A short, evocative item name. NOT a description of its mechanics and NOT a number anywhere in the text.",
    "flavor": "One to two sentences of in-world flavor text. NOT a mechanics summary and NOT a number.",
    "baseType": "Which real base type this unique is built on. NOT a role noun -- must be one of the ids listed, never invented.",
    "fixedAtoms": "One to three fixed effect atoms this unique always carries, each a real atom family plus a power band. NOT a magnitude -- power band is a named tier, never a number.",
    "varianceSlot": "The one optional rolled effect slot this unique carries, or omitted entirely. NOT a second fixed core -- at most one slot, and it may be absent.",
    "counterPressure": "The one weakness this unique's design leans on. NOT flavor text -- `kind` must be one of the listed real values.",
    "tags": "Descriptive tags from the closed registry, at most one per exclusive axis (mass-class, material-nature, durability-class, combat-posture, origin, notability). NOT invented tags, and exactly one mass-class tag is required.",
}


def _enum(field: str, values: "tuple[str, ...] | frozenset[str]") -> dict:
    return {"type": "string", "enum": sorted(values), "description": DESCRIPTIONS[field]}


def _planned_const(field: str, value: Any) -> dict:
    return {"type": "string", "const": value, "description": DESCRIPTIONS[field]}


def build_unique_schema(
    cell: Cell,
    planned_ids: "dict[str, str]",
    *,
    base_types_by_frame: "dict[str, frozenset[str]] | None" = None,
    atom_families: "frozenset[str] | None" = None,
    mass_class_tags: "tuple[str, ...] | None" = None,
    other_tags: "tuple[str, ...] | None" = None,
) -> dict:
    """One call's schema for `cell` -- `planned_ids` supplies the four PLANNED-minted string
    values (`id`, `nameKey`, `iconKey`, `flavorKey`) from the planner's `IdMinter`. Every closed
    vocabulary defaults to a fresh registry read when the caller does not supply one (tests supply
    a small fixture instead, so a schema test never depends on the live corpus's exact size)."""
    base_types_by_frame = base_types_by_frame if base_types_by_frame is not None else load_base_types_by_frame()
    atom_families = atom_families if atom_families is not None else _reg.load_atom_families()
    if mass_class_tags is None or other_tags is None:
        axes = _reg.load_tag_axes(applies_to="unique")
        mass_class_tags = mass_class_tags if mass_class_tags is not None else axes.get("mass-class", ())
        other_tags = other_tags if other_tags is not None else tuple(
            t for axis, ids in axes.items() if axis != "mass-class" for t in ids)

    atom_item = {
        "type": "object",
        "properties": {
            "family": _enum("fixedAtoms", atom_families),
            "powerBand": {"type": "string", "enum": ["low", "medium", "high"],
                         "description": "How strong this fixed atom's identity value is, as a named tier. NOT a number."},
        },
        "required": ["family", "powerBand"], "additionalProperties": False,
    }
    variance_slot = {
        "type": "object",
        "properties": {
            "family": _enum("varianceSlot", atom_families),
            "variance": _enum("varianceSlot", VARIANCE),
        },
        "required": ["family", "variance"], "additionalProperties": False,
    }
    counter_pressure = {
        "type": "object",
        "properties": {
            "kind": _enum("counterPressure", COUNTER_PRESSURE_KIND),
            "note": {"type": "string", "description": "A sentence naming the weakness in-world. NOT a number, NOT a stat name."},
        },
        "required": ["kind", "note"], "additionalProperties": False,
    }

    properties: "dict[str, Any]" = {
        "id": _planned_const("id", planned_ids["id"]),
        "nameKey": _planned_const("nameKey", planned_ids["nameKey"]),
        "iconKey": _planned_const("iconKey", planned_ids["iconKey"]),
        "flavorKey": _planned_const("flavorKey", planned_ids["flavorKey"]),
        "frame": _planned_const("frame", cell.frame),
        "powerAxis": _planned_const("powerAxis", cell.axis),
        "rarity": _planned_const("rarity", cell.band),
        "acquisition": _planned_const("acquisition", acquisition_for_band(cell.band)),
        "name": {"type": "string", "description": DESCRIPTIONS["name"]},
        "flavor": {"type": "string", "description": DESCRIPTIONS["flavor"]},
        "baseType": _enum("baseType", base_types_by_frame.get(cell.frame, frozenset())),
        "fixedAtoms": {"type": "array", "items": atom_item, "minItems": 1, "maxItems": 3,
                       "description": DESCRIPTIONS["fixedAtoms"]},
        "varianceSlot": variance_slot,
        "counterPressure": counter_pressure,
        "tags": {"type": "array", "items": {"type": "string", "enum": sorted(set(mass_class_tags) | set(other_tags))},
                 "minItems": 1, "description": DESCRIPTIONS["tags"]},
    }
    return {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "title": "Unique",
        "type": "object",
        "properties": properties,
        "required": ["id", "nameKey", "iconKey", "flavorKey", "frame", "powerAxis", "rarity",
                     "acquisition", "name", "flavor", "baseType", "fixedAtoms", "counterPressure", "tags"],
        "additionalProperties": False,
    }


SYSTEM_PROMPT = (
    "You author identity for a Diablo-style unique item in a JSON object matching the given "
    "schema. Every PLANNED field (id, nameKey, iconKey, flavorKey, frame, powerAxis, rarity, "
    "acquisition) is already fixed -- copy its const value verbatim, never change it. Choose a "
    "real baseType from the closed list that fits the power axis and frame. Choose fixedAtoms "
    "and, optionally, a varianceSlot ONLY from the real atom-family list given -- never invent a "
    "family. Write a short evocative name, one to two sentences of flavor text, and one sentence "
    "of counterPressure.note describing the item's built-in weakness. Never write a number "
    "anywhere in name, flavor, or counterPressure.note."
)


def build_brief(cell: Cell, schema: dict) -> str:
    """The user-turn text -- the schema itself carries every closed vocabulary already (LM
    Studio's constrained decoding makes an out-of-enum value unsampleable, spec-pipeline.md's own
    "prove constrained decoding is on" rule), so the brief states only what a schema cannot: the
    axis's own flavor connotation, kept short because AUTHORED free text is the only thing the
    model is actually deciding here."""
    return (
        f"Author one unique item.\n"
        f"Frame: {cell.frame}\n"
        f"Power axis: {cell.axis} (this item's build should read as leaning into {cell.axis})\n"
        f"Rarity band: {cell.band}\n"
        f"Return ONLY the JSON object."
    )
