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

import itertools
import json
import os
import re
import tempfile
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any, Callable, Collection, Mapping, Sequence

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

    **Ordering (§6.2's sibling-passing shape, closed 2026-09-06):** subjects are stably sorted
    `(tier, 0 if mechanism else 1)` — every mechanism node in a tier generates before that tier's
    magnitude nodes, never reordering ACROSS tiers or within the same `(tier, class)` group (Python's
    `sorted` is stable, so ties keep the plan's own file order). This exists so
    `run_language_stage`'s own tier-sibling tracking (below) has something real to pass a magnitude
    node the first time it renders — a magnitude node is defined as "makes an EXISTING thing larger"
    (`brief.py`'s own class note), and a tree plan is free to list its magnitude nodes before any
    mechanism node in the same tier (confirmed: `might`'s own committed plan does, for its first six
    nodes) — without this reorder, a magnitude node would ALWAYS render with an empty sibling list
    regardless of how well `run_language_stage` tracks acceptance, which is the exact "current tree is
    empty" gap the 2026-09-06 smoke test surfaced. This does not change what the FINAL seed document's
    own node order is — `run_language_stage`'s own `sorted_records = sorted(..., key=... node_id)`
    already re-sorts by `node_id` at emit time, so generation order is a pure scheduling detail.
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
    subjects.sort(key=lambda s: (s.tier, 0 if s.node_class == "mechanism" else 1))
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


#: 2026-09-06 real-call finding (`might`, tier-4 mechanism node, LM Studio local model): asked to
#: leave `blocked` as the empty string, the model instead wrote a full, valid draft (real `affixIds`,
#: `name`, `flavor`, all schema-legal) with `"blocked": "none"` — read as "I understand this means
#: not-blocked" filtered through a field NAME that shapes like a boolean/nullable rather than the
#: literal empty-string convention `schema.py`'s own description states. The EXACT same finding
#: `general_propose.derive._NULLISH_BLOCKED_TOKENS`'s own docstring already measured and named
#: (2026-09-04, a different real local model, "false"/"none") — this module simply never adopted that
#: fix. Reused verbatim rather than re-derived (`family_propose`/`signature_propose` already keep
#: their own identical copy too, cross-referencing the same evidence).
_NULLISH_BLOCKED_TOKENS = frozenset({"false", "none", "null", "n/a", "na"})


def _normalize_blocked(out: "Mapping[str, Any]") -> dict:
    """See `general_propose.derive._normalize_blocked`'s own docstring for the full real-call
    evidence and reasoning. Any member of `_NULLISH_BLOCKED_TOKENS` (any case/whitespace) is folded
    back to the empty string here, once, right where the raw draft leaves the model boundary — never
    inside `_node_verify_fn`, which stays a pure hard/soft classifier. `"true"` is deliberately NOT
    normalized: no real-call evidence for that direction exists here either, and silently
    reinterpreting it risks masking an actual decline this module has no way to tell apart from the
    same confusion."""
    if isinstance(out, Mapping) and str(out.get(BLOCKED_FIELD, "")).strip().lower() in _NULLISH_BLOCKED_TOKENS:
        return {**out, BLOCKED_FIELD: ""}
    return dict(out) if isinstance(out, Mapping) else out


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
    out, soft = call_with_self_heal(
        {"nodeId": node_id, "sampleIndex": sample_index}, brief_mod.SYSTEM_PROMPT,
        lambda _items: brief_text, _node_verify_fn(gate),
        config=config, schema=schema, default_for=lambda _key, _original: None,
        build_heal_user=lambda items, out, hard: _node_heal_user(brief_text, out, hard),
    )
    return _normalize_blocked(out), soft


def _exhausted(soft: "Mapping[str, str]") -> bool:
    return any(isinstance(v, str) and v.startswith("FAILED:") for v in soft.values())


_NAME_KEY_SLUG_RE = re.compile(r"[^a-z0-9]+")


def _slugify(name: str) -> str:
    """`name` -> the `<slug>` half of `tree.node.<slug>`, matching `NAME_KEY_PATTERN`
    (`^tree\\.node\\.[a-z0-9-]+$`): lowercase, every run of non-alphanumeric characters becomes one
    hyphen, no leading/trailing hyphen. Never returns an empty string — `"node"` if `name` collapses
    to nothing (e.g. it was pure punctuation), since `NAME_KEY_PATTERN` requires at least one
    character after `tree.node.`."""
    slug = _NAME_KEY_SLUG_RE.sub("-", name.strip().lower()).strip("-")
    return slug or "node"


def _derive_unique_name_key(name: str, known_name_keys: "Collection[str]") -> str:
    """2026-09-06 real-call finding (`might`, three separate real collisions across two real runs):
    asked to derive `nameKey` from its own `name` choice (`schema.py`'s own field description, itself
    a fix for this same finding), the real local model kept it as a bare FORMAT instruction and
    repeatedly fell back to a generic templated key (`tree.node.<branch>-<depth>-01`) completely
    decoupled from the name it had just chosen — proven not a one-off: the identical fallback key
    recurred on a FRESH run even after the wording fix landed. No amount of further prompt wording
    can be verified to fix a demonstrated real-model failure to follow an instruction twice in a row;
    the durable fix is to stop trusting the model for this field's UNIQUENESS at all. The model's own
    `nameKey` is still requested and still gates 7/13-validated for FORMAT (a real defect in some
    other field could still make the whole response fail those gates) — only the final persisted
    value is overridden here, deterministically, from the model's own accepted `name` (a slug of
    `name`, exactly what the schema now already asks the model to do by hand), with a numeric suffix
    appended only if that slug collides with an already-known key — collision-free by construction,
    never by hoping the model gets it right."""
    base_key = f"tree.node.{_slugify(name)}"
    if base_key not in known_name_keys:
        return base_key
    suffix = 2
    while f"{base_key}-{suffix}" in known_name_keys:
        suffix += 1
    return f"{base_key}-{suffix}"


def generate_node(subject: Subject, inputs: NodeGenerationInputs, *,
                  config: LlmCallerConfig = DEFAULT_CONFIG,
                  known_name_keys: "Collection[str]" = ()) -> NodeOutcome:
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

    # See `_derive_unique_name_key`'s own docstring: the model's own `nameKey` already passed gates
    # 7/13's FORMAT validation above (so a real defect elsewhere in the response is still caught) --
    # only the persisted VALUE is replaced here, deterministically derived from the model's own
    # accepted `name`, collision-free by construction against every already-known key.
    final_response["nameKey"] = _derive_unique_name_key(str(final_response["name"]), known_name_keys)

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
                       unresolved_max_share_permille: "int | None" = None,
                       max_workers: int = 1) -> LanguageStageResult:
    """One tree, start to finish, idempotent. §7 gate 14: a subject already in the ledger is never
    regenerated — its ALREADY-ACCEPTED record is read back from the ledger and reused, so a forced
    rerun over unchanged inputs makes zero model calls and re-emits byte-identical bytes (the
    historical defect this whole task exists to catch: "the commander-effect generator rewrote all
    84 entries every run").

    **Tree-wide sibling tracking (§6.2, closed 2026-09-06, widened same day).** This function, not
    `inputs_for`, owns "already-accepted siblings" — populating it needs `records`/`done`, data only
    this loop holds. Whatever `siblings` the caller's own `inputs_for(subject)` sets is REPLACED with
    the real, freshly-tracked tuple (via `dataclasses.replace`); a caller is never expected to track
    this itself. The pool is seeded from resumed (`plan.already_done`) records too, so a resumed run's
    first newly-generated node still sees whatever an EARLIER run already accepted, not just what this
    call accepts. **Tree-wide, not tier-scoped** — widened from the original per-tier design the same
    day a real run proved tier-scoping insufficient for its own stated "do not repeat" purpose (a
    real, independent three-way name collision across two different tiers) — capped to the most
    recent `_TREE_SIBLING_CAP` (12) accepted nodes, matching §6.2's own "k nearest siblings" language
    rather than passing an ever-growing, unbounded list as the tree fills in. Combined with
    `plan_run`'s own mechanism-before-magnitude ordering, a magnitude node normally has at least one
    real sibling to read by the time it renders, rather than always seeing brief.py's own "(none yet)"
    placeholder.

    **`max_workers` (owner request, 2026-09-06): batched parallelism, not blanket parallelism.**
    `plan.subjects` is already grouped into contiguous `(tier, node_class)` runs by `plan_run`'s own
    ordering — every subject in ONE such run shares the identical siblings snapshot (none of them can
    be a sibling of another in the SAME run; siblings only ever come from an EARLIER run), so they have
    no dependency on each other and are safe to generate concurrently. Runs themselves stay STRICTLY
    SEQUENTIAL — a later run must see everything an earlier one accepted, which is the whole point of
    §6.2's sibling pass. `max_workers=1` (the default) takes the single-subject-at-a-time path
    unconditionally, byte-for-byte the same code as before this parameter existed — every existing
    caller and test keeps its exact prior behavior with zero change. `max_workers>1` fans out within a
    run via a bounded `ThreadPoolExecutor`, mirroring `workflow.runner.MAX_WORKERS=4`'s own stated
    rationale ("one local model serves one request at a time; a small pool keeps it fed without
    stampeding it") — `workflow.runner.run_many` itself cannot be reused here, since it is built around
    a LangGraph `app.invoke()` interface this module never adopted. Outcomes are always reassembled in
    `plan.subjects`' own original order before being returned, regardless of which worker in a run
    finished first, so `result.outcomes` is deterministic either way.
    """
    from . import verdict  # local import — avoids a module-level cycle with `verdict.py`'s own
                            # re-export of A2's `targets` module, which nothing here otherwise needs

    done = read_ledger(ledger_path)
    plan = plan_run(tree_plan, ledger=done)

    records: "dict[str, NodeSeedRecord]" = {}
    # 2026-09-06 real-call finding: TIER-scoped siblings (the original §6.2 shape) proved insufficient
    # for the "do not repeat" purpose the spec's own text names -- a real run independently generated
    # "Deep Rooting" for three DIFFERENT defensive-branch nodes across TWO different tiers (t2-n0,
    # t2-n1, t3-n1), which tier-scoping cannot see across. Widened to a tree-wide, most-recent-N list
    # (capped, not unbounded, matching §6.2's own "k nearest siblings" language) -- this is sound now
    # that the magnitude class-note fix (brief.py, same date) already decoupled "what a magnitude node
    # amplifies" from siblings entirely ("an EXISTING game stat... never a node this tree has or has
    # not generated"), so widening scope here only affects the dedup purpose, never the amplification
    # one, and cannot reintroduce the tier-1-3-have-no-mechanism-sibling problem the original ordering
    # fix solved.
    _TREE_SIBLING_CAP = 12
    tree_siblings: "list[brief_mod.SiblingSummary]" = []
    # 2026-09-06 real-call finding: even with an explicit "derive nameKey from your own name" schema
    # description (the fix for the SAME finding, wording alone did not hold up against a second real
    # run), the model kept falling back to an identical generic templated key. `known_name_keys`
    # tracks every nameKey already spoken for so `generate_node` can override the model's own choice
    # deterministically -- see `_derive_unique_name_key`'s own docstring for the full reasoning.
    known_name_keys: "set[str]" = set()
    for subject_id in plan.already_done:
        entry = done[subject_id]
        node = entry["record"]
        record = build_node_record(
            node["id"], node["nodeKey"], node["branch"], node["tier"], node["nodeClass"], {
                "affixIds": node["affixIds"], "affinity": node["affinity"],
                "exclusion": node["exclusion"], "name": node["name"], "nameKey": node["nameKey"],
                "flavor": node["flavor"], "rationale": node.get("rationale", ""),
            })
        records[subject_id] = record
        tree_siblings.append(brief_mod.SiblingSummary(record.node_id, record.name, record.affix_ids))
        known_name_keys.add(record.name_key)

    outcomes_by_subject: "dict[str, NodeOutcome]" = {}
    unresolved_count = 0

    def _generate_one(subject: Subject, siblings_here: "tuple[brief_mod.SiblingSummary, ...]",
                      known_keys_here: "frozenset[str]") -> NodeOutcome:
        base_inputs = inputs_for(subject)
        return generate_node(subject, replace(base_inputs, siblings=siblings_here), config=config,
                             known_name_keys=known_keys_here)

    def _record_outcome(subject: Subject, outcome: NodeOutcome) -> None:
        nonlocal done, unresolved_count
        outcomes_by_subject[subject.subject_id] = outcome
        if outcome.outcome == "unresolved":
            unresolved_count += 1
        if outcome.outcome == "accepted" and outcome.record is not None:
            tree_siblings.append(brief_mod.SiblingSummary(
                outcome.record.node_id, outcome.record.name, outcome.record.affix_ids))
            known_name_keys.add(outcome.record.name_key)
            done = record_accepted(done, subject.subject_id, outcome.record)
            records[subject.subject_id] = outcome.record

    for _key, group_iter in itertools.groupby(plan.subjects, key=lambda s: (s.tier, s.node_class)):
        run = list(group_iter)

        if max_workers <= 1 or len(run) <= 1:
            # Sequential: nothing stops a subject from seeing what the PREVIOUS subject in this SAME
            # batch just produced -- there is no real concurrency here, so the snapshot is retaken
            # fresh before every single subject (real-call finding, 2026-09-06: an earlier version of
            # this fix took the snapshot once per BATCH even on the sequential path, which meant two
            # same-batch subjects with max_workers=1 could still both derive the identical nameKey
            # slug and collide -- caught by testing the fix against `might`'s own real 4-per-tier
            # magnitude batches, not assumed correct).
            for subject in run:
                siblings_here = tuple(tree_siblings[-_TREE_SIBLING_CAP:])
                known_keys_here = frozenset(known_name_keys)
                _record_outcome(subject, _generate_one(subject, siblings_here, known_keys_here))
            continue

        # Parallel: every subject in this run shares ONE siblings/known-keys snapshot, taken before
        # any of them starts -- none can see (or collide-check against) another accepted in the SAME
        # run, only what an EARLIER run already contributed. The most recent _TREE_SIBLING_CAP
        # accepted nodes, tree-wide, never just this subject's own tier. Two subjects in the SAME
        # parallel batch independently choosing the identical name is a real, narrower remaining edge
        # case `assert_no_duplicate_name_keys` would still catch at emit time, just not pre-empt --
        # the same trade-off already accepted for siblings under real concurrency.
        siblings_here = tuple(tree_siblings[-_TREE_SIBLING_CAP:])
        known_keys_here = frozenset(known_name_keys)
        with ThreadPoolExecutor(max_workers=max_workers) as pool:
            future_by_subject = {
                pool.submit(_generate_one, subject, siblings_here, known_keys_here): subject for subject in run
            }
            results: "dict[str, NodeOutcome]" = {}
            for future in future_by_subject:
                subject = future_by_subject[future]
                results[subject.subject_id] = future.result()
        # Applied in the run's own original order, never completion order, so tier_siblings grows
        # deterministically regardless of which worker finished first.
        for subject in run:
            _record_outcome(subject, results[subject.subject_id])

    outcomes = [outcomes_by_subject[s.subject_id] for s in plan.subjects]

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
