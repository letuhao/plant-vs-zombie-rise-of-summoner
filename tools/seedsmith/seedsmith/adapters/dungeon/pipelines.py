"""D1.10 (spec-dungeon-seed-contract.md §1, "Pipelines with permuted enums and majority vote") --
the boundary. `call` is always injected, never imported directly, matching every other pipeline in
this program (`items/uniques/pipelines.py`'s own established contract): a test proves zero real
model calls happen on a schema-only or dry-run path.

**Quest votes THREE fields, not one -- a real difference from the uniques pipeline, not an
oversight.** `spec-dungeon-seed-contract.md` §1.5's own field table marks `name`, `countBand` AND
`rewardBand` all "AUTHORED, voted" (uniques votes only `name`). `countBand`/`rewardBand` are
abstract MECHANICAL parameters with no narrative coupling to `flavor` -- unlike a unique's
`baseType`, voting them independently of which sample's `name` won carries no "recombined,
incoherent flavor" risk (`items/uniques/pipelines.py`'s own documented reason for keeping a
winning sample's other fields together). `flavor` stays tied to whichever sample's `name` won the
vote, since it IS narrative prose about that specific name.
"""
from __future__ import annotations

import copy
import json
from dataclasses import dataclass
from typing import Any, Callable, Mapping

from ..demons.anchor.permute import order_for
from ..demons.anchor.vote import resolve_vote
from ...metrics.dedup import canonical_words
from ...pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig, call_model, extract_json
from . import registries as _reg
from .briefs import QUEST_SYSTEM_PROMPT, build_quest_brief, build_quest_schema_for_cell
from .planner import Cell
from .schema import EVENT_KIND, EVENT_REPEAT_SCOPE

SAMPLES_PER_DRAW = 3
MAX_PARSE_HEAL = 2

CallFn = Callable[..., str]   # (system, user, *, config, schema) -> raw text -- call_model's own shape


@dataclass(frozen=True)
class DrawResult:
    cell_key: str
    entry: "dict[str, Any] | None"   # None iff unresolved
    reason: "str | None" = None      # set iff entry is None
    vote_confidence: "str | None" = None


def _permute_schema_enums(schema: dict, *, draw_id: str, sample_index: int) -> dict:
    """Identical shape to `items/uniques/pipelines.py`'s own function -- not imported from there
    (that module is items-adapter-local, matching this program's own established "each generator
    keeps its own local copy of a small orchestration helper" precedent, `combogen`/`setgen`'s own
    already-cited reason). Walks `out` (the deepcopy), never `schema` -- the exact mutate-the-input
    bug `items/uniques/pipelines.py` self-caught 2026-09-06, guarded against here from the start
    rather than re-discovering it."""
    out = copy.deepcopy(schema)

    def walk(node: Any, field_hint: "str | None") -> None:
        if isinstance(node, dict):
            if isinstance(node.get("enum"), list) and field_hint is not None:
                node["enum"] = order_for(draw_id, field_hint, sample_index, node["enum"])
            for key, sub in node.get("properties", {}).items():
                walk(sub, key)
            items = node.get("items")
            if isinstance(items, dict):
                walk(items, field_hint)

    for field, sub in out.get("properties", {}).items():
        walk(sub, field)
    return out


def _call_and_parse(call: CallFn, system: str, user: str, *, schema: dict, config: LlmCallerConfig) -> dict:
    """Identical shape to `items/uniques/pipelines.py`'s own function -- see that module's own
    doc comment for why a sample that never parses raises rather than being silently dropped."""
    for attempt in range(MAX_PARSE_HEAL + 1):
        raw = call(system, user, config=config, schema=schema)
        try:
            return extract_json(raw)
        except (ValueError, json.JSONDecodeError):
            user = ("Your previous output could not be parsed as valid JSON. "
                    "Re-emit ONLY a strictly-valid JSON object matching the schema, nothing else.")
    raise RuntimeError(f"model never returned parseable JSON after {MAX_PARSE_HEAL} heal attempt(s)")


