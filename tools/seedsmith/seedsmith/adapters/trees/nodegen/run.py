"""seedsmith.adapters.trees.nodegen.run — `plan_run` over one tree's committed plan ->
`RunPlan{subjects, held, already_done}` (task H1, spec-tree-language.md §6.1, §6.4); task H2 adds
the real multi-gate RUNNER on top: `generate_node` (one node, base call + the `affixIds` vote) and
`run_language_stage` (a whole tree, idempotent, byte-identical on a forced rerun).

⚠ **What H1 built vs what H2 adds.** H1 assembles the unit-of-work list *deterministically* — one
`Subject` per node, the only unit at which the permitted subset is exact (§6.1's own table) — and
the idempotence ledger's read/write halves. **H2 is the part that actually calls a model**: gates
6-14 (preflight, contract, brief conformance, text style, vote resolution, bounded repair,
persist-time re-gate, idempotence) plus gates 2, 23, 24 (description audit, run verdict, offline
guarantee), wired around H1's own `Subject`/`RunPlan`/ledger rather than replacing them.

**Nothing here reimplements a gate the repo already ships generically** (the todo's own instruction
— reuse, never invent a parallel shape): gate 7 is `actions.validate_heal.gates.run_g1` (schema-
generic, no `atomFamilies`-specific code in it); gate 6 is
`actions.validate_heal.preflight.run_preflight` verbatim; gate 10 is
`workflow.validators.field_echo`/`language` verbatim; gate 11 is
`actions.validate_heal.derive.resolve_set_vote_field`/`verify_permutation` verbatim (`affixIds` is
set-valued, so the SET vote — per-member majority — is the correct aggregation, not the scalar one);
gate 12 is `pipeline.llm_caller.call_with_self_heal` verbatim. Only gate 9's tree-specific half (an
affix outside the permitted set is already a gate-7 enum violation; what gate 7 cannot see is an
affix whose OWN tags collide with this node's anti-motifs) and gate 13 (the persist-time re-gate
over the base response with the VOTED `affixIds` substituted in — a combination gate 7/9 never
checked as a whole, because the vote can pick a set none of the three individual calls returned)
are new code.

**The offline transport stub, precisely.** This module makes no attempt to inject a transport
parameter through `call_with_self_heal` — that function already calls the real
`seedsmith.pipeline.llm_caller.call_model` internally, and the established pattern this whole repo
already uses four times over (`test_classify_pipelines.py`, `test_family_propose.py`,
`test_general_propose.py`, `test_signature_propose.py`) is `unittest.mock.patch
("seedsmith.pipeline.llm_caller.call_model", raising_call)`, where `raising_call(*a, **kw): raise
AssertionError(...)`. §7 gate 24 reuses that exact pattern rather than inventing a second one; the
trees test suite's own copy lives in `_nodegen_fixtures.py` so every test file here shares it.
"""
from __future__ import annotations

import json
import os
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from ....pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig, call_with_self_heal
from ....pipeline.model import BLOCKED_FIELD
from ...actions.validate_heal.derive import (VoteSample, canonical_set_key, resolve_set_vote_field,
                                             verify_permutation)
from ...actions.validate_heal.gates import run_g1
from ...actions.validate_heal.preflight import PreflightResult
from ...actions.validate_heal.preflight import run_preflight as _shared_run_preflight
from ...actions.validate_heal.schema_audit import audit_descriptions
from ...demons.anchor.permute import order_for
from ....workflow.validators.field_echo import field_echo, subject_name_echo
from ....workflow.validators.language import language_consistency
from . import brief as brief_mod
from . import plan_read
from .emit import NodeSeedRecord, build_node_record, build_seed_document, write_seed_document
from .exclusion import ExclusionClaim, validate_exclusion
from .schema import NODE_RESPONSE_SCHEMA, schema_for_call
from .vocab import AffixOption, AffixVocabulary, AffixVocabularyError

REPO_ROOT = Path(__file__).resolve().parents[6]
DEFAULT_LEDGER = REPO_ROOT / "data" / "seed" / "passive-tree" / "_runs" / "tree-language.ledger.json"

#: The three §6.1 vote samples: sample 0 IS the base call (its `affixIds` is both the node's other
#: seven fields' only source AND vote sample 0), samples 1-2 are the two extra "vote calls" the
#: 4,680 arithmetic counts (§6.1: "vote calls 1,560 x 1 voted field x (3 - 1) = 3,120").
VOTE_SAMPLE_COUNT = 3


