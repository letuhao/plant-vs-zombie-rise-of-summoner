"""seedsmith.adapters.actions.generate_innate_picker — A-S6's entrypoint (spec-innate-picker.md).
Reads:

    data/seed/actions/ (excluding `_rounds/`)                the already-accepted corpus, via
                                                                A-C1's own `load_committed`
                                                                (`../load.py`)
    data/seed/actions/_rounds/round-<n>/survivors.json        A-S3's survivors for the round, if
                                                                the round ran (today: usually
                                                                absent — A-S4/A-S3 have not run for
                                                                real; see `innate_picker/derive.py`'s
                                                                own module docstring)
    data/seed/actions/_generated/role-lean.json               A-S0 — leanOrder/leanSource/family/
                                                                motifs
    src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs the 84-species roster, catalog
                                                                order + element (parsed via
                                                                `characteristic_pool.catalog`)
    data/tuning/action-innate-picker.v1.json                  this module's OWN tuning file (the
                                                                five `w_t`)

and writes, through the A-C1 envelope:

    data/seed/actions/species-innate.json      kind: "action-innate"

and, only when `_rounds/round-<n>/survivors.json` holds real (un-promoted) candidate rows,
performs the promotion MOVE (spec §3.4 step 6b, review F14):

    data/seed/actions/committed-round-<n>.json           kind: "action-seed" — every round row
                                                          this module commits, the picked winner's
                                                          own `kindHint` overwritten to "innate"
    data/seed/actions/_rounds/round-<n>/survivors.json   rewritten IN PLACE — `entries` reduced to
                                                          `{"id": ..., "promoted": true}` markers

**No committed-round file name is spec'd** — §3.4 step 6b says only "written into the committed
corpus at the seed root". `committed-round-<n>.json` is this module's own choice, flagged here
rather than presented as a citation: a plain, non-underscore-prefixed root file, so A-C1's loader
picks it up automatically (zero `_manifest.json` change needed — the same reason `type-weights.json`
needs none: `load.py:_classify_files` only requires a manifest row for an UNDERSCORE-prefixed
directory or a non-envelope file, and this file is a real `kind`+`entries` envelope at the root).

**Zero model calls, permanently** — there is no LLM transport import anywhere in this module or
its `innate_picker` package (spec's own opening line; `OfflineGuaranteeTests` in this module's own
test suite asserts it directly).
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .characteristic_pool.catalog import CATALOG_PATH, load_catalog
from .innate_picker import derive as ip
from .innate_picker.tuning import INNATE_TUNING_PATH, load_innate_weights
from .load import load_committed

__all__ = ["run", "regenerate", "ACTIONS_ROOT", "ROLE_LEAN_PATH"]

REPO_ROOT = Path(__file__).resolve().parents[5]
ACTIONS_ROOT = REPO_ROOT / "data" / "seed" / "actions"
ROLE_LEAN_PATH = ACTIONS_ROOT / "_generated" / "role-lean.json"


def regenerate(*, actions_root: Path = ACTIONS_ROOT, catalog_path: Path = CATALOG_PATH,
              role_lean_path: Path = ROLE_LEAN_PATH, tuning_path: Path = INNATE_TUNING_PATH,
              round_no: int = 1, write: bool = True) -> dict:
    """Pure computation + (optionally) file writes. Returns a summary dict for the caller to
    report — never prints itself, so a test can call this without capturing stdout."""
    weights = load_innate_weights(tuning_path)
    species_rows = load_catalog(catalog_path)

    role_lean_doc = json.loads(role_lean_path.read_text(encoding="utf-8"))
    role_lean_by_key = ip.parse_role_lean_doc(role_lean_doc)

    load_result = load_committed(actions_root)          # excludes `_rounds/` already (A-C1)
    committed_rows = [e.data for e in load_result.corpus.by_kind("action-seed")]

    round_dir = actions_root / "_rounds" / f"round-{round_no}"
    survivors_path = round_dir / "survivors.json"
    round_doc: "dict | None" = None
    round_rows: "list[dict]" = []
    if survivors_path.is_file():
        round_doc = json.loads(survivors_path.read_text(encoding="utf-8"))
        # A row already reduced to a `promoted` marker (an earlier run of this module) carries no
        # `scope` -- never re-processed, matching "one id exists in exactly one place".
        round_rows = [r for r in (round_doc.get("entries") or []) if "scope" in r]

    all_accepted = committed_rows + round_rows

    picks, promotions = ip.pick_all_species(
        species_rows=species_rows, role_lean_by_key=role_lean_by_key,
        candidate_rows=all_accepted, weights=weights,
    )
    entries = ip.build_entries(picks)

    innate_doc = ip.build_envelope(entries, meta={
        "partition": "innate", "corpusHash": ip.corpus_hash(all_accepted),
        "tuningVersion": weights.version,
    })

    if write:
        (actions_root / "species-innate.json").write_text(
            ip.canonical_dump(innate_doc), encoding="utf-8")

    committed_written = False
    if round_rows and write:
        promoted_rows = ip.apply_promotions(round_rows, promotions)
        committed_doc = ip.build_committed_envelope(promoted_rows, meta={
            "partition": f"round-{round_no}", "round": round_no,
            "corpusHash": ip.corpus_hash(promoted_rows),
        })
        (actions_root / f"committed-round-{round_no}.json").write_text(
            ip.canonical_dump(committed_doc), encoding="utf-8")

        markers = ip.reduce_round_survivors_to_markers(round_rows)
        new_round_doc = dict(round_doc)
        new_round_doc["entries"] = markers
        survivors_path.write_text(ip.canonical_dump(new_round_doc), encoding="utf-8")
        committed_written = True

    picked_count = sum(1 for p in picks if p.innate_action_id is not None)
    return {
        "round": round_no,
        "speciesCount": len(species_rows),
        "pickedCount": picked_count,
        "nullCount": len(picks) - picked_count,
        "candidateCount": len(all_accepted),
        "committedRoundWritten": committed_written,
        "written": bool(write),
    }


def run(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description="Pick each species' innate action and commit the accepted corpus (A-S6).")
    ap.add_argument("--round", type=int, default=1, dest="round_no")
    ap.add_argument("--dry-run", action="store_true", help="compute and print, write nothing")
    args = ap.parse_args(argv)

    summary = regenerate(round_no=args.round_no, write=not args.dry_run)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


def main(argv=None) -> int:
    return run(argv)


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