def run_quest_draws(
    cells: "list[Cell]",
    planned_ids_by_cell: "Mapping[str, str]",
    *,
    call: "CallFn | None" = None,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    objective_templates: "dict[str, dict] | None" = None,
    room_kinds: "frozenset[str] | None" = None,
    event_kinds: "tuple[str, ...] | None" = None,
    reward_bands: "frozenset[str] | None" = None,
    count_bands: "frozenset[str] | None" = None,
    repeat_scopes: "tuple[str, ...] | None" = None,
    attempt: int = 0,
    existing_names: "Mapping[str, Any] | None" = None,
) -> "list[DrawResult]":
    """One draw per cell: `SAMPLES_PER_DRAW` (3) permuted calls; `name`, `countBand` and
    `rewardBand` EACH majority-voted independently; a 1-1-1 split on ANY of the three, or fewer
    than 3 samples ever parsing, is `unresolved` for the whole entry -- never a partial guess
    (AI-native contract: "1-1-1 -> unresolved, never the first option", applied per vote, and a
    quest with only two of its three load-bearing fields resolved is not a quest this pipeline
    will ship). `flavor`/`targetRef`/`prereqRefs`/`chainRef` come from whichever sample's `name`
    won its own vote -- the narratively-coupled fields stay with the sample that wrote them.

    `objective_templates`/`room_kinds`/`event_kinds`/`reward_bands`/`count_bands`/`repeat_scopes`
    default to a fresh registry read (`registries.py`) when omitted -- explicit parameters exist so
    a test can supply a small fixture instead of the real, larger corpus.
    """
    call = call or call_model
    objective_templates = objective_templates if objective_templates is not None else _reg.load_objective_templates()
    room_kinds = room_kinds if room_kinds is not None else frozenset(_reg.load_room_kinds())
    event_kinds = event_kinds if event_kinds is not None else EVENT_KIND
    reward_bands = reward_bands if reward_bands is not None else _reg.load_bands()["rewardBand"]
    count_bands = count_bands if count_bands is not None else _reg.load_bands()["countBand"]
    repeat_scopes = repeat_scopes if repeat_scopes is not None else EVENT_REPEAT_SCOPE

    used_names: "set[frozenset[str]]" = {canonical_words(n) for n in (existing_names or ())}
    results: "list[DrawResult]" = []

    for cell in cells:
        draw_id = f"quest-draw-{cell.cell_key}" if attempt == 0 else f"quest-draw-{cell.cell_key}-retry{attempt}"
        quest_id = planned_ids_by_cell[cell.cell_key]
        template_id, scope = cell.dimension_values
        target_kind = objective_templates[template_id]["targetKind"]
        base_schema = build_quest_schema_for_cell(
            cell, quest_id, objective_templates=objective_templates, room_kinds=room_kinds,
            event_kinds=event_kinds, reward_bands=reward_bands, count_bands=count_bands,
            repeat_scopes=repeat_scopes)
        brief = build_quest_brief(cell, template_id, scope, target_kind)

        samples: "list[dict]" = []
        for sample_index in range(SAMPLES_PER_DRAW):
            schema = _permute_schema_enums(base_schema, draw_id=draw_id, sample_index=sample_index)
            try:
                out = _call_and_parse(call, QUEST_SYSTEM_PROMPT, brief, schema=schema, config=config)
            except RuntimeError:
                continue
            samples.append(out)

        if len(samples) != SAMPLES_PER_DRAW:
            results.append(DrawResult(cell.cell_key, None, reason="insufficient_valid_samples"))
            continue

        name_vote = resolve_vote([s.get("name", "") for s in samples])
        if name_vote.value is None:
            results.append(DrawResult(cell.cell_key, None, reason="vote_unresolved:name", vote_confidence=name_vote.confidence))
            continue

        name_words = canonical_words(name_vote.value)
        if name_words in used_names:
            results.append(DrawResult(cell.cell_key, None,
                                      reason=f"name_collision: {name_vote.value!r} normalizes to an already-used name",
                                      vote_confidence=name_vote.confidence))
            continue

        count_band_vote = resolve_vote([s.get("countBand", "") for s in samples])
        if count_band_vote.value is None:
            results.append(DrawResult(cell.cell_key, None, reason="vote_unresolved:countBand", vote_confidence=count_band_vote.confidence))
            continue

        reward_band_vote = resolve_vote([s.get("rewardBand", "") for s in samples])
        if reward_band_vote.value is None:
            results.append(DrawResult(cell.cell_key, None, reason="vote_unresolved:rewardBand", vote_confidence=reward_band_vote.confidence))
            continue

        used_names.add(name_words)
        winner = next(s for s in samples if s.get("name") == name_vote.value)
        entry = dict(winner)
        entry["name"] = name_vote.value
        entry["countBand"] = count_band_vote.value
        entry["rewardBand"] = reward_band_vote.value

        results.append(DrawResult(cell.cell_key, entry, vote_confidence=name_vote.confidence))

    return results
