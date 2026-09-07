"""seedsmith.adapters.items.affixfamgen.run — wires `pipeline.run_ledger.RunLedger` for
Acceptance #4 ("Output through generator-harness's ledger") and the spec's own testing strategy
("Harness tests: resume/reconcile/overwrite").

One ledger row per `FamilyRequest` (`groupId:kindId:word`), matching the `<module-id>.ledger.json`
convention `setgen`'s own `set-charm-gen.ledger.json` and this program's harness spec both use.

**Reconcile, not just resume.** `is_valid` re-checks the REAL, current partition file on disk —
mirroring `run_ledger.RunLedger.plan`'s own doc ("a corpus a hand edit broke out of band gets
reconciled on the next run rather than silently skipped forever"): a ledger row claiming a family
was written is only trusted if that family's minted id still exists in
`data/seed/items/affix-families/g-<group>.json` right now.
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

from seedsmith.pipeline.run_ledger import RunLedger

from . import brief as brief_mod
from . import emit

REPO_ROOT = Path(__file__).resolve().parents[6]
DEFAULT_LEDGER = REPO_ROOT / "data" / "seed" / "items" / "_runs" / "affix-families-gen.ledger.json"


@dataclass(frozen=True)
class FamilyRequest:
    """One unit of work: author one new family, on one partition, of one declared kind."""

    group_id: str
    kind_id: str
    word: str

    @property
    def subject_id(self) -> str:
        return f"{self.group_id}:{self.kind_id}:{self.word}"


@dataclass(frozen=True)
class PlanResult:
    to_generate: "tuple[FamilyRequest, ...]"
    already_done: "tuple[str, ...]"

    @property
    def complete(self) -> bool:
        return not self.to_generate


def _minted_id_for(request: FamilyRequest, *, families_dir: "Path | None") -> "str | None":
    """The id this request would mint, or `None` if its partition has no shipped file to reconcile
    against yet (a brand-new partition — nothing to check the ledger's claim against)."""
    try:
        stem = brief_mod.group_stem(request.group_id)
    except brief_mod.UnknownGroupError:
        return None
    return emit.family_id(stem, request.word)


def plan_requests(requests: "Sequence[FamilyRequest]", ledger: RunLedger, *,
                  families_dir: "Path | None" = None) -> PlanResult:
    """Resume + reconcile over `requests`: a request already marked done in the ledger AND whose
    minted id is still really present in its partition file is skipped; everything else — never
    attempted, or reconciled away because the corpus no longer agrees with the ledger — is
    returned in `to_generate`, in the same order `requests` was given."""
    by_subject = {r.subject_id: r for r in requests}

    def is_valid(subject_id: str, entry: "dict") -> bool:
        request = by_subject.get(subject_id)
        if request is None:
            return True  # not part of THIS plan call; nothing here to reconcile against
        minted_id = entry.get("mintedId")
        if not minted_id:
            return False
        try:
            ctx = brief_mod.load_partition_context(request.group_id, families_dir=families_dir)
        except FileNotFoundError:
            return False
        return minted_id in ctx.existing_ids

    subject_ids = [r.subject_id for r in requests]
    needing = ledger.plan(subject_ids, is_valid=is_valid)
    needing_set = set(needing)
    already_done = tuple(sid for sid in subject_ids if sid not in needing_set)
    return PlanResult(
        to_generate=tuple(by_subject[sid] for sid in needing),
        already_done=already_done,
    )


def mark_family_written(ledger: RunLedger, request: FamilyRequest, minted_id: str) -> None:
    """Records the ledger row `plan_requests`'s own `is_valid` reconciles against. Deliberately a
    thin, explicit wrapper (not a bare `ledger.mark_done` call at the caller site) so the entry
    shape — `mintedId`, `groupId`, `kindId` — is defined in exactly one place."""
    ledger.mark_done(request.subject_id, {
        "mintedId": minted_id, "groupId": request.group_id, "kindId": request.kind_id,
    })


def force_requests(ledger: RunLedger, requests: "Sequence[FamilyRequest]",
                   *, scope: str) -> "list[FamilyRequest]":
    """The real `--overwrite` path: bypasses the ledger's own resume/reconcile state for the named
    requests (or every request, with the literal `scope="all"`), per `RunLedger.force`'s own
    "a typo'd --overwrite fails loudly" contract."""
    by_subject = {r.subject_id: r for r in requests}
    forced_ids = ledger.force([r.subject_id for r in requests], scope=scope)
    return [by_subject[sid] for sid in forced_ids]