def calls_for(total_subjects: int, *, vote_sample_count: int = VOTE_SAMPLE_COUNT) -> "dict[str, int]":
    """§6.1's own cost table (D29's corpus, re-derived from doc 03's 7-tier figures), computed from
    `total_subjects` rather than hardcoded — at the generic corpus's real size (1,560 subjects: 39
    trees x 40 nodes) this returns exactly the spec's own cited 4,680:

        base calls     total_subjects x 1 pipeline                =  1,560
        vote calls     total_subjects x 1 voted field x (3 - 1)   =  3,120
                                                                      -----
                                                                      4,680 calls

    The dry run (`trees generate --dry-run`, §Commands) prints this BEFORE spending a single call —
    that is the whole point of it being a pure function of a count, never of a live run."""
    base_calls = total_subjects
    vote_calls = total_subjects * (vote_sample_count - 1)
    return {"baseCalls": base_calls, "voteCalls": vote_calls, "totalCalls": base_calls + vote_calls}


class UnexpectedTransportCall(RuntimeError):
    """§7 gate 24: raised by `offline_transport_stub` — a callable with the exact
    `(system, user, schema) -> str` shape `preflight.CallModelFn`/`llm_caller.call_model` share —
    when something reaches it. Nothing in this module calls it directly (the module makes its real
    calls through `call_with_self_heal`, which reaches `llm_caller.call_model` on its own); it
    exists so a caller wiring `preflight.run_preflight`'s own `call_model_fn` parameter for a
    dry-run has a loud, named stub to pass instead of `None`, rather than inventing a fresh one
    per call site."""


def offline_transport_stub(system: str, user: str, schema: "Mapping[str, Any] | None" = None) -> str:
    raise UnexpectedTransportCall(
        "the offline transport stub was called — a dry-run or a test path reached a model "
        "transport, which must never happen (spec-tree-language.md §7 gate 24)")


def audit_node_schema_descriptions() -> "list":
    """§7 gate 2, run once before any call: the shipped `NODE_RESPONSE_SCHEMA`'s own properties
    each carry a negative-clause description. `schema_audit.audit_descriptions` is Stage 0's own
    check (`actions.validate_heal.schema_audit`), reused verbatim rather than forked — deliberately
    NOT folded into `pipeline.model.audit_schema` (that module's own docstring: doing so would fire
    on every OTHER seedsmith schema the moment this package imports it)."""
    return audit_descriptions(NODE_RESPONSE_SCHEMA)


def run_preflight(*, skip: bool, transport: "Callable[[str, str, dict], str] | None" = None,
                  endpoint: "str | None" = None, model_id: "str | None" = None) -> PreflightResult:
    """§7 gate 6, delegating verbatim to `actions.validate_heal.preflight.run_preflight` — the
    SAME single-member-enum probe, not a tree-specific copy of it (constrained decoding is a
    property of the SERVER, not of this schema)."""
    return _shared_run_preflight(skip=skip, call_model_fn=transport, endpoint=endpoint,
                                 model_id=model_id)


def brief_conformance_defects(response: "Mapping[str, Any]", *, permitted_affix_ids: "Sequence[str]",
                              anti_motif_tags: "Sequence[str]",
                              affix_vocab: AffixVocabulary) -> "dict[str, str]":
    """§7 gate 9's tree-specific half. An affix outside the permitted set is ALREADY a gate-7 enum
    violation (`run_g1` walks `affixIds.items.enum`) — what gate 7 cannot see is an affix whose own
    `tags` (`vocab.AffixOption.tags`) collide with this node's anti-motifs, since that is a property
    of the VOCABULARY, not of the schema's enum shape."""
    defects: "dict[str, str]" = {}
    affix_ids = response.get("affixIds")
    if not isinstance(affix_ids, list) or not anti_motif_tags:
        return defects
    hit_tags: "set[str]" = set()
    for affix_id in affix_ids:
        if affix_id not in permitted_affix_ids:
            continue  # already named by gate 7; never double-reported under a different key
        try:
            option = affix_vocab.get(affix_id)
        except AffixVocabularyError:
            continue
        hit_tags.update(option.tags)
    collision = hit_tags & set(anti_motif_tags)
    if collision:
        defects["affixIds"] = (
            f"picks an affix tagged {sorted(collision)} — this node's own anti-motifs — even "
            f"though no single affixId is itself forbidden")
    return defects


