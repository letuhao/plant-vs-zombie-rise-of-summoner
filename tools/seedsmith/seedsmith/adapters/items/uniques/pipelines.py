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
from typing import Any, Callable, Iterable, Mapping

from ...creatures.anchor.permute import order_for
from ...creatures.anchor.vote import resolve_vote
from ....metrics.dedup import canonical_words
from .. import registries as _reg
from ....pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig, call_model, extract_json
from .briefs import SYSTEM_PROMPT, assemble_tags, build_brief, build_unique_schema
from .planner import Cell

SAMPLES_PER_DRAW = 3
MAX_PARSE_HEAL = 2

#: `tags.v1.json`'s own `exclusive: false` axis (registries.py's `load_tag_axes` drops the flag,
#: since only ONE of the seven real axes needs it) -- every other axis returned for `unique`
#: allows at most one member; this one allows any number, including zero.
NON_EXCLUSIVE_AXES = frozenset({"economy-source"})

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

    # Walk `out` (the deepcopy), never `schema` -- walking `schema` itself would both mutate the
    # caller's input as a side effect AND return a copy taken before that mutation, one step
    # behind what was just computed (self-caught 2026-09-06: a retry-attempt test exposed a
    # dry-run and a real run seeing DIFFERENT permutations for what should be an IDENTICAL first
    # call, which is only possible if some earlier call had already corrupted shared state).
    for field, sub in out.get("properties", {}).items():
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


def _duplicate_fixed_atom_family(fixed_atoms: "list[Mapping[str, str]]") -> "str | None":
    """A real batch run (2026-09-06) measured `uniqueItems: true` on `fixedAtoms` NOT catching a
    real duplicate -- two identical `{family: atom.vitality, powerBand: high}` rows both survived
    a live call. `uniqueItems` is a whole-object-equality constraint, and grammar-based constrained
    decoding (LM Studio's GBNF sampling) enforces per-token/per-field shape, not a semantic
    cross-item equality check -- the schema-level claim was real but not backed by the actual
    decoder. This is the post-hoc backstop, checked on FAMILY alone (not the (family, powerBand)
    pair): every sampled real anchor in the 144-corpus uses two DISTINCT families, never the same
    family twice at any power band, so a repeat family is content-wrong even when the two
    powerBands differ."""
    families = [a.get("family") for a in fixed_atoms]
    seen: "set[str]" = set()
    for family in families:
        if family in seen:
            return f"fixedAtoms repeats family {family!r} -- every real anchor in the corpus uses distinct families"
        seen.add(family)
    return None


def _tag_axis_violation(tags: "list[str]", tag_axes: "Mapping[str, tuple[str, ...]]") -> "str | None":
    """`tags.v1.json`'s own per-axis exclusivity rule, checked post-hoc -- the JSON Schema's flat
    `enum` list can restrict which STRINGS are legal but not "at most one from this subset,
    for six different subsets, in one array field", so a model can satisfy the schema while still
    violating the real rule (confirmed live 2026-09-06: a real smoke call against the local model
    happened to produce a compliant tag set, but nothing structural guaranteed it would). Also
    enforces spec §1's "exactly one mass-class" requirement. Returns the violation reason, or
    `None` when clean."""
    tag_to_axis = {t: axis for axis, ids in tag_axes.items() for t in ids}
    seen_per_axis: "dict[str, str]" = {}
    mass_class_count = 0
    for tag in tags:
        axis = tag_to_axis.get(tag)
        if axis is None:
            continue
        if axis == "mass-class":
            mass_class_count += 1
        if axis in NON_EXCLUSIVE_AXES:
            continue
        if axis in seen_per_axis and seen_per_axis[axis] != tag:
            return f"more than one {axis} tag: {seen_per_axis[axis]!r} and {tag!r}"
        seen_per_axis[axis] = tag
    if mass_class_count != 1:
        return f"exactly one mass-class tag is required, found {mass_class_count}"
    return None


