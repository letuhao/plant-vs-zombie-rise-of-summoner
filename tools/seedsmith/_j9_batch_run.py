"""J9 de-risking batch run (owner-approved 2026-09-07): run `run_species_tree` for real, against the
real local model, for the first N species of the real 904-species roster (in the roster's own stable
`_index.json` order) -- committing REAL output to the REAL repo paths this time (never a temp
seed_root), unlike the earlier single-species PoC runs. `assign_favour_cells` is called ONCE over the
FULL real roster (all 904 ids), matching what the eventual full-840/904 production pass would compute,
so this batch's own committed content stays valid input to a later, larger continuation -- nothing is
wasted or redone.

Usage: python _j9_batch_run.py <count>   (defaults to 30)
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedsmith.adapters.trees.species.generate_tree import run_species_tree
from seedsmith.adapters.trees.species.plan import assign_favour_cells
from seedsmith.adapters.trees.species.roster import load_roster
from seedsmith.adapters.trees.nodegen import tuning as nodegen_tuning
from seedsmith.adapters.trees.plan import tuning as plan_tuning
from seedsmith.adapters.trees.targets import load as load_species_targets
from seedsmith.pipeline.llm_caller import DEFAULT_CONFIG

count = int(sys.argv[1]) if len(sys.argv) > 1 else 30

roster = load_roster()
species_targets = load_species_targets()
assignments = assign_favour_cells(roster.species_ids, species_targets)

targets = nodegen_tuning.load()
tuning = plan_tuning.load()

batch_ids = list(roster.species_ids[:count])
print(f"batch of {len(batch_ids)} species (of {len(roster.species_ids)} real roster entries)", flush=True)

results = []
for i, species_id in enumerate(batch_ids):
    anchor = roster.anchors[species_id]
    assignment = assignments[species_id]
    ordinal = roster.species_ids.index(species_id)
    t0 = time.monotonic()
    try:
        result = run_species_tree(
            species_id, anchor, ordinal, assignment.cell, list(assignment.alternates),
            targets=targets, tuning=tuning, config=DEFAULT_CONFIG, workers=4)
    except Exception as ex:  # a refused/crashed species is reported, never silently drops the batch
        elapsed = time.monotonic() - t0
        print(f"[{i+1}/{len(batch_ids)}] {species_id}: EXCEPTION after {elapsed:.1f}s: {ex}", flush=True)
        results.append({"speciesId": species_id, "error": str(ex)})
        continue
    elapsed = time.monotonic() - t0
    summary = {
        "speciesId": species_id,
        "elapsedSeconds": round(elapsed, 1),
        "resolvedCell": None if result.resolved_cell is None else {
            "aptitude": result.resolved_cell.aptitude, "element": result.resolved_cell.element,
            "status": result.resolved_cell.status},
        "favourUnresolvedReason": result.favour_unresolved_reason,
        "nodeKeyRefusedReason": result.node_key_refused_reason,
        "outcomeCounts": dict(result.outcome_counts),
        "markedNodeCount": len(result.marked_node_ids),
        "codexSummary": result.codex_summary,
        "codexUnresolvedReason": result.codex_unresolved_reason,
        "metadataWritten": result.metadata_path is not None,
    }
    results.append(summary)
    print(f"[{i+1}/{len(batch_ids)}] {json.dumps(summary)}", flush=True)

out_path = Path(__file__).resolve().parent / "_j9_batch_run_results.json"
out_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
print(f"wrote {out_path}", flush=True)