def build_response_gate(*, schema: "Mapping[str, Any]", permitted_affix_ids: "Sequence[str]",
                        permitted_property_keys: "Sequence[str]", anti_motif_tags: "Sequence[str]",
                        affix_vocab: AffixVocabulary,
                        property_vocabulary: "Mapping[str, tuple[str, ...]] | Sequence[str]",
                        tree_display_name: str,
                        motifs: "Sequence[str]") -> "Callable[[Mapping[str, Any]], list[str]]":
    """The one `gate` callable §6.3's response is checked against, composed from gates 7, 9, 10 and
    the per-response half of 18 (exclusion) — never five separate call sites, so a caller
    (`generate_node`, or a future H4 harness) has exactly one function to run twice (verify-time,
    then persist-time re-gate, gate 13)."""

    def gate(response: "Mapping[str, Any]") -> "list[str]":
        problems: "list[str]" = []
        contract_defects = dict(run_g1(response, schema))
        # ⚠ `run_g1` carries ONE hardcoded, actions-specific assumption: "`blocked` must be a
        # boolean" — literal, unconditional, regardless of the schema it is handed. §6.3's own
        # design is deliberately the OPPOSITE (schema.py's own docstring: "`blocked` is an
        # empty-string sentinel, not a boolean... a boolean let a real local model fill the field
        # with something plausible-but-wrong"), so this one false positive is dropped whenever the
        # value actually matches the SCHEMA's own declared type (a real wrong-type `blocked` —
        # an int, say — still fails `run_g1`'s own generic per-field type check above, unaffected).
        if isinstance(response.get(BLOCKED_FIELD), str):
            contract_defects.pop(BLOCKED_FIELD, None)
        problems.extend(f"{field}: {reason}" for field, reason in contract_defects.items())

        # §6.3's own field description for `affinity`, verbatim: "in the same order as `affixIds`
        # and the same length" — `run_g1` checks each array against its OWN enum, never a
        # cross-field length agreement, so this is the one place that requirement is enforced. It
        # matters most exactly where gate 13 (persist-time re-gate) needs it: the base call's own
        # `affinity` was sized for the base call's own `affixIds`, and the vote (§7 gate 11) may
        # substitute a DIFFERENT-length set in before persisting.
        affix_ids, affinity = response.get("affixIds"), response.get("affinity")
        if isinstance(affix_ids, list) and isinstance(affinity, list) and len(affix_ids) != len(affinity):
            problems.append(
                f"affinity: has {len(affinity)} entries but affixIds has {len(affix_ids)} — §6.3 "
                f"requires the same length, in the same order")

        # Deeper, semantic checks only make sense once the shape itself is sound — a response that
        # already failed gate 7 (e.g. `affixIds` is not even a list) has nothing coherent for gate 9
        # or the exclusion ladder to check.
        if not contract_defects:
            brief_defects = brief_conformance_defects(
                response, permitted_affix_ids=permitted_affix_ids, anti_motif_tags=anti_motif_tags,
                affix_vocab=affix_vocab)
            problems.extend(f"{field}: {reason}" for field, reason in brief_defects.items())

            exclusion = response.get("exclusion")
            if isinstance(exclusion, dict):
                claim = ExclusionClaim.from_response(exclusion)
                problems.extend(str(d) for d in validate_exclusion(claim, property_vocabulary))

        problems.extend(field_echo(response, {}))
        problems.extend(subject_name_echo(response, {"displayName": tree_display_name}))
        problems.extend(language_consistency(response, {"motifs": motifs}))
        return problems

    return gate


@dataclass(frozen=True)
class Subject:
    """One unit of work: one node, in one tree, at one call granularity (§6.1).

    `brief`/`schema` stay `None` until the caller supplies this node's resolved quota cell — H1's
    `plan_run` enumerates WHICH nodes need a call; it does not itself resolve what any one of them
    may say, because that resolution is H3's (`quota.permitted`) composed with `brief.render_brief`
    and `schema.schema_for_call` (H2's graph wiring calls both, per node, per the todo's own
    dependency line `H2: Depends on: A2, H1`).
    """

    subject_id: str
    tree_id: str
    node_id: str
    node_key: str
    branch: str
    tier: int
    node_class: str
    brief: "str | None" = None
    schema: "dict[str, Any] | None" = None

    def to_dict(self) -> dict:
        return {
            "subjectId": self.subject_id, "treeId": self.tree_id, "nodeId": self.node_id,
            "nodeKey": self.node_key, "branch": self.branch, "tier": self.tier,
            "nodeClass": self.node_class,
        }


