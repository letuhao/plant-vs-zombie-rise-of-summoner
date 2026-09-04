"""Real-run entrypoint for `affix-authoring` (T7.1/T7.2, spec-affix-authoring.md, effect-pipeline
module 9). Committed so the run is reproducible, matching `generate_commander_effects.py`'s own
precedent.

⛔ **The eligible-atom pool, made concrete rather than left unspecified.** Neither this module's own
spec nor any earlier task named where a run's own `eligibleAtoms` should come from — recorded as a
genuine open question in `tasks/seed-to-concrete-todo.md` (T7.1's own evidence block). Resolved here
the same way `commander_effect`'s own subjects come from a committed tree: the pool is every atom id
the REAL shipped seed tree (`data/seed/atoms/**.json`) actually carries — not invented, not narrowed
by a guess at which atoms "should" be biddable, the whole shared library the model may pick from.
`--only` narrows it to a themed subset for a smaller, more controllable run.

⛔ **T7.2 (2026-09-05): every draw is now a real 3-way vote, not a single unpermuted call.**
`spec-affix-authoring.md`'s own "Voted fields" section requires the affix's **name/identity** and
its **ref bundle composition** to be 3-way voted, "same machinery, same `resolve_vote` semantics" as
`demon-seed`'s own `classify-pipelines` (Q25 precedent) — but until this pass, `generate_affixes.py`
made exactly one model call per draw, so there was never a second or third sample to vote over.
`run_voted_draws` below is now that caller: THREE permuted samples per draw (`permute.order_for`,
seeded with `sample_index` INSIDE the seed per spec-option-permutation.md §3 — three votes over three
identical option orders is one sample with extra steps), voted independently via `vote.resolve_vote`
on `name` and on `derive.canonical_bundle_key(refs)`. A 1-1-1 split on EITHER field is `unresolved`
and the draw is recorded, never fabricated into a persisted entry — matching this program's own
`default_for=lambda k, o: None` discipline. `resolve_vote`/`order_for` are reused verbatim (per
spec's own "Ask first: forking any piece of the reused machinery" boundary); no new vote-resolution
or permutation-seeding logic is added here.

Usage:
    python -m seedsmith.adapters.effects.affix.generate_affixes --dry-run   # briefs only
    python -m seedsmith.adapters.effects.affix.generate_affixes --count 5   # real model calls
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Mapping

from ....pipeline.llm_caller import DEFAULT_CONFIG, LlmCallerConfig
from ....workflow.runner import MAX_WORKERS
from ....workflow.state import new_state
from .prompts import ID_PREFIX, build_brief, build_context, entry_for

REPO_ROOT = Path(__file__).resolve().parents[6]
ATOMS_ROOT = REPO_ROOT / "data" / "seed" / "atoms"
OUTPUT_DIR = REPO_ROOT / "data" / "seed" / "effects" / "affixes"

PROMPT_VERSION = "affix-authoring/1"

#: Every draw is voted over exactly this many permuted samples — spec-affix-authoring.md's own
#: "Voted fields" section, same `resolve_vote` 3-0/2-1/1-1-1 contract as `demon-seed`.
SAMPLES_PER_DRAW = 3


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


def run_voted_draws(
    *,
    count: int,
    eligible: "list[str]",
    atom_triggers: "Mapping[str, bool]",
    provenance_base: "Mapping[str, Any]",
    theme_hint: str = "",
    call: "Callable[..., str] | None" = None,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    workers: int = MAX_WORKERS,
) -> "tuple[dict[str, dict], dict[str, dict], dict[str, dict]]":
    """Draws `count` affix bundles, THREE permuted samples each, and votes `name` + the ref-bundle
    composition through the exact `resolve_vote` machinery `demon-seed`'s own `run_one_species`
    already proved against real calls (2026-09-02) — reused here, not reimplemented.

    Each sample runs the FULL `build_affix_authoring_graph` (generate -> validate -> repair ->
    persist/escalate), so a sample that never produces a schema-valid bundle contributes nothing to
    that draw's vote rather than polluting it with an invalid value — the same "only a validated
    draft counts" discipline the graph already enforces for a single-sample run.

    Returns `(fresh, unresolved, results)`:
      * `fresh` — committed entries keyed by affix id, one per draw where BOTH `name` and the ref
        bundle resolved (3-0 or 2-1). Never contains a fabricated guess.
      * `unresolved` — keyed by draw id, the reason a draw produced no entry: either a 1-1-1 split
        on `name` or on the ref bundle (`"vote_unresolved"`), or fewer than `SAMPLES_PER_DRAW`
        samples ever validated (`"insufficient_valid_samples"`) — a dead sample is not a silent
        drop, it shows up here.
      * `results` — the per-sample-state graph outcome, keyed by the sample's own subject id, for a
        caller's own `byOutcome` accounting (unchanged shape from before this pass).

    `call` is injected (never imported directly) so a test proves the exact number of model calls
    made without reaching the network — the same contract every graph in this program already
    honours.
    """
    from ....workflow.graphs.effect_affix import build_affix_authoring_graph
    from ....workflow.runner import run_many
    from ...demons.anchor.permute import order_for
    from ...demons.anchor.vote import resolve_vote
    from .derive import canonical_bundle_key, derive_affix_class

    persisted: "dict[str, dict]" = {}
    app = build_affix_authoring_graph(
        on_persist=lambda k, v: persisted.__setitem__(k, v), config=config, call=call)

    # THREE permuted samples per draw — `sample_index` lives INSIDE `order_for`'s own seed
    # (species_id|field|sample_index), so a rerun over the same draw id reproduces the identical
    # three permutations; three votes over three identical option orders would be one sample with
    # extra steps, exactly what spec-option-permutation.md §3 warns against.
    draw_samples: "dict[str, list[str]]" = {}
    states: "list[dict]" = []
    for i in range(count):
        draw_id = f"affix-draw-{i:03d}"
        sample_ids: "list[str]" = []
        for sample_index in range(SAMPLES_PER_DRAW):
            shuffled = order_for(draw_id, "eligibleAtoms", sample_index, eligible)
            context = build_context(shuffled, theme_hint=theme_hint)
            brief = build_brief(context)
            subject_id = f"{draw_id}-sample-{sample_index}"
            states.append(new_state(subject_id, brief=brief, context=context))
            sample_ids.append(subject_id)
        draw_samples[draw_id] = sample_ids

    results = run_many(app, states, max_workers=workers)

    fresh: "dict[str, dict]" = {}
    unresolved: "dict[str, dict]" = {}
    for draw_id, sample_ids in draw_samples.items():
        samples = [
            persisted[sid] for sid in sample_ids
            if sid in persisted and isinstance(persisted[sid], dict) and "refs" in persisted[sid]
        ]
        if len(samples) != SAMPLES_PER_DRAW:
            unresolved[draw_id] = {
                "reason": "insufficient_valid_samples",
                "validSamples": len(samples),
                "samplesExpected": SAMPLES_PER_DRAW,
            }
            continue

        name_vote = resolve_vote([s.get("name", "") for s in samples])
        canonical_values = [canonical_bundle_key(s.get("refs") or []) for s in samples]
        refs_vote = resolve_vote(canonical_values)

        # Never the first sample by default (spec §4/vote.py's own explicit warning) — a 1-1-1 on
        # EITHER voted field means this draw has no resolved identity or no resolved bundle, so it
        # is recorded as unresolved rather than shipping one voted field next to a guessed other.
        if name_vote.value is None or refs_vote.value is None:
            unresolved[draw_id] = {
                "reason": "vote_unresolved",
                "name": {"confidence": name_vote.confidence},
                "refs": {"confidence": refs_vote.confidence},
            }
            continue

        winning_refs = next(
            s["refs"] for s, key in zip(samples, canonical_values) if key == refs_vote.value)
        affix_class = derive_affix_class(
            winning_refs, has_trigger=lambda a: atom_triggers.get(a, False))

        provenance = dict(provenance_base)
        provenance["voteConfidence"] = {"name": name_vote.confidence, "refs": refs_vote.confidence}
        minority: "dict[str, str]" = {}
        if name_vote.minority:
            minority["name"] = name_vote.minority
        if refs_vote.minority:
            minority["refs"] = refs_vote.minority
        if minority:
            provenance["voteMinority"] = minority

        affix_id = f"{ID_PREFIX}{draw_id}"
        draft = {"name": name_vote.value, "refs": list(winning_refs)}
        fresh[affix_id] = entry_for(
            draft, affix_id=affix_id, affix_class=affix_class, provenance=provenance)

    return fresh, unresolved, results


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Author named, multi-atom affix bundles.")
    ap.add_argument("--dry-run", action="store_true", help="assemble briefs, make no model calls")
    ap.add_argument(
        "--count", type=int, default=1,
        help="how many independent bundles to draw — each draw makes THREE permuted model calls "
             "and votes name+refs (spec-affix-authoring.md's own 'Voted fields'), not one")
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

    if args.dry_run:
        context = build_context(eligible, theme_hint=args.theme)
        brief = build_brief(context)
        print(f"{len(eligible)} eligible atoms; no model calls made.")
        print("--- sample brief (sample 0 of 3 — a real run permutes the atom order per draw) ---")
        print(brief)
        return 0

    config = LlmCallerConfig(endpoint=args.endpoint, model=args.model, attempts=2,
                             retry_delay=1.0, timeout=420)
    provenance_base = {
        "pipeline": "affix-authoring",
        "model": args.model,
        "promptVersion": PROMPT_VERSION,
        "generatedUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
    }

    fresh, unresolved, results = run_voted_draws(
        count=args.count, eligible=eligible, atom_triggers=atom_triggers,
        provenance_base=provenance_base, theme_hint=args.theme, config=config,
        workers=args.workers)

    existing = load_existing()
    merged = {**existing, **fresh}
    entries = [merged[k] for k in sorted(merged)]

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIR / "all.json").write_text(
        json.dumps({"schemaVersion": 1, "kind": "affix", "_meta": {"partition": "all"}, "entries": entries},
                   ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8")

    by_outcome: "dict[str, int]" = {}
    for r in results.values():
        by_outcome[r.get("outcome", "?")] = by_outcome.get(r.get("outcome", "?"), 0) + 1

    print(json.dumps({
        "eligibleAtoms": len(eligible),
        "draws": args.count,
        "samplesPerDraw": SAMPLES_PER_DRAW,
        "resolvedDraws": len(fresh),
        "unresolvedDraws": len(unresolved),
        "unresolvedDetail": unresolved,
        "totalEntries": len(entries),
        "byOutcome": by_outcome,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
