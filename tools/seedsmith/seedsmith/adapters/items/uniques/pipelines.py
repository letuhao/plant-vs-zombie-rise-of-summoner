"""The uniques pipelines (D4.29, spec-unique-pipeline.md §1) -- the boundary. Calls the model
`SAMPLES_PER_DRAW` permuted times per cell, majority-votes the one true load-bearing field
(`name`), and self-heals a sample that fails to parse. `call` is always injected, never imported
directly -- the same contract every pipeline in this program already honours, so a test proves
zero real model calls happen on a schema-only or dry-run path.

**A simpler vote than `effects/affix/generate_affixes.py`'s own `run_voted_draws`, on purpose.**
That pipeline votes a variable-length ref BUNDLE with no single anchor field (`resolve_set_vote`,
fixed 2026-09-06 after whole-bundle voting resolved ~10% of draws). A unique anchor has one: the
free-text `name` a model writes is the best available signal that three independent tries
converged on the same real concept, and every OTHER field this schema exposes
(`baseType`/`fixedAtoms[].family`/`varianceSlot.family`/`tags`) is already drawn from a real
closed enum under LM Studio's constrained decoding (`llm_caller.call_model`'s own `schema=`
parameter) -- an illegal value is already unsampleable on every individual sample, so the residual
risk a cross-sample vote would catch is presentation-order bias, which permuting each sample's own
enum order (`order_for`) already mitigates per `SAMPLES_PER_DRAW` sample. Voting `name` and then
keeping the REST of the winning sample's own object intact (rather than recombining fields across
samples) keeps flavor text, fixed atoms and counterPressure internally coherent -- recombining a
winning `baseType` from one sample with `flavor` text written about a different sample's pick is
the exact kind of incoherent output a structural vote would silently produce.
"""
from __future__ import annotations

import copy
import json
from dataclasses import dataclass
from typing import Any, Callable, Mapping

from ...demons.anchor.permute import order_for
from ...demons.anchor.vote import resolve_vote
from ...pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig, call_model, extract_json
from .briefs import SYSTEM_PROMPT, build_brief, build_unique_schema
from .planner import Cell

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
    """A fresh copy of `schema` with every `enum` list reordered by `order_for(draw_id, field,
    sample_index, values)` -- the AI-native contract's "permute every enum, seeded from
    (entity_id, field, sample_index)" rule. `sample_index` lives INSIDE the seed, never as a
    separate re-roll, so a rerun over the same draw id reproduces the identical three
    permutations (seed-contract §6)."""
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

    for field, sub in schema.get("properties", {}).items():
        walk(sub, field)
    return out


def _call_and_parse(call: CallFn, system: str, user: str, *, schema: dict,
                    config: LlmCallerConfig) -> dict:
    """One sample: call, parse, and self-heal a pure JSON-shape failure (the residual failure mode
    once constrained decoding already rules out an illegal enum value) by re-asking for strictly
    valid JSON. Never silently drops a sample -- a sample that never parses raises, and the caller
    (`run_unique_draws`) records it as `insufficient_valid_samples` rather than guessing."""
    for attempt in range(MAX_PARSE_HEAL + 1):
        raw = call(system, user, config=config, schema=schema)
        try:
            return extract_json(raw)
        except (ValueError, json.JSONDecodeError):
            user = ("Your previous output could not be parsed as valid JSON. "
                    "Re-emit ONLY a strictly-valid JSON object matching the schema, nothing else.")
    raise RuntimeError(f"model never returned parseable JSON after {MAX_PARSE_HEAL} heal attempt(s)")


def run_unique_draws(
    cells: "list[Cell]",
    planned_ids_by_cell: "Mapping[str, dict[str, str]]",
    *,
    call: "CallFn | None" = None,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    samples_per_draw: int = SAMPLES_PER_DRAW,
    schema_kwargs: "dict[str, Any] | None" = None,
) -> "list[DrawResult]":
    """One draw per cell: `samples_per_draw` permuted calls, `name` majority-voted
    (`resolve_vote`), the winning sample's own complete object used for every other field. A 1-1-1
    split on `name`, or fewer than `samples_per_draw` samples ever parsing, is `unresolved` --
    never a guess (AI-native contract: "1-1-1 -> unresolved, never the first option").

    `call` defaults to the real local transport (`llm_caller.call_model`) so a real run needs no
    caller-side wiring; a test passes a stub that raises, proving this function makes zero calls
    on any path that should not reach the model.
    """
    call = call or call_model
    schema_kwargs = schema_kwargs or {}
    results: "list[DrawResult]" = []

    for cell in cells:
        draw_id = f"unique-draw-{cell.cell_key}"
        planned_ids = planned_ids_by_cell[cell.cell_key]
        base_schema = build_unique_schema(cell, planned_ids, **schema_kwargs)

        samples: "list[dict]" = []
        for sample_index in range(samples_per_draw):
            schema = _permute_schema_enums(base_schema, draw_id=draw_id, sample_index=sample_index)
            brief = build_brief(cell, schema)
            try:
                out = _call_and_parse(call, SYSTEM_PROMPT_FOR(cell), brief, schema=schema, config=config)
            except RuntimeError:
                continue
            samples.append(out)

        if len(samples) != samples_per_draw:
            results.append(DrawResult(cell.cell_key, None, reason="insufficient_valid_samples"))
            continue

        name_vote = resolve_vote([s.get("name", "") for s in samples])
        if name_vote.value is None:
            results.append(DrawResult(cell.cell_key, None, reason="vote_unresolved",
                                      vote_confidence=name_vote.confidence))
            continue

        winner = next(s for s in samples if s.get("name") == name_vote.value)
        entry = dict(winner)
        entry["name"] = name_vote.value
        results.append(DrawResult(cell.cell_key, entry, vote_confidence=name_vote.confidence))

    return results


def SYSTEM_PROMPT_FOR(cell: Cell) -> str:  # noqa: N802 -- reads as a constant-shaped helper at call sites
    from .briefs import SYSTEM_PROMPT
    return SYSTEM_PROMPT
