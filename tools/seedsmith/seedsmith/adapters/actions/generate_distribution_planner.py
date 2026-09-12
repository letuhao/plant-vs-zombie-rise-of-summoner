"""seedsmith.adapters.actions.generate_distribution_planner — A-S1 "Engine 1"'s entrypoint
(spec-distribution-planner.md). Reads:

    data/seed/actions/_generated/role-lean.json       (A-S0 — species anchor + family membership)
    data/seed/demons/species/**/*.json                      (live species and family fields)
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
import re
from pathlib import Path

from .characteristic_pool.catalog import (
    CATALOG_PATH, derive_live_family_assignments, load_catalog,
)
from .distribution_planner import derive as dp
from .distribution_planner.tuning import (
    DEDUP_TUNING_PATH, RUN_TUNING_PATH, load_dedup_k, load_run_tuning,
)
from .dedup_select.derive import parse_candidate
from .vocab import load_family_ids

__all__ = ["run", "regenerate", "is_passing_quality_gate", "ACTIONS_ROOT", "DEMONS_ROOT"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
DEMONS_ROOT = REPO_ROOT / "data" / "seed" / "demons"
RUNGS_PATH = REPO_ROOT / "data" / "tuning" / "action-rungs.v1.json"
ROLE_LEAN_PATH = ACTIONS_ROOT / "_generated" / "role-lean.json"
TYPE_WEIGHTS_PATH = ACTIONS_ROOT / "type-weights.json"
PAIRINGS_PATH = ACTIONS_ROOT / "pairings.json"
BRIEFS_DIR = ACTIONS_ROOT / "_briefs"

#: Bump ONLY when a change alters the briefs this module emits for unchanged inputs. Folded into
#: `_corpus_hash` so a plan produced by an older algorithm fails the freshness check rather than
#: silently resuming candidates generated against a different ordinal order. History:
#:   1 — 2026-09-11: initial explicit version; the ordinal-spreading fix in `expand_counts` (marginals
#:       unchanged, emitted order changed) is the change that motivated making the algorithm
#:       versioned at all.
#:   2 — 2026-09-12: generalized scope-level allocation. `apportion_axis` (deficit-greedy) now also
#:       drives `targetMode`, and `apportion_area_shapes` drives `areaShape`. `area` and its four
#:       shapes were unreachable at family/species scope before this (0 of 6,655 briefs); now ~750
#:       species / ~188 family briefs carry them. Marginals change, so the version must move.
ALGORITHM_VERSION = 2

# A-S5 writes the round-scoped quality-gate report at this path. The full-run gate below checks the
# report's measured verdict, not mere file presence.
SMOKE_GATE_EVIDENCE_PATH = ACTIONS_ROOT / "_reports" / "coverage-round-1.json"


def _canonical_dump(doc: dict) -> str:
    return json.dumps(doc, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def is_passing_quality_gate(path: Path, *, round_no: int = 1) -> bool:
    """Return whether an A-S5 report can authorize a full A-S1 plan.

    A report is evidence only when it is the expected envelope for this round and its closed
    metrics explicitly passed.  This deliberately fails closed for stale smoke output, malformed
    JSON, a review/reject envelope, an unevaluated metric, or any remaining gap.
    """
    if not path.is_file():
        return False
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return False
    if not isinstance(doc, dict) or doc.get("kind") != "action-coverage":
        return False
    meta = doc.get("_meta")
    verdict = meta.get("verdict") if isinstance(meta, dict) else None
    if not isinstance(meta, dict) or meta.get("round") != round_no or not isinstance(verdict, dict):
        return False
    # A clean smoke report is also a passing quality gate. A full report uses `pass`; a smoke
    # report uses `smoke-clean` because A-S5 deliberately never labels a partial run `pass`.
    return (verdict.get("verdict") in {"pass", "smoke-clean"} and
            verdict.get("notMeasuredMetrics") == [] and
            verdict.get("gapMetrics") == [])


def _family_members(family_assignments: dict) -> "dict[str, list[str]]":
    members: "dict[str, list[str]]" = {}
    for species_id, families in family_assignments.items():
        if isinstance(families, str):
            families = [families]
        for fam in families:
            members.setdefault(fam, []).append(species_id)
    return {fam: sorted(v) for fam, v in members.items()}


def _accepted_neighbours_by_group(
        actions_root: Path, *, before_round: int) -> "dict[tuple[str, str | None], list[tuple[str, dp.FingerprintComponents]]]":
    """§3 step 8's accepted-corpus input, grouped by `(scope, scopeKey)`.

    Reads only rounds **strictly earlier** than the round being planned
    (`committed-round-<n>.json`, `n < before_round`). This is the spec's own round-1 rule made
    precise: "round 1 reading no report" (`spec-distribution-planner.md` §7) exists so the plan is
    not circular, and the same discipline applies to the accepted corpus — a round must never be
    planned against its own already-committed output. On the live tree that means round 1 reads
    nothing (the shipped `committed-round-1/2/909.json` are not earlier than round 1), which is
    exactly what `test_round_1_has_no_accepted_corpus_avoid_neighbours_empty` pins; planning round
    910 would read all three.

    Each row is rendered with **A-S3's own `parse_candidate`**, so the neighbour fingerprint and the
    one A-S3 will judge against are one definition, never a second one shaped like it. A malformed
    committed row is skipped rather than aborting the plan: the accepted corpus is produced by an
    earlier round, and one unparseable row must not make the whole plan unbuildable. The same rows
    are separately reported by `load.load_committed`.
    """
    if before_round <= 1:
        return {}
    family_ids = load_family_ids()
    grouped: "dict[tuple[str, str | None], list[tuple[str, dp.FingerprintComponents]]]" = {}
    pattern = re.compile(r"^committed-round-(\d+)\.json$")
    for path in sorted(actions_root.glob("committed-round-*.json")):
        match = pattern.match(path.name)
        if match is None or int(match.group(1)) >= before_round:
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        for row in doc.get("entries") or ():
            if not isinstance(row, dict):
                continue
            try:
                candidate = parse_candidate(row, family_ids)
            except ValueError:
                continue
            grouped.setdefault((candidate.scope, candidate.scope_key), []).append(
                (candidate.id, candidate.fp))
    return grouped


def _corpus_hash(role_lean_corpus_hash: str, type_weights_lean_hash: str, run_tuning_version: int,
                 rungs_version: int) -> str:
    """A stable digest over every real input this module's OWN algorithm consumes -- never a
    wall-clock stamp. Deliberately does NOT re-hash the whole role-lean/type-weights files (their
    own `_meta` hashes already cover their content); this is a hash of THOSE hashes plus the two
    version numbers this module additionally depends on.

    **⛔ ALGORITHM_VERSION is folded in — the 2026-09-11 freshness hole.** The payload originally
    covered only INPUTS (role-lean/type-weights hashes, tuning/rungs versions). A change to a pure
    function the planner owns -- e.g. the 2026-09-11 ordinal-spreading fix in `expand_counts`, whose
    marginals are identical but whose emitted order is not -- therefore produced different briefs
    under the SAME hash. The stale plan would still pass `_load_plan`'s freshness check, and
    `load_resume_entries` would accept candidates generated against the old ordinal order. Bumping
    this integer whenever `distribution_planner.derive`'s OUTPUT for unchanged inputs changes makes
    the freshness check cover the algorithm, not just its inputs. It is not a tuning value and must
    never be moved by a balance pass -- only by a change that alters emitted briefs."""
    payload = {
        "roleLeanCorpusHash": role_lean_corpus_hash, "typeWeightsLeanHash": type_weights_lean_hash,
        "runTuningVersion": run_tuning_version, "rungsVersion": rungs_version,
        "algorithmVersion": ALGORITHM_VERSION,
    }
    blob = json.dumps(payload, ensure_ascii=False, sort_keys=True).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def regenerate(*, actions_root: Path = ACTIONS_ROOT, demons_root: Path = DEMONS_ROOT,
              catalog_path: Path = CATALOG_PATH, role_lean_path: Path = ROLE_LEAN_PATH,
              family_assignments_path: "Path | None" = None, type_weights_path: "Path | None" = None,
              rungs_path: Path = RUNGS_PATH, run_tuning_path: Path = RUN_TUNING_PATH,
              dedup_tuning_path: Path = DEDUP_TUNING_PATH, pairings_path: "Path | None" = None,
              full_flag: bool = False, write: bool = True, round_no: int = 1) -> dict:
    """Pure computation + (optionally) one file write. Returns a summary dict for the caller to
    report -- never prints itself, so a test can call this without capturing stdout."""
    # `type_weights_path`/`pairings_path` deliberately do NOT derive from the `actions_root` parameter -- that
    # parameter names only where THIS run WRITES its own round file (`_briefs/round-1.json`
    # below), so a test can redirect the write target without also redirecting every real read.
    type_weights_path = type_weights_path or (ACTIONS_ROOT / "type-weights.json")
    pairings_path = pairings_path or (ACTIONS_ROOT / "pairings.json")

    run_tuning = load_run_tuning(run_tuning_path)
    dedup_k, dedup_k_source = load_dedup_k(dedup_tuning_path)

    gate_passed = is_passing_quality_gate(SMOKE_GATE_EVIDENCE_PATH)
    dp.refuse_full_run_if_ungated(run_tuning.mode, full_flag, gate_passed)

    catalog = load_catalog(catalog_path)
    species_ids = [s.species_id for s in catalog]

    role_lean_text = role_lean_path.read_text(encoding="utf-8")
    role_lean_doc = json.loads(role_lean_text)
    species_anchor = dp.parse_species_anchor(role_lean_doc)
    role_lean_corpus_hash = role_lean_doc["_meta"]["corpusHash"]

    using_live_families = family_assignments_path is None
    if using_live_families:
        live_species_root = catalog_path if catalog_path.is_dir() else demons_root / "species"
        family_assignments = derive_live_family_assignments(live_species_root)
    else:
        family_assignments = json.loads(family_assignments_path.read_text(encoding="utf-8"))
    family_members = _family_members(family_assignments)
    if using_live_families and set(family_assignments) != set(species_ids):
        raise ValueError("live family assignments do not cover exactly the live species roster")

    if set(species_anchor) != set(species_ids):
        missing = sorted(set(species_ids) - set(species_anchor))
        extra = sorted(set(species_anchor) - set(species_ids))
        raise ValueError(
            "role-lean.json is stale relative to the live species seed folder: "
            f"missing={missing[:5]!r}, extra={extra[:5]!r}"
        )

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

    accepted_neighbours = _accepted_neighbours_by_group(actions_root, before_round=round_no)

    briefs = dp.plan_round(
        species_ids=species_ids, family_members=family_members, species_anchor=species_anchor,
        weights_by_key=weights_by_key, rung_table=rung_table, family_ids=family_ids,
        pairing_table=pairing_table, general_count=run_tuning.general_count,
        per_species_count=run_tuning.per_species_count, per_family_count=run_tuning.per_family_count,
        multiplicative_pairs=run_tuning.multiplicative_pairs,
        family_motif_max=run_tuning.family_motif_max, corpus_hash=corpus_hash,
        tuning_version=run_tuning.version, round_no=round_no, prompt_version=1,
        accepted_neighbours_by_group=accepted_neighbours, avoid_neighbour_k=dedup_k,
    )
    briefs.sort(key=lambda b: b["briefId"])
    for b in briefs:
        dp.audit_no_magnitude_smuggling(b)

    out_doc = {
        "schemaVersion": 1,
        "kind": "action-brief",
        "_meta": {"partition": "briefs", "corpusHash": corpus_hash, "tuningVersion": run_tuning.version,
                 "round": round_no},
        "entries": briefs,
    }

    if write:
        briefs_dir = actions_root / "_briefs"
        briefs_dir.mkdir(parents=True, exist_ok=True)
        (briefs_dir / f"round-{round_no}.json").write_text(_canonical_dump(out_doc), encoding="utf-8")

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
