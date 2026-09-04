"""seedsmith.adapters.actions.family_propose.derive --- A-P2's own candidate assembly: per-sample
bounded self-heal (F9's adapted contract) and majority-vote resolution over `atomFamilies` ONLY
(spec-family-propose.md SS2 "Which fields are voted"). Reuses `demons.anchor.vote.resolve_vote`
and `pipeline.llm_caller.call_with_self_heal` directly, never reimplemented (this whole program's
own reuse discipline) -- exactly as `general_propose/derive.py` does.

**`motifsExpressed` is deliberately NOT voted** (spec SS2): it is a self-report feeding the
tier-3 review queue only, and tier 3 is advisory by design -- voting it would add a third of the
run's cost for a field that never rejects anything. It is taken straight from sample 0 (the same
"primary" sample that already supplies `name`/`flavor`/`rationale`, none of which are voted
either) -- stated once here rather than re-decided per field, mirroring
`general_propose/derive.py:finalize_candidate`'s own identical rule for its prose fields.

**Not a copy of `validate_heal.derive`.** A-S4 (`validate_heal`) is a LATER, separate stage that
gates and votes over whatever a generation pipeline hands it, against ITS OWN fixture schemas.
This module is the real A-P2: it owns its OWN schema (`prompts.FAMILY_ACTION_SCHEMA`), and it
never grades itself (spec SS3) -- so the vote/heal machinery here exists ONLY to turn three raw
model samples into one candidate row, never to decide final acceptance. `confidence` on that row
is read straight off `VoteResult`, never invented (binding constraint 4).
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any, Callable, Mapping, Sequence

from ....pipeline.llm_caller import LlmCallerConfig, call_with_self_heal
from ....pipeline.model import BLOCKED_FIELD
from ...demons.anchor.vote import VoteResult, resolve_vote
from .prompts import (
    SYSTEM_PROMPT,
    atom_families_are_allowed,
    atom_families_not_forbidden,
    build_brief,
    build_context,
    entry_for,
    motifs_expressed_are_known,
    motifs_expressed_exclude_anti_motifs,
    schema_for_call,
)

__all__ = [
    "SAMPLE_COUNT", "MAX_HEAL", "canonical_family_key", "build_verify_fn", "default_for_none",
    "sample_draft", "Candidate", "finalize_candidate", "propose_family_action",
    "candidate_row", "canonical_dump", "candidate_set_hash",
]

#: Spec SS2: three permuted samples, always. Never a config knob -- changing this changes the
#: vote's own arithmetic (a 1-1-1 split has no meaning over any other count).
SAMPLE_COUNT = 3

#: Acceptance #11: "Repairs are bounded at two ... passed explicitly -- the config default is 3."
MAX_HEAL = 2

#: A sample whose own self-heal was exhausted (F9: `default_for_none` returns `None` for the
#: still-failing key) contributes NO usable `atomFamilies` pick. It must still cast a VOTE --
#: silently dropping it from the count would let two real samples out-vote a total of two instead
#: of three, changing what "1-1-1" and "2-1" mean. This sentinel can never collide with a real
#: pick (canonical keys are `|`-joined atom-family ids, none of which is this literal string), so
#: it always counts as its own, distinct, losing vote. Identical convention to
#: `general_propose/derive.py`'s own sentinel, kept local (see `canonical_family_key`'s own
#: docstring for why this module stays self-contained rather than importing its sibling's).
_UNRESOLVED_SENTINEL = "\x00family-propose:unresolved-sample\x00"


def canonical_family_key(values: Sequence[str]) -> str:
    """Sorted + deduped + joined, so `["b","a"]` and `["a","b"]` vote as agreement rather than a
    2-1 split created by ordering alone. Independently defined here rather than imported from
    `general_propose.derive.canonical_family_key` or `validate_heal.derive.canonical_set_key` --
    the identical one-line convention, kept local so this module stays pure and self-contained
    (spec SS3: "one brief in, one candidate out"; sibling propose stages are peers, not shared
    dependencies of each other)."""
    return "|".join(sorted(set(values)))


def build_verify_fn(context: Mapping[str, Any]) -> Callable[[Mapping[str, Any], Mapping[str, Any]], "tuple[dict, dict]"]:
    """`call_with_self_heal`'s own `verify_fn(items, out) -> (hard, soft)`, closed over ONE
    rendered brief context. A declared block (`blocked` non-empty) short-circuits every other
    check -- an honest decline is an answer, never a defect to heal (the same discipline
    `general_propose/derive.py:build_verify_fn` states for the sibling stage). `motifsExpressed`
    is checked for BOTH planted-violation shapes spec SS4.3 names: a motif outside this brief's
    own list, and a motif that is one of this family's own anti-motifs -- the latter is a
    rejection, not an expression, and is hard-rejected independently of the former."""

    def verify_fn(items: Mapping[str, Any], out: Mapping[str, Any]) -> "tuple[dict, dict]":
        hard: "dict[str, str]" = {}
        soft: "dict[str, str]" = {}
        if not isinstance(out, dict):
            hard["_draft"] = "response is not an object"
            return hard, soft
        if out.get(BLOCKED_FIELD):
            return hard, soft

        for key in ("name", "flavor", "atomFamilies", "motifsExpressed", "rationale", BLOCKED_FIELD):
            if key not in out:
                hard[key] = "required key missing"

        if "atomFamilies" in out:
            reasons = atom_families_are_allowed(out, context) + atom_families_not_forbidden(out, context)
            if reasons:
                hard["atomFamilies"] = "; ".join(reasons)

        if "motifsExpressed" in out:
            reasons = (motifs_expressed_are_known(out, context)
                      + motifs_expressed_exclude_anti_motifs(out, context))
            if reasons:
                hard["motifsExpressed"] = "; ".join(reasons)

        return hard, soft

    return verify_fn


def default_for_none(key: str, original: object) -> None:
    """F9's own required override, restated for this pipeline (acceptance #11): NEVER
    `call_with_self_heal`'s shipped default (`lambda key, original: original`), which for a
    GENERATION stage would hand back a BRIEF-CONTEXT field as though the model had answered it.
    `None` is the only honest fallback -- the same shape `VoteResult.value` already uses for
    `confidence == 'unresolved'`."""
    return None


#: See `general_propose.derive._NULLISH_BLOCKED_TOKENS`'s own docstring for the real-call evidence
#: -- `"none"` was measured directly on THIS module (`brief.family.hypno.001`), `"false"` on the
#: sibling. Kept local, same closed set, same self-containment reason this module's own functions
#: state elsewhere.
_NULLISH_BLOCKED_TOKENS = frozenset({"false", "none", "null", "n/a", "na"})


def _normalize_blocked(out: Mapping[str, Any]) -> dict:
    """**Real-call finding, 2026-09-04 (first real smoke batch)** -- identical fix to the
    general-propose sibling module. See `general_propose.derive._normalize_blocked`'s docstring for
    the full real-call evidence: the hardened description alone did not stop a real local model
    from substituting a null-ish word for the empty string this field asks for; any member of
    `_NULLISH_BLOCKED_TOKENS` is folded back to the empty string here, once, at the model boundary.
    `"true"` is deliberately left alone."""
    if isinstance(out, Mapping) and str(out.get(BLOCKED_FIELD, "")).strip().lower() in _NULLISH_BLOCKED_TOKENS:
        return {**out, BLOCKED_FIELD: ""}
    return dict(out) if isinstance(out, Mapping) else out


def sample_draft(brief: Mapping[str, Any], *, sample_index: int,
                 pairing_table: "Mapping[str, Sequence[str]] | None" = None,
                 config: "LlmCallerConfig | None" = None) -> "tuple[dict, dict]":
    """The ONE function in this module that calls a model -- one of `SAMPLE_COUNT` samples for one
    brief. `max_heal=MAX_HEAL` is passed EXPLICITLY (acceptance #11). Returns `(out, soft)`; `soft`
    carries a `FAILED:<reason>` entry for any key `default_for_none` fell back on."""
    context = build_context(brief, sample_index=sample_index, pairing_table=pairing_table)
    brief_text = build_brief(context)
    out, soft = call_with_self_heal(
        dict(context), SYSTEM_PROMPT, lambda _items: brief_text, build_verify_fn(context),
        config=config or LlmCallerConfig(), max_heal=MAX_HEAL, default_for=default_for_none,
        schema=schema_for_call(context["allowedAtomFamilies"], context["motifsExpressedEnum"]),
    )
    return _normalize_blocked(out), soft


@dataclass(frozen=True)
class Candidate:
    brief_id: str
    outcome: str                          # "accepted" | "blocked" | "unresolved"
    entry: "dict[str, Any] | None"
    vote: "VoteResult | None"
    provenance: "dict[str, Any]"


def finalize_candidate(brief: Mapping[str, Any], drafts: Sequence[Mapping[str, Any]], *,
                       candidate_id: str, provenance: "Mapping[str, Any] | None" = None) -> Candidate:
    """Pure -- zero model calls, so this is what `--dry-run` and the recorded-transcript replay
    test both exercise. Takes exactly `SAMPLE_COUNT` already-produced draft dicts (live-called-
    and-healed, or a recorded transcript replayed verbatim) and resolves ONE candidate:

    1. A declared block on sample 0 short-circuits straight to `outcome='blocked'`. `name`/
       `flavor`/`rationale`/`motifsExpressed` are never voted (spec SS2) -- sample 0 is therefore
       this candidate's own single, deterministic source for all four throughout, stated once
       here rather than re-decided per field.
    2. Otherwise, majority vote over the three samples' own canonical `atomFamilies` key
       (`resolve_vote`, reused, never reimplemented). A 1-1-1 split -- or a vote whose WINNING key
       is the "sample never produced a usable pick" sentinel -- writes `outcome='unresolved'`,
       `entry=None`, `vote.value is None` where applicable: **never sample 0's raw pick**
       (binding constraint 4, asserted explicitly by this module's own tests).
    3. On a resolved vote, the final `atomFamilies` is the VOTE's own resolved set, split back
       into a sorted list -- never any one sample's raw draft value. `motifsExpressed` is NEVER
       touched by the vote -- it stays exactly sample 0's own value, whatever the other two
       samples wrote.
    """
    if len(drafts) != SAMPLE_COUNT:
        raise ValueError(f"finalize_candidate needs exactly {SAMPLE_COUNT} drafts, got {len(drafts)}")

    brief_id = brief.get("briefId") or brief.get("id")
    prov = dict(provenance or {})

    primary = drafts[0]
    if isinstance(primary, Mapping) and primary.get(BLOCKED_FIELD):
        return Candidate(brief_id=brief_id, outcome="blocked", entry=None, vote=None, provenance=prov)

    keys: "list[str]" = []
    for draft in drafts:
        families = draft.get("atomFamilies") if isinstance(draft, Mapping) else None
        keys.append(canonical_family_key(families) if isinstance(families, list) and families
                   else _UNRESOLVED_SENTINEL)

    vote = resolve_vote(keys)
    if vote.confidence == "unresolved" or vote.value == _UNRESOLVED_SENTINEL:
        return Candidate(brief_id=brief_id, outcome="unresolved", entry=None, vote=vote, provenance=prov)

    atom_families = sorted(vote.value.split("|")) if vote.value else []
    entry = entry_for({**primary, "atomFamilies": atom_families}, candidate_id=candidate_id,
                      brief_id=brief_id, provenance=prov)
    return Candidate(brief_id=brief_id, outcome="accepted", entry=entry, vote=vote, provenance=prov)


def propose_family_action(brief: Mapping[str, Any], *, candidate_id: str,
                          pairing_table: "Mapping[str, Sequence[str]] | None" = None,
                          config: "LlmCallerConfig | None" = None,
                          provenance: "Mapping[str, Any] | None" = None) -> Candidate:
    """The live, one-brief-in-one-candidate-out entry point (spec SS3: "Never carry state between
    calls"). Calls a real model up to `SAMPLE_COUNT * (MAX_HEAL + 1)` times. Never exercised
    directly by this module's own test suite (binding constraint 8) -- every test instead drives
    `sample_draft`'s pure ingredients (`build_context`/`build_brief`) and `finalize_candidate`
    (which makes zero calls) separately."""
    drafts: "list[dict]" = []
    heal_notes: "list[dict]" = []
    for sample_index in range(SAMPLE_COUNT):
        draft, soft = sample_draft(brief, sample_index=sample_index, pairing_table=pairing_table,
                                   config=config)
        drafts.append(draft)
        heal_notes.append(soft)

    prov = dict(provenance or {})
    prov["healNotes"] = heal_notes
    return finalize_candidate(brief, drafts, candidate_id=candidate_id, provenance=prov)


def candidate_row(candidate: Candidate, *, pipeline_id: str = "A-P2", scope: str = "family") -> "dict[str, Any]":
    """The row shape this module WRITES to disk -- deliberately close to
    `general_propose.derive.candidate_row`'s own identical shape
    (`candidateId`/`briefId`/`pipelineId`/`scope`/`draft`), so a future wiring of A-S4 onto this
    module's real output is a straight read, not a reshape. `confidence`/`voteMinority` are read
    off `VoteResult`, never invented (binding constraint 4) -- absent (`None`) exactly when no
    vote was resolved (a `blocked` candidate makes none)."""
    return {
        "candidateId": candidate.entry["candidateId"] if candidate.entry else None,
        "briefId": candidate.brief_id,
        "pipelineId": pipeline_id,
        "scope": scope,
        "outcome": candidate.outcome,
        "confidence": candidate.vote.confidence if candidate.vote else None,
        "voteMinority": candidate.vote.minority if candidate.vote else None,
        "draft": candidate.entry,
        "_provenance": candidate.provenance,
    }


def canonical_dump(doc: dict) -> str:
    return json.dumps(doc, ensure_ascii=False, indent=2, sort_keys=True, default=str) + "\n"


def candidate_set_hash(rows: Sequence[Mapping[str, Any]]) -> str:
    """A stable digest over a whole round's own candidate rows, ordered by `candidateId` /
    `briefId` (never dict/filesystem iteration order) -- mirrors
    `general_propose.derive.candidate_set_hash`'s identical convention, kept local for the same
    self-containment reason `canonical_family_key` states above."""
    ordered = sorted(rows, key=lambda r: (r.get("candidateId") or "", r.get("briefId") or ""))
    blob = json.dumps(ordered, ensure_ascii=False, sort_keys=True, default=str).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()
