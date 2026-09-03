"""seedsmith.adapters.actions.generate_distribution_planner — A-S1 "Engine 1"'s entrypoint
(spec-distribution-planner.md). Reads:

    data/seed/actions/_generated/role-lean.json       (A-S0 — species anchor + family membership)
    data/seed/demons/_generated/family-assignments.json (species -> family LIST, size-measured)
    data/seed/actions/type-weights.json                (A-T1 — categoryMilli/targetModeMilli/...)
    data/tuning/action-rungs.v1.json                   (the 10-row rung table)
    data/tuning/action-corpus-run.v1.json              (this module's OWN new tuning file)
    data/tuning/action-dedup.v1.json                   (A-S3's -- read, never written; MISSING in
                                                          this checkout, see distribution_planner/
                                                          tuning.py's own docstring for the default)
    data/seed/items/affix-families/*.json              (the 98-family namespace)
    data/seed/actions/pairings.json                    (read-only; 0 real hits today, see §2)

and writes, through the A-C1 envelope:

    data/seed/actions/_briefs/round-1.json             kind: "action-brief"

**Zero model calls** — every value here is read from a shipped table or derived from one; the
`--dry-run` flag previews the summary without writing, and `--full` alone never authorizes a full
run (see `distribution_planner.derive.refuse_full_run_if_ungated`).

**The `pairings.json` rewrite named in spec §3 step 6 is explicitly OUT OF SCOPE for this module's
build** — a deliberate, owner-made scoping decision, not a shortfall. The rewrite authors NEW
payoff atom families (identity-authoring, LLM-layer work per this repo's Law 2); this module only
ever READS the pairing table, verbatim, and builds the full role-ASSIGNMENT mechanism against it
(`distribution_planner.derive.assign_pairing_roles`), proven correct in
`tests/test_distribution_planner.py` against a synthetic fixture. Against the REAL, unmodified
`pairings.json` today, every brief this module emits correctly carries `pairing.role: "none"` —
the spec's own measured, expected state (§2: neither shipped payoff key exists in the 98-family
namespace), not a partial or broken result.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from .characteristic_pool.catalog import CATALOG_PATH, load_catalog
from .distribution_planner import derive as dp
from .distribution_planner.tuning import (
    DEDUP_TUNING_PATH, RUN_TUNING_PATH, load_dedup_k, load_run_tuning,
)
from .vocab import load_family_ids

__all__ = ["run", "regenerate", "ACTIONS_ROOT", "DEMONS_ROOT"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
DEMONS_ROOT = REPO_ROOT / "data" / "seed" / "demons"
RUNGS_PATH = REPO_ROOT / "data" / "tuning" / "action-rungs.v1.json"
ROLE_LEAN_PATH = ACTIONS_ROOT / "_generated" / "role-lean.json"
FAMILY_ASSIGNMENTS_PATH = DEMONS_ROOT / "_generated" / "family-assignments.json"
TYPE_WEIGHTS_PATH = ACTIONS_ROOT / "type-weights.json"
PAIRINGS_PATH = ACTIONS_ROOT / "pairings.json"
BRIEFS_DIR = ACTIONS_ROOT / "_briefs"

# A-S5 `coverage-report` is not built yet (map §7's own dependency table). This is the plausible
# path its quality-gate report would land at (`_reports/`, matching `kinds.py`'s own
# `action-coverage` KindSpec directory) -- checked for existence only, never parsed for content,
# since no module writes it yet. Its absence is exactly why `mode: "full"` is refused today.
SMOKE_GATE_EVIDENCE_PATH = ACTIONS_ROOT / "_reports" / "coverage.json"


def _canonical_dump(doc: dict) -> str:
    return json.dumps(doc, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def _family_members(family_assignments: dict) -> "dict[str, list[str]]":
    members: "dict[str, list[str]]" = {}
    for species_id, families in family_assignments.items():
        for fam in families:
            members.setdefault(fam, []).append(species_id)
    return {fam: sorted(v) for fam, v in members.items()}


def _corpus_hash(role_lean_corpus_hash: str, type_weights_lean_hash: str, run_tuning_version: int,
                 rungs_version: int) -> str:
    """A stable digest over every real input this module's OWN algorithm consumes -- never a
    wall-clock stamp. Deliberately does NOT re-hash the whole role-lean/type-weights files (their
    own `_meta` hashes already cover their content); this is a hash of THOSE hashes plus the two
    version numbers this module additionally depends on."""
    payload = {
        "roleLeanCorpusHash": role_lean_corpus_hash, "typeWeightsLeanHash": type_weights_lean_hash,
        "runTuningVersion": run_tuning_version, "rungsVersion": rungs_version,
    }
    blob = json.dumps(payload, ensure_ascii=False, sort_keys=True).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def regenerate(*, actions_root: Path = ACTIONS_ROOT, demons_root: Path = DEMONS_ROOT,
              catalog_path: Path = CATALOG_PATH, role_lean_path: Path = ROLE_LEAN_PATH,
              family_assignments_path: "Path | None" = None, type_weights_path: "Path | None" = None,
              rungs_path: Path = RUNGS_PATH, run_tuning_path: Path = RUN_TUNING_PATH,
              dedup_tuning_path: Path = DEDUP_TUNING_PATH, pairings_path: "Path | None" = None,
              full_flag: bool = False, write: bool = True) -> dict:
    """Pure computation + (optionally) one file write. Returns a summary dict for the caller to
    report -- never prints itself, so a test can call this without capturing stdout."""
    # `family_assignments_path` derives from the `demons_root` PARAMETER (a caller redirecting
    # demons_root genuinely wants its own family-assignments file read too). `type_weights_path`/
    # `pairings_path` deliberately do NOT derive from the `actions_root` parameter -- that
    # parameter names only where THIS run WRITES its own round file (`_briefs/round-1.json`
    # below), so a test can redirect the write target without also redirecting every real read.
    family_assignments_path = family_assignments_path or (demons_root / "_generated" / "family-assignments.json")
    type_weights_path = type_weights_path or (ACTIONS_ROOT / "type-weights.json")
    pairings_path = pairings_path or (ACTIONS_ROOT / "pairings.json")

    run_tuning = load_run_tuning(run_tuning_path)
    dedup_k, dedup_k_source = load_dedup_k(dedup_tuning_path)

    gate_evidence_present = SMOKE_GATE_EVIDENCE_PATH.is_file()
    dp.refuse_full_run_if_ungated(run_tuning.mode, full_flag, gate_evidence_present)

    catalog = load_catalog(catalog_path)
    species_ids = [s.species_id for s in catalog]

    role_lean_text = role_lean_path.read_text(encoding="utf-8")
    role_lean_doc = json.loads(role_lean_text)
    species_anchor = dp.parse_species_anchor(role_lean_doc)
    role_lean_corpus_hash = role_lean_doc["_meta"]["corpusHash"]

    family_assignments = json.loads(family_assignments_path.read_text(encoding="utf-8"))
    family_members = _family_members(family_assignments)

    type_weights_text = type_weights_path.read_text(encoding="utf-8")
    type_weights_doc = json.loads(type_weights_text)
    weights_by_key = dp.parse_type_weights(type_weights_doc)
    type_weights_lean_hash = type_weights_doc["_meta"]["leanHash"]

    rung_table = dp.load_rung_table(rungs_path)
    family_ids = load_family_ids()
    pairing_table = dp.load_pairing_table(pairings_path)

    rungs_doc = json.loads(rungs_path.read_text(encoding="utf-8"))
    corpus_hash = _corpus_hash(role_lean_corpus_hash, type_weights_lean_hash, run_tuning.version,
                              rungs_doc["version"])

    briefs = dp.plan_round(
        species_ids=species_ids, family_members=family_members, species_anchor=species_anchor,
        weights_by_key=weights_by_key, rung_table=rung_table, family_ids=family_ids,
        pairing_table=pairing_table, general_count=run_tuning.general_count,
        per_species_count=run_tuning.per_species_count, per_family_count=run_tuning.per_family_count,
        multiplicative_pairs=run_tuning.multiplicative_pairs,
        family_motif_max=run_tuning.family_motif_max, corpus_hash=corpus_hash,
        tuning_version=run_tuning.version, round_no=1, prompt_version=1,
    )
    briefs.sort(key=lambda b: b["briefId"])
    for b in briefs:
        dp.audit_no_magnitude_smuggling(b)

    out_doc = {
        "schemaVersion": 1,
        "kind": "action-brief",
        "_meta": {"partition": "briefs", "corpusHash": corpus_hash, "tuningVersion": run_tuning.version,
                 "round": 1},
        "entries": briefs,
    }

    if write:
        briefs_dir = actions_root / "_briefs"
        briefs_dir.mkdir(parents=True, exist_ok=True)
        (briefs_dir / "round-1.json").write_text(_canonical_dump(out_doc), encoding="utf-8")

    by_scope: "dict[str, int]" = {}
    by_role: "dict[str, int]" = {}
    restriction_count = 0
    for b in briefs:
        by_scope[b["scope"]] = by_scope.get(b["scope"], 0) + 1
        by_role[b["pairing"]["role"]] = by_role.get(b["pairing"]["role"], 0) + 1
        if not b["slot"]["structureEnforced"]:
            restriction_count += 1

    return {
        "totalBriefs": len(briefs),
        "byScope": by_scope,
        "byPairingRole": by_role,
        "restrictionUnenforcedCount": restriction_count,
        "speciesSubjects": len(species_ids),
        "familySubjects": len(family_members),
        "familyAssignedSpeciesCount": sum(len(v) for v in family_members.values()),
        "dedupK": dedup_k, "dedupKSource": dedup_k_source,
        "corpusHash": corpus_hash, "tuningVersion": run_tuning.version, "mode": run_tuning.mode,
        "written": bool(write),
    }


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Plan action-corpus briefs by quota, not by sampling (A-S1 'Engine 1').")
    ap.add_argument("--dry-run", action="store_true", help="compute and print, write nothing")
    ap.add_argument("--full", action="store_true",
                    help="required (but not sufficient) to plan a full run -- see refuse_full_run_if_ungated")
    args = ap.parse_args(argv)

    summary = regenerate(write=not args.dry_run, full_flag=args.full)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


def main(argv=None) -> int:
    return run(argv)


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
