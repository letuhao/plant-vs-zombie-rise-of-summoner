"""D1.10's real remaining scope, quest half (2026-09-07): per-cell schema and brief construction
for `dungeon-quest` -- the SAME "generic schema for audit, cell-parameterized schema for real
generation" split `adapters/items/uniques/briefs.py` already established for uniques (`schema.py`'s
own `build_quest_schema()` is the documentation/audit view; this module builds the REAL, per-cell
version a live call actually uses).

Grows to cover the other four AUTHORED-field kinds (event, encounter, room, domain) as each one's
own real generation pass is built -- one file per this whole adapter, not one per kind, matching
this repo's own file list for D1.10 (`adapters/dungeon/pipelines.py`, singular).
"""
from __future__ import annotations

from typing import Any

from . import registries as _reg
from .planner import Cell

#: `ItemRole.cs`'s own fifteen real equip roles (no committed JSON registry exists for these --
#: the same "vocabulary with no dungeon-registries home, cited to its real source" shape
#: `schema.py`'s own `ELEMENTS`/`THREAT_BAND` already use). `Standard` (the sixteenth, reserved
#: commander slot) is excluded -- its own doc comment says "the generator emits nothing into it",
#: so a quest could never actually be satisfied by extracting one.
ITEM_ROLES: "tuple[str, ...]" = (
    "armament-primary", "core-guard", "ward-array", "armament-secondary", "jewel-major",
    "manipulator", "mantle", "head-guard", "girdle", "sense", "footing", "infusion",
    "retinue", "jewel-minor-a", "jewel-minor-b",
)

#: `QuestCatalog.cs`'s own fixed six-id set (`CountLessTemplates`) -- NOT derivable from
#: `targetKind` alone (`extract-with-item-kind` has a real `item-kind` targetKind yet is still
#: count-less), so it is its own named list here too, matching the C# side exactly rather than
#: re-deriving a rule that does not actually hold.
COUNT_LESS_TEMPLATES: "frozenset[str]" = frozenset({
    "kill-boss", "extract-with-item-kind", "bring-demon-home-alive",
    "finish-under-hunger", "survive-no-downed", "spend-no-provision",
})

#: `QuestCatalog.cs`'s own `needsTargetRef` check, verbatim: only these three `targetKind` values
#: ever need a real targetRef -- `kill-boss`'s own `targetKind: "boss"` does NOT (there is only one
#: boss per domain, nothing to disambiguate), so it is deliberately absent from this set even
#: though "boss" reads like it should need one.
TARGET_KIND_NEEDING_REF: "frozenset[str]" = frozenset({"room-kind", "curio-kind", "item-kind"})


def target_ref_candidates_for(
    target_kind: str, room_kinds: "frozenset[str]", event_kinds: "tuple[str, ...]",
) -> "tuple[str, ...]":
    """The real vocabulary a `targetKind` value draws from, confirmed against the real C# consumers
    rather than the spec's own looser "room kind, event kind, species family" prose:
    `room-kind` -> the eleven real room kinds (`RoomTableBinding`-adjacent, `QuestOffer.cs`'s own
    `RoomsOfKind` reads a room's ARCHETYPE, but `cleanse-fights`' own targetRef is a room KIND);
    `curio-kind` -> `EVENT_KIND` (`QuestProgress.cs:39`: `e.Kind == quest.TargetRef` compares
    against `DelveReportEvent.Kind`, itself an event's own six-member kind, not a curio sub-type
    despite the template's own name); `item-kind` -> `ItemRole` (`QuestProgress.cs:41`:
    `h.Role == quest.TargetRef`, `h` a haul entry). `none`/`boss` never reach here — the caller
    checks `TARGET_KIND_NEEDING_REF` first.
    """
    if target_kind == "room-kind":
        return tuple(sorted(room_kinds))
    if target_kind == "curio-kind":
        return event_kinds
    if target_kind == "item-kind":
        return ITEM_ROLES
    raise ValueError(f"target_kind {target_kind!r} never needs a targetRef -- check TARGET_KIND_NEEDING_REF first")


