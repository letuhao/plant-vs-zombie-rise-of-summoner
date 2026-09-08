"""seedsmith.adapters.trees.species.generate_codex — the codex-summary STAGE's own resolution
function (task J8, spec-species-tree.md §6, §7.1). Its sibling stage,
`species.generate_favour_fit` (§3.1 step 3), is the SAME shape in a separate file — one file per
stage, matching `adapters.effects.affix`'s own one-file-per-pipeline convention. Pure
result-producing, no persistence: matching
`species/plan.py`'s own scope discipline (a DETERMINISTIC half exists with no file writer of its
own), this module is the MODEL-CALLING half with the identical property — it returns results in
memory and never decides where a committed per-species seed file lives, because that file does not
exist yet. `data/seed/passive-tree/species/<speciesId>.json` (spec's own Project structure table:
"THE SEED — enums + prose + codexSummary") is written ALONGSIDE a species tree's own generated
nodes, by the still-unbuilt full orchestration this task (J8) also owns — building a throwaway
output path for this stage alone, ahead of that real writer, would just be work to redo.

3-way voted via EXACT-MATCH `resolve_vote` — the SAME machinery `generate_affixes.py`'s own `name`
field already uses, never a bespoke fuzzy-sentence-similarity scheme invented here (this program's
own "ask first before forking any piece of the reused machinery" boundary). Whether three
independently-generated sentences converge often enough in practice is exactly the kind of thing
the real run (J9) measures — the same posture `resolve_set_vote`'s own fix was only made after a
real run measured the plain `resolve_vote` shape at ~90% unresolved for a DIFFERENT, multi-member
field. Never assumed here either way.
"""
from __future__ import annotations

from typing import Any, Callable, Mapping, Sequence

from ....pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig
from ....workflow.runner import MAX_WORKERS
from .plan import FavourCell
from .roster import SpeciesAnchor

SAMPLES_PER_SPECIES = 3


def resolve_codex_summaries(
    species: "Sequence[tuple[str, SpeciesAnchor, FavourCell]]", *,
    provenance_base: "Mapping[str, Any]",
    call: "Callable[..., str] | None" = None,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    workers: int = MAX_WORKERS,
) -> "tuple[dict[str, dict], dict[str, dict], dict[str, dict]]":
    """`species` is `(speciesId, anchor, favourCell)` triples — the caller's own already-resolved
    roster+favour-lock join, never re-derived here (this module owns the model call, not the
    upstream data). Returns `(fresh, unresolved, results)`:

    * `fresh` — `{speciesId: {"codexSummary": str, "_provenance": {...}}}`, one entry per species
      whose vote resolved (3-0 or 2-1). A resolved value can never itself carry a content defect:
      `codex_summary_content_is_clean` (`workflow/graphs/species_codex.py`) already gates every
      SAMPLE that reaches `persisted` on the identical `codex_summary_defects` check this module
      would otherwise repeat post-vote, and `resolve_vote` only ever returns one of the samples it
      was given verbatim — never a synthesized value — so a validator-clean input set makes a
      dirty RESOLVED value a logical impossibility, not merely an untested case. (Caught while
      writing this module's own tests: an earlier draft re-checked the resolved value anyway and
      the "content defect survives the vote" test could not be made to fail without disabling the
      validator, proving the branch it was testing was dead code, not an untested one — removed.)
    * `unresolved` — `{speciesId: {"reason": ..., ...}}`: `"vote_unresolved"` (1-1-1) or
      `"insufficient_valid_samples"` (fewer than `SAMPLES_PER_SPECIES` samples ever validated —
      this is where a persistently numeric/jargon-laden brief shows up, since the validator repairs
      or drops those samples before they ever reach a vote).
    * `results` — the per-sample-state graph outcome, keyed by the sample's own subject id.
    """
    from ....workflow.graphs.species_codex import build_species_codex_graph, state_for_species_codex
    from ....workflow.runner import run_many
    from ...demons.anchor.vote import resolve_vote

    persisted: "dict[str, dict]" = {}
    app = build_species_codex_graph(
        on_persist=lambda k, v: persisted.__setitem__(k, v), config=config, call=call)

    species_samples: "dict[str, list[str]]" = {}
    states: "list[dict]" = []
    for species_id, anchor, favour in species:
        sample_ids: "list[str]" = []
        for sample_index in range(SAMPLES_PER_SPECIES):
            # A permuted seed per sample, seeded on speciesId|sample_index -- consistent with
            # every other voted stage in this program, even though this brief has no OPTION LIST
            # to permute; `order_for` is not called here for exactly that reason (nothing to
            # shuffle), and the vote's own three independent samples are what "votes", not a
            # permutation of a fixed option order.
            subject_id = f"{species_id}-codex-sample-{sample_index}"
            state = state_for_species_codex(subject_id, anchor, favour)
            states.append(state)
            sample_ids.append(subject_id)
        species_samples[species_id] = sample_ids

    results = run_many(app, states, max_workers=workers)

    fresh: "dict[str, dict]" = {}
    unresolved: "dict[str, dict]" = {}
    for species_id, sample_ids in species_samples.items():
        samples = [
            persisted[sid] for sid in sample_ids
            if sid in persisted and isinstance(persisted[sid], dict) and "codexSummary" in persisted[sid]
        ]
        if len(samples) != SAMPLES_PER_SPECIES:
            unresolved[species_id] = {
                "reason": "insufficient_valid_samples",
                "validSamples": len(samples), "samplesExpected": SAMPLES_PER_SPECIES,
            }
            continue

        vote = resolve_vote([s.get("codexSummary", "") for s in samples])
        if vote.value is None:
            unresolved[species_id] = {"reason": "vote_unresolved", "confidence": vote.confidence}
            continue

        provenance = dict(provenance_base)
        provenance["voteConfidence"] = vote.confidence
        if vote.minority:
            provenance["voteMinority"] = vote.minority
        fresh[species_id] = {"codexSummary": vote.value, "_provenance": provenance}

    return fresh, unresolved, results
