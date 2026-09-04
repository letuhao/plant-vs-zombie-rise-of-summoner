"""seedsmith.adapters.actions.generate_validate_heal --- A-S4's entrypoint (spec-validate-heal.md).
Reads:

    <candidates>     a JSON envelope {"entries": [...]} of candidate rows -- each carrying at
                      least candidateId/briefId/pipelineId/scope/draft, matching
                      `validate_heal.derive.validate_round`'s own row shape. No shipped default:
                      A-P1/A-P2/A-P3 (`propose-general`/`propose-family`/`propose-signature`) are
                      not built yet (spec SS6's own dependency table -- "none exist"), so there is
                      no real per-round candidate file anywhere in this checkout to default at,
                      matching `generate_dedup_select.py`'s own "no shipped default" precedent for
                      exactly the same reason.
    <briefs>          A-S1's own brief envelope for this round (`distribution_planner`'s output) --
                      used to build each candidate's `BriefContext` (allowed/forbidden atom
                      families, motifs, structure-axis ceiling budget).
    data/tuning/action-rungs.v1.json   the rung table g2 reads the ceiling row's own structureBudget
                                        from (via `distribution_planner.derive`, reused, never
                                        re-derived here).

and writes, under `data/seed/actions/_rounds/round-<n>/`:

    accepted.json     kind: "action-seed"   -- candidates whose outcome was "accepted"
    blocked.json      kind: "action-review" -- candidates the model genuinely declined
    unresolved.json   kind: "action-review" -- 1-1-1 votes and heal-exhausted candidates
    escalated.json    kind: "action-review" -- anything this module's own contract has no named
                                                path for (see `derive.py`'s own docstring)

**Zero model calls under `--dry-run`** (acceptance #9): gates a recorded candidate set, `--preflight`
is skipped, `preflight: "skipped"` is written into the round report's own provenance either way
unless `--preflight` was also passed and a real call ran. The live heal path (no `--dry-run`) is
wired here but exercised by NO test in this build (SS4's own "tests never call a model" rule) --
A-P1/A-P2/A-P3 do not exist yet to produce a live brief to heal against.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .distribution_planner.derive import load_rung_table, structure_axes_for
from .validate_heal.derive import build_envelope, canonical_dump, validate_round
from .validate_heal.gates import BriefContext
from .validate_heal.preflight import run_preflight

__all__ = ["run", "regenerate", "ACTIONS_ROOT", "RUNG_TABLE_PATH", "build_contexts"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
RUNG_TABLE_PATH = REPO_ROOT / "data" / "tuning" / "action-rungs.v1.json"


def build_contexts(brief_rows: "list[dict]", *, rung_table_path: Path = RUNG_TABLE_PATH,
                   family_action_atom_sets: "tuple[frozenset, ...]" = (),
                   forbidden_anchor_tokens: "tuple[str, ...]" = ()) -> "dict[str, BriefContext]":
    """One `BriefContext` per brief, keyed by `briefId` -- g2's own ceiling-row budget comes from
    `distribution_planner.derive.structure_axes_for` (A-S1's stated collapse rule), never
    re-derived (spec SS3: "never invent a rung-band resolution")."""
    rung_table = load_rung_table(rung_table_path)
    out: "dict[str, BriefContext]" = {}
    for b in brief_rows:
        scope = b["scope"]
        pipeline_id = {"general": "A-P1", "family": "A-P2", "species": "A-P3"}[scope]
        anchor = b.get("anchor") or {}
        motifs = frozenset(anchor.get("motifs") or anchor.get("familyMotifs") or ())
        anti_motifs = frozenset(anchor.get("antiMotifs") or anchor.get("familyAntiMotifs") or ())
        pool = b.get("pool") or {}
        out[b["briefId"]] = BriefContext(
            brief_id=b["briefId"], pipeline_id=pipeline_id,
            allowed_atom_families=frozenset(pool.get("allowedAtomFamilies") or ()),
            forbidden_atom_families=frozenset(pool.get("forbiddenAtomFamilies") or ()),
            motifs=motifs, anti_motifs=anti_motifs,
            structure_budget_ceiling=structure_axes_for(scope, rung_table),
            family_action_atom_sets=family_action_atom_sets if pipeline_id == "A-P3" else (),
            forbidden_anchor_tokens=forbidden_anchor_tokens if pipeline_id == "A-P1" else (),
        )
    return out


def regenerate(*, candidates_path: Path, briefs_path: Path, round_no: int = 1,
               dry_run: bool = True, do_preflight: bool = False, write: bool = True) -> dict:
    candidates_doc = json.loads(candidates_path.read_text(encoding="utf-8"))
    candidate_rows = candidates_doc.get("entries") or []
    briefs_doc = json.loads(briefs_path.read_text(encoding="utf-8"))
    brief_rows = briefs_doc.get("entries") or []

    contexts = build_contexts(brief_rows)

    if do_preflight and not dry_run:
        from ...pipeline.llm_caller import call_model, load_config
        config = load_config()
        preflight = run_preflight(
            skip=False,
            call_model_fn=lambda system, user, schema: call_model(system, user, config=config, schema=schema),
            endpoint=config.endpoint, model_id=config.model,
        )
        if preflight.blocks_run:
            raise RuntimeError(f"--preflight failed: {preflight.detail} (endpoint={preflight.endpoint})")
    else:
        preflight = run_preflight(skip=True)

    report = validate_round(candidate_rows, contexts=contexts)

    by_outcome: "dict[str, list[dict]]" = {"accepted": [], "blocked": [], "unresolved": [], "escalated": []}
    for v in report.verdicts:
        row = v.entry if v.entry is not None else {"candidateId": v.candidate_id, "briefId": v.brief_id}
        row = dict(row)
        row["candidateId"] = v.candidate_id
        row["_provenance"] = {**v.provenance, "gateDefects": v.gate_defects,
                              "structureAxesUnchecked": v.structure_axes_unchecked}
        by_outcome[v.outcome].append(row)

    base_meta = {
        "partition": "rounds", "round": round_no, "candidateSetHash": report.candidate_set_hash,
        "disagreementRate": report.disagreement_rate,
        "differentiatorNoneRate": report.differentiator_none_rate,
        "restrictionUncheckedCount": report.restriction_unchecked_count,
        "preflight": preflight.status,
    }

    docs = {
        "accepted": build_envelope("action-seed", by_outcome["accepted"], dict(base_meta)),
        "blocked": build_envelope("action-review", by_outcome["blocked"], dict(base_meta)),
        "unresolved": build_envelope("action-review", by_outcome["unresolved"], dict(base_meta)),
        "escalated": build_envelope("action-review", by_outcome["escalated"], dict(base_meta)),
    }

    if write:
        round_dir = ACTIONS_ROOT / "_rounds" / f"round-{round_no}"
        round_dir.mkdir(parents=True, exist_ok=True)
        for name, doc in docs.items():
            (round_dir / f"{name}.json").write_text(canonical_dump(doc), encoding="utf-8")

    return {
        "round": round_no, "candidateCount": len(candidate_rows),
        "acceptedCount": len(by_outcome["accepted"]), "blockedCount": len(by_outcome["blocked"]),
        "unresolvedCount": len(by_outcome["unresolved"]), "escalatedCount": len(by_outcome["escalated"]),
        "candidateSetHash": report.candidate_set_hash, "disagreementRate": report.disagreement_rate,
        "differentiatorNoneRate": report.differentiator_none_rate,
        "restrictionUncheckedCount": report.restriction_unchecked_count,
        "preflight": preflight.status, "written": bool(write),
    }


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Validate + bounded-self-heal one round of action-corpus candidates (A-S4).")
    ap.add_argument("--candidates", required=True, help="path to a candidate-row JSON envelope")
    ap.add_argument("--briefs", required=True, help="path to A-S1's own brief JSON envelope for this round")
    ap.add_argument("--round", type=int, default=1, dest="round_no")
    ap.add_argument("--dry-run", action="store_true",
                    help="gate the recorded candidate set with zero model calls -- preflight is skipped")
    ap.add_argument("--preflight", action="store_true", dest="do_preflight",
                    help="prove constrained decoding with one real call before running (ignored under --dry-run)")
    args = ap.parse_args(argv)

    summary = regenerate(candidates_path=Path(args.candidates), briefs_path=Path(args.briefs),
                         round_no=args.round_no, dry_run=args.dry_run,
                         do_preflight=args.do_preflight, write=not args.dry_run)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


def main(argv=None) -> int:
    return run(argv)


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
