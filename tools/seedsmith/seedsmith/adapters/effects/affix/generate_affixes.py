"""Real-run entrypoint for `affix-authoring` (T7.1, spec-affix-authoring.md, effect-pipeline module
9). Committed so the run is reproducible, matching `generate_commander_effects.py`'s own precedent.

⛔ **The eligible-atom pool, made concrete rather than left unspecified.** Neither this module's own
spec nor any earlier task named where a run's own `eligibleAtoms` should come from — recorded as a
genuine open question in `tasks/seed-to-concrete-todo.md` (T7.1's own evidence block). Resolved here
the same way `commander_effect`'s own subjects come from a committed tree: the pool is every atom id
the REAL shipped seed tree (`data/seed/atoms/**.json`) actually carries — not invented, not narrowed
by a guess at which atoms "should" be biddable, the whole shared library the model may pick from.
`--only` narrows it to a themed subset for a smaller, more controllable run.

Usage:
    python -m seedsmith.adapters.effects.affix.generate_affixes --dry-run   # briefs only
    python -m seedsmith.adapters.effects.affix.generate_affixes --count 5   # real model calls
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

from ....pipeline.llm_caller import LlmCallerConfig
from ....workflow.runner import MAX_WORKERS
from ....workflow.state import new_state
from .prompts import ID_PREFIX, build_brief, build_context, entry_for

REPO_ROOT = Path(__file__).resolve().parents[6]
ATOMS_ROOT = REPO_ROOT / "data" / "seed" / "atoms"
OUTPUT_DIR = REPO_ROOT / "data" / "seed" / "effects" / "affixes"

PROMPT_VERSION = "affix-authoring/1"


def derive_atom_id(entry: "dict") -> str:
    """Mirrors `AtomRow.DeriveId` exactly (`AtomRow.cs`): `family.t{tier}` or
    `family.{variant}.t{tier}` — the SAME split, so an id computed here always matches what the C#
    importer derives from the identical seed row."""
    family = entry["family"]
    tier = entry["tier"]
    variant = entry.get("variant") or ""
    return f"{family}.{variant}.t{tier}" if variant else f"{family}.t{tier}"


def load_eligible_atoms(atoms_root: Path, only: "list[str] | None" = None) -> "dict[str, bool]":
    """Every atom id the real shipped seed tree carries, mapped to whether IT OWN row declares a
    trigger — read fresh each call, never cached (a dev-run tool, not a hot path). Mirrors
    `AffixValidator.AffixClassOfAtom`'s own rule exactly: an atom's OWN `when.trigger` presence,
    never a kind-level default — the real seed row already carries this, so `derive_affix_class`
    reads real data here, not a guess."""
    has_trigger: "dict[str, bool]" = {}
    for path in sorted(atoms_root.glob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        if doc.get("kind") != "atom":
            continue
        for entry in doc.get("entries", []):
            atom_id = derive_atom_id(entry)
            has_trigger[atom_id] = bool((entry.get("when") or {}).get("trigger"))
    if only:
        wanted = set(only)
        has_trigger = {k: v for k, v in has_trigger.items() if k in wanted}
    return has_trigger


def load_existing() -> "dict[str, dict]":
    path = OUTPUT_DIR / "all.json"
    if not path.exists():
        return {}
    doc = json.loads(path.read_text(encoding="utf-8"))
    return {e["id"]: e for e in doc.get("entries", [])}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Author named, multi-atom affix bundles.")
    ap.add_argument("--dry-run", action="store_true", help="assemble briefs, make no model calls")
    ap.add_argument("--count", type=int, default=1, help="how many independent bundles to draw")
    ap.add_argument("--only", default="", help="comma-separated atom ids to narrow the eligible pool")
    ap.add_argument("--theme", default="", help="an optional theme hint in the brief")
    ap.add_argument("--endpoint", default="http://localhost:1234/v1/chat/completions")
    ap.add_argument("--model", default="google/gemma-4-26b-a4b-qat")
    ap.add_argument("--workers", type=int, default=MAX_WORKERS)
    args = ap.parse_args(argv)

    only = [a.strip() for a in args.only.split(",") if a.strip()] or None
    atom_triggers = load_eligible_atoms(ATOMS_ROOT, only)
    eligible = sorted(atom_triggers)
    if len(eligible) < 2:
        raise SystemExit(
            f"REFUSING TO RUN: only {len(eligible)} eligible atom(s) found under {ATOMS_ROOT} "
            "(--only may have narrowed it too far) — a bundle needs at least two.")

    context = build_context(eligible, theme_hint=args.theme)
    brief = build_brief(context)

    if args.dry_run:
        print(f"{len(eligible)} eligible atoms; no model calls made.")
        print("--- sample brief ---")
        print(brief)
        return 0

    from ....workflow.graphs.effect_affix import build_affix_authoring_graph
    from ....workflow.runner import run_many

    persisted: "dict[str, dict]" = {}
    config = LlmCallerConfig(endpoint=args.endpoint, model=args.model, attempts=2,
                             retry_delay=1.0, timeout=420)
    app = build_affix_authoring_graph(on_persist=lambda k, v: persisted.__setitem__(k, v), config=config)

    states = [new_state(f"affix-draw-{i:03d}", brief=brief, context=context) for i in range(args.count)]

    results = run_many(app, states, max_workers=args.workers)

    existing = load_existing()
    provenance = {
        "pipeline": "affix-authoring",
        "model": args.model,
        "promptVersion": PROMPT_VERSION,
        "generatedUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
    }

    from .derive import derive_affix_class

    fresh: "dict[str, dict]" = {}
    for subject_id, draft in persisted.items():
        if not isinstance(draft, dict) or "refs" not in draft:
            continue
        affix_id = f"{ID_PREFIX}{subject_id}"
        affix_class = derive_affix_class(draft["refs"], has_trigger=lambda a: atom_triggers.get(a, False))
        fresh[affix_id] = entry_for(draft, affix_id=affix_id, affix_class=affix_class, provenance=provenance)

    merged = {**existing, **fresh}
    entries = [merged[k] for k in sorted(merged)]

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIR / "all.json").write_text(
        json.dumps({"kind": "affix", "_meta": {"partition": "all"}, "entries": entries},
                   ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8")

    by_outcome: "dict[str, int]" = {}
    for r in results.values():
        by_outcome[r.get("outcome", "?")] = by_outcome.get(r.get("outcome", "?"), 0) + 1

    print(json.dumps({
        "eligibleAtoms": len(eligible), "drawn": len(fresh), "totalEntries": len(entries),
        "byOutcome": by_outcome,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
