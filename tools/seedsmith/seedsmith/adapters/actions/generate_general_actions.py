"""seedsmith.adapters.actions.generate_general_actions --- A-P1 "general-propose"'s own entrypoint
(spec-general-propose.md, action-corpus program). Mirrors
`adapters/effects/affix/generate_affixes.py`'s own `--dry-run`/`--count` shape (SS1's own named
precedent), adapted for a pipeline that reads a PLANNED brief (A-S1's `_briefs/round-<n>.json`)
rather than computing its own eligible pool, and that votes three permuted samples per brief
instead of calling once.

    python -m seedsmith.adapters.actions.generate_general_actions --dry-run          # briefs only
    python -m seedsmith.adapters.actions.generate_general_actions --count 5          # real calls

**Where candidates land, decided here because no prior module decided it.** Spec SS3 forbids
writing into `data/seed/actions/` in the sense that matters -- an ACCEPTED `kind: "action-seed"`
entry at the corpus root ("Acceptance is A-S3's; persistence is A-S6's"). It does not forbid the
whole subtree: A-S1 already writes `_briefs/` and A-S4 already writes `_rounds/` there, both
underscore-prefixed scratch directories a fellow module in this exact domain owns. Neither A-S1
nor A-S4's own entrypoint (`generate_validate_heal.py`'s `--candidates` flag has no committed
default) named where a GENERATION stage's own pre-acceptance candidates should land, so this build
follows the same convention: `data/seed/actions/_candidates/general/round-<n>.json`.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

from ...pipeline.llm_caller import LlmCallerConfig
from .distribution_planner.derive import load_pairing_table
from .general_propose.derive import candidate_row, candidate_set_hash, canonical_dump, propose_general_action
from .general_propose.prompts import build_brief, build_context

__all__ = ["run", "regenerate", "load_general_briefs", "ACTIONS_ROOT", "BRIEFS_PATH"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
BRIEFS_PATH = ACTIONS_ROOT / "_briefs" / "round-1.json"
PAIRINGS_PATH = ACTIONS_ROOT / "pairings.json"
CANDIDATES_DIR = ACTIONS_ROOT / "_candidates" / "general"

PROMPT_VERSION = "general-propose/1"


def _brief_hash(brief: dict) -> str:
    """Acceptance #8: "`_provenance` records ... brief hash" -- per CANDIDATE, over the exact
    brief it answers, so a rerun over one unchanged brief is verifiably unchanged even when a
    sibling brief in the same round moved. Never `briefsCorpusHash` (a round-wide digest A-S1
    already stamps in `_meta`) -- that is carried alongside it for the round-level check, not in
    its place."""
    blob = json.dumps(brief, ensure_ascii=False, sort_keys=True, default=str).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def _general_briefs_of(doc: dict) -> "list[dict]":
    """Every `scope: "general"` entry of A-S1's round envelope, sorted by `briefId` -- never
    dict/filesystem iteration order."""
    general = [e for e in doc.get("entries", []) if e.get("scope") == "general"]
    general.sort(key=lambda b: b.get("briefId") or "")
    return general


def load_general_briefs(briefs_path: Path = BRIEFS_PATH) -> "list[dict]":
    return _general_briefs_of(json.loads(briefs_path.read_text(encoding="utf-8")))


def regenerate(*, briefs_path: Path = BRIEFS_PATH, pairings_path: Path = PAIRINGS_PATH,
              candidates_dir: Path = CANDIDATES_DIR, count: int = 1, dry_run: bool = True,
              round_no: int = 1, endpoint: str = "http://localhost:1234/v1/chat/completions",
              model: str = "google/gemma-4-26b-a4b-qat", write: bool = True) -> dict:
    """Pure-ish computation (`dry_run=True` makes zero model calls and writes nothing regardless
    of `write`) plus, on a real run, up to `count * 3 * (MAX_HEAL + 1)` model calls and one file
    write. Returns a summary dict; never prints itself, matching `generate_distribution_planner
    .regenerate`'s own "a test can call this without capturing stdout" convention."""
    briefs_doc = json.loads(briefs_path.read_text(encoding="utf-8"))
    general_briefs = _general_briefs_of(briefs_doc)
    selected = general_briefs[:max(0, count)]

    pairing_table = load_pairing_table(pairings_path) if pairings_path.is_file() else {}

    if dry_run:
        sample_brief_text = ""
        if selected:
            context = build_context(selected[0], sample_index=0, pairing_table=pairing_table)
            sample_brief_text = build_brief(context)
        return {
            "dryRun": True,
            "totalGeneralBriefs": len(general_briefs),
            "selected": len(selected),
            "modelCalls": 0,
            "sampleBrief": sample_brief_text,
        }

    config = LlmCallerConfig(endpoint=endpoint, model=model)
    base_provenance = {
        "pipeline": "general-propose", "model": model, "promptVersion": PROMPT_VERSION,
        "briefsCorpusHash": briefs_doc.get("_meta", {}).get("corpusHash"),
        "generatedUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
    }

    rows: "list[dict]" = []
    by_outcome: "dict[str, int]" = {}
    for i, brief in enumerate(selected):
        candidate_id = f"candidate.general.{i:03d}"
        prov = dict(base_provenance)
        prov["briefHash"] = _brief_hash(brief)
        candidate = propose_general_action(
            brief, candidate_id=candidate_id, pairing_table=pairing_table, config=config,
            provenance=prov,
        )
        row = candidate_row(candidate)
        rows.append(row)
        by_outcome[candidate.outcome] = by_outcome.get(candidate.outcome, 0) + 1

    rows.sort(key=lambda r: r["briefId"])
    set_hash = candidate_set_hash(rows)

    out_doc = {
        "schemaVersion": 1,
        "kind": "action-candidate",
        "_meta": {
            "partition": "general", "round": round_no, "promptVersion": PROMPT_VERSION,
            "model": model, "candidateSetHash": set_hash,
            "briefsCorpusHash": base_provenance["briefsCorpusHash"],
        },
        "entries": rows,
    }

    if write:
        candidates_dir.mkdir(parents=True, exist_ok=True)
        (candidates_dir / f"round-{round_no}.json").write_text(canonical_dump(out_doc), encoding="utf-8")

    return {
        "dryRun": False,
        "totalGeneralBriefs": len(general_briefs),
        "selected": len(selected),
        "byOutcome": by_outcome,
        "candidateSetHash": set_hash,
        "written": bool(write),
    }


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Propose general-scope actions from A-S1's planned briefs (A-P1 'general-propose').")
    ap.add_argument("--dry-run", action="store_true", help="render briefs, make no model calls")
    ap.add_argument("--count", type=int, default=1, help="how many briefs to draw a candidate for")
    ap.add_argument("--round", type=int, default=1, help="the round number this run writes")
    ap.add_argument("--endpoint", default="http://localhost:1234/v1/chat/completions")
    ap.add_argument("--model", default="google/gemma-4-26b-a4b-qat")
    args = ap.parse_args(argv)

    summary = regenerate(count=args.count, dry_run=args.dry_run, round_no=args.round,
                         endpoint=args.endpoint, model=args.model)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


def main(argv=None) -> int:
    return run(argv)


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