@dataclass
class RunPlan:
    subjects: "list[Subject]"
    held: "list[tuple[str, str]]"
    already_done: "list[str]"

    @property
    def complete(self) -> bool:
        """A plan with a held subject is NOT complete — same rule `items/setgen/run.py`'s own
        `RunPlan.complete` states, so a held quota cell (an `UnsatisfiableCell`, upstream) can never
        be silently reported as a finished run."""
        return not self.held

    def summary(self) -> dict:
        by_reason: "dict[str, int]" = {}
        for _, reason in self.held:
            by_reason[reason] = by_reason.get(reason, 0) + 1
        return {"toGenerate": len(self.subjects), "alreadyDone": len(self.already_done),
                "held": len(self.held), "heldByReason": by_reason, "complete": self.complete}


def read_ledger(path: "Path | None" = None) -> "dict[str, dict]":
    ledger_path = path or DEFAULT_LEDGER
    if not ledger_path.exists():
        return {}
    doc = json.loads(ledger_path.read_text(encoding="utf-8"))
    return dict(doc.get("done") or {})


def write_ledger(done: "dict[str, dict]", path: "Path | None" = None) -> Path:
    """Atomic replace — same discipline `items/setgen/run.py:write_ledger` already ships, reused
    here rather than re-derived, so a killed process never leaves a half-written ledger at 1,560
    subjects any more than it does at ~1,800."""
    ledger_path = path or DEFAULT_LEDGER
    ledger_path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps({"schemaVersion": 1, "done": done}, ensure_ascii=False, indent=2) + "\n"
    handle, tmp_name = tempfile.mkstemp(dir=str(ledger_path.parent), suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp_name, ledger_path)
    except BaseException:
        Path(tmp_name).unlink(missing_ok=True)
        raise
    return ledger_path


def plan_run(tree_plan: "plan_read.TreePlan", *, ledger: "dict[str, dict] | None" = None) -> RunPlan:
    """Enumerates one `Subject` per node in the plan, skipping any already in the ledger.

    Nothing is `held` at this stage today: a hold this module could name would be a quota cell it
    cannot satisfy, and this module never resolves a quota cell (`quota.py`/H3's job) — so `held` is
    always `[]` here, present in the shape only so H3 has somewhere to put one without changing
    `RunPlan`'s own fields.
    """
    done = ledger if ledger is not None else read_ledger()
    subjects: "list[Subject]" = []
    already: "list[str]" = []
    for node in tree_plan.nodes:
        subject_id = f"{tree_plan.tree_id}:{node.node_id}"
        if subject_id in done:
            already.append(subject_id)
            continue
        subjects.append(Subject(
            subject_id=subject_id, tree_id=tree_plan.tree_id, node_id=node.node_id,
            node_key=node.node_key, branch=node.branch, tier=node.tier,
            node_class=node.node_class,
        ))
    return RunPlan(subjects=subjects, held=[], already_done=already)


def record_accepted(done: "dict[str, dict]", subject_id: str, record: NodeSeedRecord) -> "dict[str, dict]":
    """§7 gate 14's own citation, `ProvenanceLedger.record` (`pipeline/provenance.py:109-118`):
    "raises on a duplicate row." This ledger is a plain JSON `{subjectId: entry}` map rather than
    that class (H1's own shape, already round-tripped by `read_ledger`/`write_ledger`), but the
    SAME discipline applies: two runs both believing they generated the same subject is exactly the
    defect idempotence exists to prevent, so recording over an existing row raises rather than
    silently overwriting it. Returns a NEW dict — the caller's own `done` mapping is never mutated
    in place, so a caller mid-loop still holds the pre-record snapshot if it needs to roll back.
    """
    if subject_id in done:
        raise ValueError(
            f"subject {subject_id!r} already has a ledger row — a second `record_accepted` call "
            f"means idempotence failed (two runs both believed they generated it)")
    return {**done, subject_id: {"record": record.to_dict()}}