def run_unique_draws(
    cells: "list[Cell]",
    planned_ids_by_cell: "Mapping[str, dict[str, str]]",
    *,
    call: "CallFn | None" = None,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    schema_kwargs: "dict[str, Any] | None" = None,
    tag_axes: "Mapping[str, tuple[str, ...]] | None" = None,
    attempt: int = 0,
    existing_names: "Iterable[str] | None" = None,
    role_by_cell: "Mapping[str, str] | None" = None,
) -> "list[DrawResult]":
    """One draw per cell: `SAMPLES_PER_DRAW` (3, fixed -- `resolve_vote`'s own hard contract)
    permuted calls, `name` majority-voted, the winning sample's own complete object used for every
    other field. A 1-1-1 split on `name`, or fewer than 3 samples ever parsing, is `unresolved` --
    never a guess (AI-native contract: "1-1-1 -> unresolved, never the first option").

    `call` defaults to the real local transport (`llm_caller.call_model`) so a real run needs no
    caller-side wiring; a test passes a stub that raises, proving this function makes zero calls
    on any path that should not reach the model.

    `existing_names` seeds the cross-cell name-collision check (see `canonical_words` use below)
    with names the caller already considers taken -- the real 144-corpus's own names, or an
    earlier attempt's already-resolved entries when the batch script retries only the leftovers.
    Real finding, 2026-09-06: with nothing differentiating a firstseed vs. almanac brief beyond the
    rarity const, the model repeatedly wrote the IDENTICAL name for both -- `ItemSeedValidator`'s
    own `NameCollision` check caught 4 of these in the first real 20-cell batch. Checked WITHIN
    this call too (cells share the running `used` set as they resolve), so two cells in the SAME
    `cells` list can never collide with each other either.

    `role_by_cell` overrides `role_for_cell(cell)`'s default per cell-key -- see
    `build_unique_schema`'s own docstring for why the default grid rotation cannot be used when
    shipping under a real, registered ordinal (70) with its own already-occupied role/axis slots.
    """
    call = call or call_model
    schema_kwargs = schema_kwargs or {}
    role_by_cell = role_by_cell or {}
    # Falls back to `schema_kwargs`'s OWN `tag_axes` (the same fixture/registry snapshot the
    # schema itself was built from) before a fresh registry read -- passing two independently-
    # supplied `tag_axes` for "what the model may choose" vs. "what the violation check accepts"
    # is exactly how the two could silently drift apart; one real source, used both places.
    tag_axes = tag_axes if tag_axes is not None else schema_kwargs.get("tag_axes") or _reg.load_tag_axes(applies_to="unique")
    used_names: "set[frozenset[str]]" = {canonical_words(n) for n in (existing_names or ())}
    results: "list[DrawResult]" = []

    for cell in cells:
        # `attempt` shifts the permutation seed for a retried draw (batch-level policy, e.g. after
        # an unresolved vote or a tag-axis violation) -- never a second call at the SAME seed,
        # which would just reproduce the identical three samples and the identical outcome.
        draw_id = f"unique-draw-{cell.cell_key}" if attempt == 0 else f"unique-draw-{cell.cell_key}-retry{attempt}"
        planned_ids = planned_ids_by_cell[cell.cell_key]
        role = role_by_cell.get(cell.cell_key)
        base_schema = build_unique_schema(cell, planned_ids, role=role, **schema_kwargs)

        samples: "list[dict]" = []
        for sample_index in range(SAMPLES_PER_DRAW):
            schema = _permute_schema_enums(base_schema, draw_id=draw_id, sample_index=sample_index)
            brief = build_brief(cell, schema, role=role)
            try:
                out = _call_and_parse(call, SYSTEM_PROMPT, brief, schema=schema, config=config)
            except RuntimeError:
                continue
            samples.append(out)

        if len(samples) != SAMPLES_PER_DRAW:
            results.append(DrawResult(cell.cell_key, None, reason="insufficient_valid_samples"))
            continue

        name_vote = resolve_vote([s.get("name", "") for s in samples])
        if name_vote.value is None:
            results.append(DrawResult(cell.cell_key, None, reason="vote_unresolved",
                                      vote_confidence=name_vote.confidence))
            continue

        name_words = canonical_words(name_vote.value)
        if name_words in used_names:
            results.append(DrawResult(cell.cell_key, None,
                                      reason=f"name_collision: {name_vote.value!r} normalizes to an already-used name",
                                      vote_confidence=name_vote.confidence))
            continue
        used_names.add(name_words)

        winner = next(s for s in samples if s.get("name") == name_vote.value)
        entry = dict(winner)
        entry["name"] = name_vote.value
        assemble_tags(entry)   # five single-value fields -> the real tags[] array (briefs.py)

        # Defensive, not load-bearing: `assemble_tags` makes a violation structurally unreachable
        # (each of its five inputs is already a constrained-decoded single enum value), but the
        # check is cheap and catches a future bug in the assembly itself rather than trusting it.
        violation = _tag_axis_violation(entry.get("tags") or [], tag_axes)
        if violation is not None:
            results.append(DrawResult(cell.cell_key, None, reason=f"tag_axis_violation: {violation}",
                                      vote_confidence=name_vote.confidence))
            continue

        # NOT defensive -- `uniqueItems` on `fixedAtoms` measured live as unenforced by the actual
        # decoder (see the function's own docstring), so this is the only real backstop today.
        dup = _duplicate_fixed_atom_family(entry.get("fixedAtoms") or [])
        if dup is not None:
            results.append(DrawResult(cell.cell_key, None, reason=f"duplicate_fixed_atom: {dup}",
                                      vote_confidence=name_vote.confidence))
            continue

        results.append(DrawResult(cell.cell_key, entry, vote_confidence=name_vote.confidence))

    return results