def build_quest_schema_for_cell(
    cell: Cell, quest_id: str, *,
    objective_templates: "dict[str, dict]", room_kinds: "frozenset[str]", event_kinds: "tuple[str, ...]",
    reward_bands: "frozenset[str]", count_bands: "frozenset[str]", repeat_scopes: "tuple[str, ...]",
) -> dict:
    """The REAL, per-cell schema a live call uses -- `schema.py`'s own `build_quest_schema()` stays
    the generic, unparameterized audit/documentation view (every PLANNED field a placeholder
    `const`); this function patches in the cell's own real `questId`/`objectiveTemplate`/`scope`
    and resolves `targetRef`/`countBand` to their template-conditional real shape, matching the
    spec's own field table (`spec-dungeon-seed-contract.md` §1.5) exactly rather than the loosely-
    worded prose alone.
    """
    template_id, scope = cell.dimension_values
    template = objective_templates[template_id]
    target_kind = template["targetKind"]
    needs_target_ref = target_kind in TARGET_KIND_NEEDING_REF
    is_count_less = template_id in COUNT_LESS_TEMPLATES

    if needs_target_ref:
        candidates = target_ref_candidates_for(target_kind, room_kinds, event_kinds)
        target_ref_node: dict = {"type": "string", "enum": sorted(candidates)}
    else:
        target_ref_node = {"type": "string", "const": "none"}

    if is_count_less:
        count_band_node: dict = {"type": "string", "const": "none"}
    else:
        count_band_node = {"type": "string", "enum": sorted(count_bands)}

    return {
        "type": "object",
        "properties": {
            "questId": {"type": "string", "const": quest_id},
            "objectiveTemplate": {"type": "string", "const": template_id},
            "scope": {"type": "string", "const": scope},
            "name": {"type": "string"},
            "flavor": {"type": "string"},
            "targetRef": target_ref_node,
            "countBand": count_band_node,
            "rewardBand": {"type": "string", "enum": sorted(reward_bands)},
            "repeatScope": {"type": "string", "enum": sorted(repeat_scopes)},
            "prereqRefs": {"type": "array", "items": {"type": "string"}, "minItems": 0, "uniqueItems": True},
            "chainRef": {"type": "string", "const": "none"},
        },
        "required": [
            "questId", "objectiveTemplate", "scope", "name", "flavor", "targetRef",
            "countBand", "rewardBand", "repeatScope", "prereqRefs", "chainRef",
        ],
        "additionalProperties": False,
    }


QUEST_SYSTEM_PROMPT = (
    "You are authoring one quest anchor for a party-dungeon crawl. Write only what the schema asks "
    "for. `name` and `flavor` are free text; every other field is a closed choice already fixed by "
    "the schema's own enum or const. Never invent a targetRef outside the schema's own enum -- an "
    "empty or unlisted value is a defect, not a creative choice. `flavor` is one or two sentences "
    "naming what the quest asks and why it would matter to whoever is asking -- never a rules "
    "explanation, never a citation of a game mechanic by name."
)


#: `spec-delve-quests.md:79-82`'s own real, mechanical definition of scope -- WHICH FACT SOURCE the
#: quest's completion predicate reads, never a difficulty or a narrative register on its own. Real
#: finding, 2026-09-07: a first live batch measured a HIGH `name_collision` rate specifically
#: between different scopes of the SAME template (`bring-demon-home-alive` at `delve` vs `domain`
#: vs `roster` produced the identical name twice) -- the brief gave the model no reason to write a
#: different name for the "same ask evaluated differently" shape scope actually is. This hint
#: grounds a real, accurate distinction (never an invented one) so `name`/`flavor` can legitimately
#: differ without pretending scope is a difficulty knob.
_SCOPE_HINT = {
    "delve": "Judged by what happens during this one descent alone -- an immediate task for this run.",
    "domain": "Judged across every attempt the player has made at this same domain -- a standing task tied to the place, not one run.",
    "roster": "Judged by what becomes of the party's own demons at extraction -- a task about who comes home, not where.",
}


def build_quest_brief(cell: Cell, template_id: str, scope: str, target_kind: str) -> str:
    """The user-facing brief -- names the cell's own fixed facts so the model writes `name`/
    `flavor` that actually fit the template, without repeating the schema's own enum lists (those
    are already enforced by constrained decoding; repeating them in prose only invites the model to
    treat them as suggestions rather than a closed set)."""
    kind_hint = {
        "none": "no specific target -- the objective itself is the whole ask",
        "boss": "the domain's own single boss -- never named, there is only one",
        "room-kind": "a kind of room the party must clear",
        "curio-kind": "a kind of event the party must find and resolve",
        "item-kind": "a kind of item the party must carry out of the domain",
    }[target_kind]
    return (
        f"Objective template: {template_id}. Target: {kind_hint}. "
        f"{_SCOPE_HINT[scope]} Write a `name` (a short quest title) and `flavor` (one to two "
        f"sentences) that fit this exact template, target and scope -- a quest at a different scope "
        f"needs a genuinely different name and flavor, even for the same objective template, since "
        f"what earns it and who it is about are different. Nothing else."
    )