# ---------------------------------------------------------------------------------------------
# H2 — the real gate runner. Gates 6, 7, 9, 10, 11, 12, 13 fire per node; gates 2, 23, 24 fire once
# per run (description audit before any call, the run verdict after every subject, the offline
# guarantee everywhere — proven by test, never by a runtime check).
# ---------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class NodeGenerationInputs:
    """Everything one node's generation needs beyond the `Subject` itself. H3's quota stage is what
    RESOLVES a node's own `permitted_affixes`/`permitted_properties`/`anti_motif_tags` from the
    plan's quota cell (`quota.py`, not yet built) — this module takes them as already-resolved
    arguments rather than re-deriving them, the same split `Subject.brief`/`Subject.schema` staying
    `None` in H1 already draws."""

    tree_display_name: str
    tree_reading: str
    motifs: "Sequence[str]"
    anti_motifs: "Sequence[str]"
    anti_motif_tags: "Sequence[str]"
    permitted_affixes: "Sequence[AffixOption]"
    permitted_properties: "Sequence[str]"
    property_vocabulary: "Mapping[str, tuple[str, ...]] | Sequence[str]"
    affix_vocab: AffixVocabulary
    siblings: "Sequence[brief_mod.SiblingSummary]" = ()


@dataclass(frozen=True)
class NodeOutcome:
    """One node's whole generation result. `outcome` is one of `"accepted" | "blocked" |
    "unresolved" | "escalated"` — the same four-way split `pipeline.model.PipelineResult` and
    `actions.validate_heal.derive.CandidateVerdict` both already use, so a caller reading this
    outcome does not have to learn a fifth vocabulary for the same four ideas."""

    subject_id: str
    outcome: str
    record: "NodeSeedRecord | None" = None
    detail: str = ""


def _node_verify_fn(gate: "Callable[[Mapping[str, Any]], list[str]]") -> "Callable":
    """`call_with_self_heal`'s own `verify_fn(items, out) -> (hard, soft)`, closed over one node's
    `gate`. A declared `blocked` response short-circuits (an honest decline is never a defect, the
    same rule every pipeline in this repo already applies) — the caller reads `out[BLOCKED_FIELD]`
    to tell a genuine decline from an accepted draft."""

    def verify_fn(_items: "Mapping[str, Any]", out: "Mapping[str, Any]"):
        if not isinstance(out, dict):
            return {"_draft": "response is not an object"}, {}
        if out.get(BLOCKED_FIELD):
            return {}, {}
        problems = gate(out)
        hard = {f"gate[{i}]": p for i, p in enumerate(problems)}
        return hard, {}

    return verify_fn


def _node_heal_user(brief_text: str, _out: "Mapping[str, Any]", hard: "Mapping[str, str]") -> str:
    """Names the exact defect, then re-sends the ORIGINAL brief — never the model's own prior
    (wrong) draft — mirroring `actions.validate_heal.derive.build_heal_user`'s own shape and its
    own reasoning: a generation stage's "source" is what it was asked to build from."""
    defects = "\n".join(f"- {reason}" for reason in hard.values())
    return (f"Your previous answer had these problems:\n{defects}\n\n"
            f"Fix them. Return the COMPLETE corrected JSON object (every required key).\n\n"
            f"The brief you are answering:\n{brief_text}")


def call_one_node_sample(*, node_id: str, sample_index: int, brief_text: str,
                         schema: "Mapping[str, Any]", gate: "Callable[[Mapping[str, Any]], list[str]]",
                         config: LlmCallerConfig = DEFAULT_CONFIG) -> "tuple[dict, dict]":
    """One §6.2/§6.3 call at `sample_index`. Gates 7 (contract) and 9 (brief conformance) fire
    inside `gate`, checked on every heal attempt AND once more on the final draft — gate 12
    (bounded repair) is `call_with_self_heal` itself, reused rather than rebuilt. `default_for`
    returns `None` on exhaustion (F9's own rule, `actions.validate_heal.derive.default_for_none`):
    handing back the BRIEF's own value would make an escalated field look like the model answered
    it, which this pipeline refuses to do."""
    return call_with_self_heal(
        {"nodeId": node_id, "sampleIndex": sample_index}, brief_mod.SYSTEM_PROMPT,
        lambda _items: brief_text, _node_verify_fn(gate),
        config=config, schema=schema, default_for=lambda _key, _original: None,
        build_heal_user=lambda items, out, hard: _node_heal_user(brief_text, out, hard),
    )


