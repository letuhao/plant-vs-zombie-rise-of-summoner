"""seedsmith.adapters.trees.species.generate_tree — the per-species orchestration caller (task J8's
own stated remainder / task J9's real prerequisite, spec-species-tree.md whole-module). Sequences
every REAL, tested, independently-provable piece this program built for species trees into ONE
species' own committed output:

    favour-fit (§3.1 step 3) -> species_tree_spec/build_plan (§1/§4) -> quota_for_plan ->
    run_language_stage (the shared, resumable H1-H8 node-generation loop) ->
    mark_species_unique_nodes (§5.3 rule 3) -> codex-summary (§6)

**A real, stated design decision, not an invented one**: the 40 generated NODES are written to the
SAME shared `data/seed/passive-tree/nodes/<speciesId>.json` every other tree category already uses
(`nodegen.emit.write_seed_document`, unchanged, category-agnostic) — species trees reuse the node
RECORD verbatim (§1's own table), and reusing its STORAGE location too needs no new code at all.
`data/seed/passive-tree/species/<speciesId>.json` (spec's own Project structure table) is this
module's own NEW, small, species-SPECIFIC supplement — `codexSummary`, the resolved favour lock, and
the marked namespace-affix node ids — never a second copy of the 40 nodes already committed above.

**What this module does NOT do, by design, matching this program's own "a run is not authoring"
split (already used for J7's affix corpus and J9 itself)**: it never decides how many species to
run, never loops over a roster, and never schedules the real 105,840-call production pass — it is
the unit of work J9's own real run would call once per species, proven correct for ONE call here
rather than assumed correct at 840x scale.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from ....pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig
from ....workflow.runner import MAX_WORKERS
from ...trees.plan.emit import REPO_ROOT as PLAN_REPO_ROOT
from .generate_codex import resolve_codex_summaries
from .generate_favour_fit import resolve_favour_fit
from .plan import FavourCell, mark_species_unique_nodes
from .roster import SpeciesAnchor

REPO_ROOT = PLAN_REPO_ROOT


def species_metadata_path(species_id: str, seed_root: "Path | None" = None) -> Path:
    root = seed_root or (REPO_ROOT / "data" / "seed")
    return root / "passive-tree" / "species" / f"{species_id}.json"


class SpeciesTreeRunError(ValueError):
    """A species tree run could not proceed to the next stage — refused, never silently skipped."""


@dataclass(frozen=True)
class SpeciesTreeRunResult:
    species_id: str
    resolved_cell: "FavourCell | None"
    favour_unresolved_reason: "str | None"
    node_key_refused_reason: "str | None"
    nodes_seed_path: "Path | None"
    outcome_counts: "Mapping[str, int]"
    marked_node_ids: "frozenset[str]"
    codex_summary: "str | None"
    codex_unresolved_reason: "str | None"
    metadata_path: "Path | None"


def run_species_tree(
    species_id: str, anchor: SpeciesAnchor, ordinal: int,
    offered_cell: FavourCell, alternates: "Sequence[FavourCell]", *,
    targets: object, tuning: dict, speciesUniqueAffixMin: "int | None" = None,
    ledger_path: "Path | None" = None, seed_root: "Path | None" = None,
    call: "Callable[..., str] | None" = None, config: LlmCallerConfig = DEFAULT_CONFIG,
    workers: int = MAX_WORKERS,
) -> SpeciesTreeRunResult:
    """One species, start to finish. `offered_cell`/`alternates` are `assign_favour_cells`'s own
    per-species output (task J5) — this function does not run the corpus-wide quota itself, since
    that is a one-time, whole-roster computation a caller runs ONCE and passes every species' own
    slice of, never re-derived per species.

    Refuses to proceed past an unresolved favour (never generates a tree for a lock nothing
    confirmed) or an unresolved codex summary (never ships a tree with no Codex sentence, matching
    this module's own Success criteria) — both are reported as `SpeciesTreeRunResult` fields, never
    raised, so a caller running many species can collect every outcome rather than stopping at the
    first one that needs review.
    """
    from ...trees.nodegen import emit as emit_mod
    from ...trees.nodegen import quota as quota_mod
    from ...trees.nodegen import run as run_mod
    from ...trees.nodegen import vocab as vocab_mod
    from ...trees.nodegen import plan_read
    from ...trees.plan import emit as plan_emit

    provenance_base = {"pipeline": "species-tree", "model": config.model}

    fresh_favour, unresolved_favour, _ = resolve_favour_fit(
        [(species_id, anchor, offered_cell, alternates)], provenance_base=provenance_base,
        call=call, config=config, workers=workers)
    if species_id in unresolved_favour:
        return SpeciesTreeRunResult(
            species_id=species_id, resolved_cell=None,
            favour_unresolved_reason=unresolved_favour[species_id]["reason"],
            node_key_refused_reason=None,
            nodes_seed_path=None, outcome_counts={}, marked_node_ids=frozenset(),
            codex_summary=None, codex_unresolved_reason=None, metadata_path=None)
    resolved_cell = fresh_favour[species_id]["cell"]

    spec = plan_emit.species_tree_spec(
        species_id, ordinal,
        (resolved_cell.aptitude, resolved_cell.element, resolved_cell.status), seed_root)
    plan_dict = plan_emit.build_plan(spec, tuning)
    tree_plan = plan_read.load_from_dict(plan_dict, source_label=f"species:{species_id}")

    cells = quota_mod.quota_for_plan(
        tree_plan, targets, category="species",
        forced_element=resolved_cell.element, forced_status=resolved_cell.status)
    affix_vocab = vocab_mod.build()

    def inputs_for(subject: "run_mod.Subject") -> "run_mod.NodeGenerationInputs":
        cell = cells[subject.node_id]
        permitted_affixes = affix_vocab.permitted_for_branch(subject.branch)
        return run_mod.NodeGenerationInputs(
            tree_display_name=species_id, tree_reading=species_id,
            motifs=(), anti_motifs=(), anti_motif_tags=(),
            permitted_affixes=permitted_affixes,
            permitted_properties=sorted(tree_plan.property_vocabulary),
            property_vocabulary=tree_plan.property_vocabulary, affix_vocab=affix_vocab,
            # 2026-09-11 (A2): a species node's own quota cell travels with it, same as the
            # generic CLI's inputs_for — `generate_node` narrows the exclusion enum to this cell.
            quota_cell=cell)

    # Real-call finding (2026-09-07, `AbyssSwordStar`'s own first live proof-of-concept run): the
    # model independently generated two DIFFERENT nodes both named "Abyssal Shell", and
    # `build_seed_document`'s own `assert_no_duplicate_name_keys` correctly refused rather than
    # silently renaming one out from under the model's answer -- the exact same defect class
    # `_cmd_trees_generate`'s own docstring already names for `might`'s real run ("two DIFFERENT
    # accepted nodes collided on nameKey"), caught there by the identical try/except this mirrors.
    # The ledger itself is not at risk either way: `run_language_stage` writes it before ever
    # building the seed document, so every already-accepted node from this attempt stays safely
    # recorded regardless of this refusal. This function's own first draft had no such guard and
    # crashed the caller outright -- fixed to report it as a structured outcome instead, matching
    # every other "needs another pass, never a crash" result this function already returns.
    try:
        language_result = run_mod.run_language_stage(
            tree_plan, inputs_for, ledger_path=ledger_path, seed_root=seed_root, config=config,
            unresolved_max_share_permille=getattr(targets, "unresolved_count_max_share_permille", None),
            max_workers=workers)
    except emit_mod.NodeKeyRefused as ex:
        return SpeciesTreeRunResult(
            species_id=species_id, resolved_cell=resolved_cell, favour_unresolved_reason=None,
            node_key_refused_reason=str(ex),
            nodes_seed_path=None, outcome_counts={}, marked_node_ids=frozenset(),
            codex_summary=None, codex_unresolved_reason=None, metadata_path=None)

    # `NodeSeedRecord.to_dict()` already carries `id`/`nodeKey`/`branch`/`tier`/`nodeClass` as
    # plan-side identity fields it never re-derives (`emit.py`'s own docstring) -- exactly the
    # shape `mark_species_unique_nodes` needs, with no second lookup back into the plan required.
    accepted_records = [
        o.record.to_dict() for o in language_result.outcomes
        if o.outcome == "accepted" and o.record is not None
    ]
    k = speciesUniqueAffixMin if speciesUniqueAffixMin is not None else getattr(
        targets, "species_unique_affix_min", 8)
    marked = mark_species_unique_nodes(accepted_records, k)

    outcome_counts: "dict[str, int]" = {}
    for o in language_result.outcomes:
        outcome_counts[o.outcome] = outcome_counts.get(o.outcome, 0) + 1

    fresh_codex, unresolved_codex, _ = resolve_codex_summaries(
        [(species_id, anchor, resolved_cell)], provenance_base=provenance_base,
        call=call, config=config, workers=workers)
    codex_summary = fresh_codex[species_id]["codexSummary"] if species_id in fresh_codex else None
    codex_unresolved_reason = unresolved_codex.get(species_id, {}).get("reason")

    metadata_path = None
    if codex_summary is not None:
        metadata_path = species_metadata_path(species_id, seed_root)
        metadata_path.parent.mkdir(parents=True, exist_ok=True)
        metadata_doc = {
            "schemaVersion": 1, "speciesId": species_id,
            "mechanicalFavour": {"aptitude": resolved_cell.aptitude, "element": resolved_cell.element,
                                 "status": resolved_cell.status},
            "codexSummary": codex_summary,
            "speciesUniqueNodeIds": sorted(marked),
        }
        metadata_path.write_text(
            json.dumps(metadata_doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    return SpeciesTreeRunResult(
        species_id=species_id, resolved_cell=resolved_cell, favour_unresolved_reason=None,
        node_key_refused_reason=None,
        nodes_seed_path=language_result.seed_path, outcome_counts=outcome_counts,
        marked_node_ids=marked, codex_summary=codex_summary,
        codex_unresolved_reason=codex_unresolved_reason, metadata_path=metadata_path)
