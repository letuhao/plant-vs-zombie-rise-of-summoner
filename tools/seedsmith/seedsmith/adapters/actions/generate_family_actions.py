"""seedsmith.adapters.actions.generate_family_actions --- A-P2 "family-propose"'s own entrypoint
(spec-family-propose.md, action-corpus program). Mirrors `generate_general_actions.py`'s own
`--dry-run`/`--count` shape (itself modelled on `adapters/effects/affix/generate_affixes.py`),
adapted for a pipeline that reads only `scope: "family"` briefs from A-S1's planned round and
votes three permuted samples per brief over `atomFamilies` alone.

    python -m seedsmith.adapters.actions.generate_family_actions --dry-run          # briefs only
    python -m seedsmith.adapters.actions.generate_family_actions --count 5          # real calls

**Where candidates land, following `generate_general_actions.py`'s own precedent exactly** (spec
SS3 forbids writing an ACCEPTED `kind: "action-seed"` entry at the corpus root -- "Acceptance is
A-S3's; persistence is A-S6's" -- never the whole `_`-prefixed scratch subtree A-S1/A-S4 already
share): `data/seed/actions/_candidates/family/round-<n>.json`.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

from ...pipeline.llm_caller import LlmCallerConfig
from .distribution_planner.derive import load_pairing_table
from .family_propose.derive import candidate_row, candidate_set_hash, canonical_dump, propose_family_action
from .family_propose.prompts import build_brief, build_context
from .vocab import load_family_glossary

__all__ = ["run", "regenerate", "load_family_briefs", "ACTIONS_ROOT", "BRIEFS_PATH"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
BRIEFS_PATH = ACTIONS_ROOT / "_briefs" / "round-1.json"
PAIRINGS_PATH = ACTIONS_ROOT / "pairings.json"
CANDIDATES_DIR = ACTIONS_ROOT / "_candidates" / "family"

PROMPT_VERSION = "family-propose/1"


def _brief_hash(brief: dict) -> str:
    """Acceptance #10: "`_provenance` records ... brief hash" -- per CANDIDATE, over the exact
    brief it answers, so a rerun over one unchanged brief is verifiably unchanged even when a
    sibling brief in the same round moved. Never `briefsCorpusHash` (a round-wide digest A-S1
    already stamps in `_meta`) -- that is carried alongside it for the round-level check, not in
    its place. Identical to `generate_general_actions._brief_hash`, kept local for the same
    self-containment reason the sibling module's own functions state."""
    blob = json.dumps(brief, ensure_ascii=False, sort_keys=True, default=str).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def _family_briefs_of(doc: dict) -> "list[dict]":
    """Every `scope: "family"` entry of A-S1's round envelope, sorted by `briefId` -- never
    dict/filesystem iteration order."""
    family = [e for e in doc.get("entries", []) if e.get("scope") == "family"]
    family.sort(key=lambda b: b.get("briefId") or "")
    return family


def load_family_briefs(briefs_path: Path = BRIEFS_PATH) -> "list[dict]":
    return _family_briefs_of(json.loads(briefs_path.read_text(encoding="utf-8")))


def regenerate(*, briefs_path: Path = BRIEFS_PATH, pairings_path: Path = PAIRINGS_PATH,
              candidates_dir: Path = CANDIDATES_DIR, count: int = 1, dry_run: bool = True,
              round_no: int = 1, endpoint: str = "http://localhost:1234/v1/chat/completions",
              model: str = "google/gemma-4-26b-a4b-qat", write: bool = True) -> dict:
    """Pure-ish computation (`dry_run=True` makes zero model calls and writes nothing regardless
    of `write`) plus, on a real run, up to `count * 3 * (MAX_HEAL + 1)` model calls and one file
    write. Returns a summary dict; never prints itself, matching
    `generate_distribution_planner.regenerate`'s own "a test can call this without capturing
    stdout" convention."""
    briefs_doc = json.loads(briefs_path.read_text(encoding="utf-8"))
    family_briefs = _family_briefs_of(briefs_doc)
    selected = family_briefs[:max(0, count)]

    pairing_table = load_pairing_table(pairings_path) if pairings_path.is_file() else {}
    #: SMOKE BATCH criterion-2 fix, 2026-09-05: read fresh every call, never cached -- see
    #: `vocab.load_family_glossary`'s own docstring.
    family_glossary = load_family_glossary()

    if dry_run:
        sample_brief_text = ""
        if selected:
            context = build_context(selected[0], sample_index=0, pairing_table=pairing_table,
                                    family_glossary=family_glossary)
            sample_brief_text = build_brief(context)
        return {
            "dryRun": True,
            "totalFamilyBriefs": len(family_briefs),
            "selected": len(selected),
            "modelCalls": 0,
            "sampleBrief": sample_brief_text,
        }

    config = LlmCallerConfig(endpoint=endpoint, model=model)
    base_provenance = {
        "pipeline": "family-propose", "model": model, "promptVersion": PROMPT_VERSION,
        "briefsCorpusHash": briefs_doc.get("_meta", {}).get("corpusHash"),
        "generatedUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
    }

    rows: "list[dict]" = []
    by_outcome: "dict[str, int]" = {}
    for i, brief in enumerate(selected):
        candidate_id = f"candidate.family.{i:03d}"
        prov = dict(base_provenance)
        prov["briefHash"] = _brief_hash(brief)
        candidate = propose_family_action(
            brief, candidate_id=candidate_id, pairing_table=pairing_table,
            family_glossary=family_glossary, config=config, provenance=prov,
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
            "partition": "family", "round": round_no, "promptVersion": PROMPT_VERSION,
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
        "totalFamilyBriefs": len(family_briefs),
        "selected": len(selected),
        "byOutcome": by_outcome,
        "candidateSetHash": set_hash,
        "written": bool(write),
    }


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Propose family-scope actions from A-S1's planned briefs (A-P2 'family-propose').")
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