def _exhausted(soft: "Mapping[str, str]") -> bool:
    return any(isinstance(v, str) and v.startswith("FAILED:") for v in soft.values())


def generate_node(subject: Subject, inputs: NodeGenerationInputs, *,
                  config: LlmCallerConfig = DEFAULT_CONFIG) -> NodeOutcome:
    """One node, start to finish: gate 2 (schema description audit, before any call), the base
    call, the two `affixIds` vote calls (§6.1), gate 11's vote resolution, and gate 13's
    persist-time re-gate over the base response with the VOTED `affixIds` substituted in.

    The persist-time re-gate is not a duplicate of the base call's own gate: the base call only
    ever validated its OWN `affixIds` pick, never the composite the vote may have resolved to
    (majority-per-member, `resolve_set_vote_field` — a member two of three samples picked is not
    necessarily the exact set any ONE of them returned), so that composite is checked here for the
    first time.
    """
    permitted_affix_ids = [o.affix_id for o in inputs.permitted_affixes]
    schema = schema_for_call(permitted_affix_ids, list(inputs.permitted_properties))

    schema_defects = audit_descriptions(schema)
    if schema_defects:
        raise ValueError(
            f"{subject.node_id}: response schema fails the description audit (§7 gate 2), before "
            f"any call: {[str(d) for d in schema_defects]}")

    gate = build_response_gate(
        schema=schema, permitted_affix_ids=permitted_affix_ids,
        permitted_property_keys=list(inputs.permitted_properties),
        anti_motif_tags=inputs.anti_motif_tags, affix_vocab=inputs.affix_vocab,
        property_vocabulary=inputs.property_vocabulary,
        tree_display_name=inputs.tree_display_name, motifs=inputs.motifs)

    picks_by_sample: "dict[int, tuple[str, ...]]" = {}
    base_response: "dict[str, Any] | None" = None

    for sample_index in range(VOTE_SAMPLE_COUNT):
        sample_brief = brief_mod.render_brief(
            node_id=subject.node_id, sample_index=sample_index,
            tree_display_name=inputs.tree_display_name, tree_reading=inputs.tree_reading,
            branch=subject.branch, tier=subject.tier, node_class=subject.node_class,
            motifs=inputs.motifs, anti_motifs=inputs.anti_motifs,
            permitted_affixes=inputs.permitted_affixes,
            permitted_properties=inputs.permitted_properties, siblings=inputs.siblings)
        out, soft = call_one_node_sample(
            node_id=subject.node_id, sample_index=sample_index, brief_text=sample_brief,
            schema=schema, gate=gate, config=config)

        if sample_index == 0:
            if out.get(BLOCKED_FIELD):
                # §6.3's own field description: the REASON lives in `blocked` itself ("set this
                # INSTEAD of the content fields ... and say why"), never in `rationale`.
                return NodeOutcome(subject.subject_id, "blocked", detail=str(out[BLOCKED_FIELD]))
            if _exhausted(soft) or not out:
                return NodeOutcome(subject.subject_id, "escalated",
                                   detail=f"base call exhausted the repair budget: {soft}")
            base_response = dict(out)

        # A sample that came back blocked or exhausted contributes an empty pick to the vote —
        # never crashes the whole node over one bad vote-only call (the base call already proved
        # this brief is answerable).
        if out.get(BLOCKED_FIELD) or _exhausted(soft):
            picks_by_sample[sample_index] = ()
        else:
            picks_by_sample[sample_index] = tuple(out.get("affixIds") or ())

    assert base_response is not None  # sample 0 always returns above on blocked/escalated

    samples = [
        VoteSample(
            sample_index=i,
            rendered_order=tuple(order_for(subject.node_id, "affixIds", i, permitted_affix_ids)),
            chosen_value=canonical_set_key(picks_by_sample[i]),
        )
        for i in range(VOTE_SAMPLE_COUNT)
    ]
    try:
        for sample in samples:
            verify_permutation(subject.node_id, "affixIds", permitted_affix_ids, sample)
        vote = resolve_set_vote_field(subject.node_id, "affixIds", permitted_affix_ids, samples)
    except ValueError as ex:
        # §7 gate 11: "never silently the first option" — a claimed permutation that did not
        # reproduce, or a malformed vote call, escalates rather than defaulting to sample 0's pick.
        return NodeOutcome(subject.subject_id, "escalated", detail=str(ex))

    if vote.confidence == "unresolved":
        return NodeOutcome(subject.subject_id, "unresolved",
                           detail=f"{subject.node_id}/affixIds: 1-1-1 vote, no majority")

    final_response = dict(base_response)
    final_response["affixIds"] = list(vote.values)

    persist_defects = gate(final_response)
    if persist_defects:
        return NodeOutcome(subject.subject_id, "escalated",
                           detail=f"failed the gate at persist time (§7 gate 13): {persist_defects}")

    record = build_node_record(subject.node_id, subject.node_key, subject.branch, subject.tier,
                               subject.node_class, final_response)
    return NodeOutcome(subject.subject_id, "accepted", record=record)


@dataclass(frozen=True)
class LanguageStageResult:
    """One tree's whole run: every node's outcome, the seed document path (only written when at
    least one record exists), and the §7 gate 23 `RunReport`."""

    tree_id: str
    outcomes: "tuple[NodeOutcome, ...]"
    seed_path: "Path | None"
    report: "verdict.RunReport"


def run_language_stage(tree_plan: "plan_read.TreePlan",
                       inputs_for: "Callable[[Subject], NodeGenerationInputs]", *,
                       ledger_path: "Path | None" = None, seed_root: "Path | None" = None,
                       config: LlmCallerConfig = DEFAULT_CONFIG,
                       unresolved_max_share_permille: "int | None" = None) -> LanguageStageResult:
    """One tree, start to finish, idempotent. §7 gate 14: a subject already in the ledger is never
    regenerated — its ALREADY-ACCEPTED record is read back from the ledger and reused, so a forced
    rerun over unchanged inputs makes zero model calls and re-emits byte-identical bytes (the
    historical defect this whole task exists to catch: "the commander-effect generator rewrote all
    84 entries every run").
    """
    from . import verdict  # local import — avoids a module-level cycle with `verdict.py`'s own
                            # re-export of A2's `targets` module, which nothing here otherwise needs

    done = read_ledger(ledger_path)
    plan = plan_run(tree_plan, ledger=done)

    records: "dict[str, NodeSeedRecord]" = {}
    for subject_id in plan.already_done:
        entry = done[subject_id]
        node = entry["record"]
        records[subject_id] = build_node_record(
            node["id"], node["nodeKey"], node["branch"], node["tier"], node["nodeClass"], {
                "affixIds": node["affixIds"], "affinity": node["affinity"],
                "exclusion": node["exclusion"], "name": node["name"], "nameKey": node["nameKey"],
                "flavor": node["flavor"], "rationale": node.get("rationale", ""),
            })

    outcomes: "list[NodeOutcome]" = []
    unresolved_count = 0
    for subject in plan.subjects:
        outcome = generate_node(subject, inputs_for(subject), config=config)
        outcomes.append(outcome)
        if outcome.outcome == "unresolved":
            unresolved_count += 1
        if outcome.outcome == "accepted" and outcome.record is not None:
            done = record_accepted(done, subject.subject_id, outcome.record)
            records[subject.subject_id] = outcome.record

    if plan.subjects:
        write_ledger(done, ledger_path)

    seed_path = None
    if records:
        sorted_records = sorted(records.values(), key=lambda r: r.node_id)
        doc = build_seed_document(
            tree_plan.tree_id, sorted_records, plan_hash=tree_plan.raw.get("sha256", ""),
            prompt_version=brief_mod.PROMPT_VERSION, model=config.model)
        seed_path = write_seed_document(doc, seed_root)

    report = verdict.RunReport()
    total = len(plan.subjects) + len(plan.already_done)
    unresolved_share_permille = (unresolved_count * 1000) // total if total else 0
    if unresolved_max_share_permille is not None:
        report.record(
            verdict.UNRESOLVED_COUNT_METRIC, ran=True,
            cleared=unresolved_share_permille <= unresolved_max_share_permille,
            detail=f"{unresolved_count}/{total} unresolved ({unresolved_share_permille}‰)")

    return LanguageStageResult(tree_id=tree_plan.tree_id, outcomes=tuple(outcomes),
                              seed_path=seed_path, report=report)
