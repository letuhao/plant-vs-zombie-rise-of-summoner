"""seedsmith.adapters.trees.plan.emit — orchestrates one tree's plan (task B1).

`python -m seedsmith trees plan --emit` (spec-tree-plan.md Commands). No model calls, no RNG —
every value here is a pure function of the tuning files, the roster mirrors, and (for node ids)
whatever plan is already committed on disk.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from . import gates as gates_mod
from . import ids as ids_mod
from . import invariants as invariants_mod
from .archetypes import (
    PAIRING_RULE,
    SHIPPED_ARCHETYPES,
    TIER_COUNT,
    Archetype,
    assign_archetype,
    check_reward_spread,
    max_node_milli,
    mechanism_nodes,
    reward_per_point_milli_by_tier,
)
from .ladder import TierLadder, node_budget_milli, tier_budget_milli
from .vocabulary import load_family_roster_or_pending, load_property_vocabulary, load_roster

# spec-tree-plan.md's Manifest schema table: "plannerVersion | string | FROZEN — bump on any math
# change". Manifest-only — the per-tree files keep their own pre-existing `schemaVersion`/`version`
# fields untouched (task B1/C1, out of C2's scope to rename).
PLANNER_VERSION = "1.0.0"
MANIFEST_SCHEMA_VERSION = 1

REPO_ROOT = Path(__file__).resolve().parents[6]

# CLAUDE.md's numeric-overflow rule: widen before multiplying. Same explicit `long`-bound stand-in
# every other adapter's own `_widen_mul` uses (Python ints do not overflow; this is the check that
# stands in for the `long` a C# consumer eventually reads this plan as).
_LONG_MAX = 9_223_372_036_854_775_807
_LONG_MIN = -9_223_372_036_854_775_808


def _widen_mul(a: int, b: int) -> int:
    a = int(a)
    b = int(b)
    product = a * b
    if product > _LONG_MAX or product < _LONG_MIN:
        raise OverflowError(f"_widen_mul({a}, {b}) = {product} does not fit a long ({_LONG_MIN}..{_LONG_MAX})")
    return product


class EmitError(ValueError):
    """The plan could not be emitted — a refusal, never a silent partial write."""


class GateCurrencyRefusal(EmitError):
    """R-G0: a plan whose ladder.gateCurrency is anything but 'aptitudePoints' is refused."""


def canonical_json_bytes(doc: dict) -> bytes:
    """The ONE serializer every emitted file goes through (spec-tree-plan.md's Reproducibility
    section): sorted keys, 2-space indent, `\\n` line endings, UTF-8 with no BOM, no trailing
    whitespace. `json.dumps` already emits a bare `\\n` between lines regardless of OS — it never
    reads `os.linesep` — so the only hazard is downstream: `Path.write_text`'s DEFAULT `newline`
    argument re-translates every `\\n` back into `os.linesep` on write, which is `\\r\\n` on
    Windows. That is a real, reproduced defect: the working-tree `might.v1.json` this task found
    had `\\r\\n` endings throughout (`write_text(..., encoding="utf-8")` with no `newline=""`),
    which would NOT be byte-identical to a Linux checkout of the same content — exactly what this
    section says must never happen. Returning `bytes` here and writing them with `Path.write_bytes`
    (never `write_text`) is what makes the round trip actually byte-identical."""
    text = json.dumps(doc, indent=2, sort_keys=True, ensure_ascii=False)
    return (text + "\n").encode("utf-8")


def content_sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def tree_content_hash(tree_plan: dict) -> str:
    """A tree's own `sha256` (the manifest's `trees[].sha256` field) — sha256 over that tree's own
    canonical JSON bytes, the same bytes that get written to `plan/<treeId>.v1.json`."""
    return content_sha256(canonical_json_bytes(tree_plan))


def _milli_shares_to_points(total_points: int, shares_milli: "list[int]") -> "list[int]":
    """Converts a branch's ‰ shares (summing to exactly 1000, by construction) into absolute
    `budgetPoints` summing to exactly `total_points` — floor every share but the last, and let the
    last absorb the residual, the SAME "round down, residual to the last slot" convention
    `tier_budget_milli`/`node_budget_milli` already use elsewhere in this module (never a second
    rounding rule). Widened before multiplying, divided by 1000 once, last."""
    points = [_widen_mul(total_points, share) // 1000 for share in shares_milli[:-1]]
    points.append(total_points - sum(points))
    return points


def _build_archetypes_block(tuning: dict) -> "list[dict]":
    """`archetypes[].*` (spec-tree-plan.md's frozen manifest schema): one entry per SHIPPED
    archetype, corpus-shared data — every tree's plan carries the same block until a real
    multi-tree manifest file exists (C2's scope), so a diff between any two trees of different
    archetypes already shows the §3.1 pacing gradient and the §3 shape difference without waiting
    on that manifest to land."""
    mechanism_cfg = tuning["mechanism"]
    unlock_cfg = tuning["unlockCost"]
    tier_shares = tier_budget_milli(TIER_COUNT)
    result = []
    for a in SHIPPED_ARCHETYPES:
        node_budget_by_tier = [list(node_budget_milli(tier_shares[t - 1], a.widths[t - 1]))
                               for t in range(1, TIER_COUNT + 1)]
        result.append({
            "id": a.id,
            "widths": list(a.widths),
            "nodeBudgetMilli": node_budget_by_tier,
            "maxNodeMilli": max_node_milli(a, TIER_COUNT),
            "rewardPerPointMilli": list(reward_per_point_milli_by_tier(
                a, unlock_cfg["firstPoints"], unlock_cfg["stepPoints"])),
            "mechNodes": list(mechanism_nodes(a, TIER_COUNT, mechanism_cfg["rampStartMilli"],
                                              mechanism_cfg["rampEndMilli"])),
        })
    return result


@dataclass(frozen=True)
class TreeSpec:
    """The inputs B1 needs to plan ONE tree — the seed this module turns into a full plan.
    `ordinal` drives archetype assignment (append-safe, spec-tree-plan.md §3.1)."""
    tree_id: str
    category: str  # "primary" | "elemental" | "status" | "family" | "species" (R7)
    ordinal: int
    gate_quantity: str
    gate_index_kind: str
    gate_state: str  # "carrier" | "pending" (R-G1)


def might_tree_spec(seed_root: "Path | None" = None) -> TreeSpec:
    """The specific tree B1's acceptance criteria name: `Might`, a primary tree, shipped and
    reachable today (spec-tree-plan.md §7's table — primary trees' gate quantity already has a
    production carrier: `PointBudget.PointsFor(AllocationScope.Commander, ...)`).

    task C2: `gateState` is READ from the checked-in evidence file
    (`data/seed/passive-tree/gate-evidence.v1.json`), never hand-typed here — this function used to
    write `gate_state="carrier"` as a literal, which is exactly the "the planner resolves a
    quantity itself" defect §7.1 forbids."""
    root = seed_root or (REPO_ROOT / "data" / "seed")
    evidence = gates_mod.load_gate_evidence(root)
    gate_index_kind = "aptitudePoints"
    gate_state = gates_mod.resolve_gate_state(gate_index_kind, evidence)
    return TreeSpec(
        tree_id="might", category="primary", ordinal=0,
        gate_quantity="aptitude.Might@Commander", gate_index_kind=gate_index_kind,
        gate_state=gate_state,
    )


def _read_existing_plan(path: Path) -> "dict | None":
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def _existing_node_keys(existing_plan: "dict | None") -> "dict[tuple[str, int, int], str]":
    """R3: read back every previously-minted key, keyed by its exact (branch, tier, index) slot."""
    if existing_plan is None:
        return {}
    result: "dict[tuple[str, int, int], str]" = {}
    for node in existing_plan.get("nodes", []):
        result[(node["branch"], node["tier"], node["indexInTier"])] = node["nodeKey"]
    return result


def build_plan(spec: TreeSpec, tuning: dict, existing_plan: "dict | None" = None,
               seed_root: "Path | None" = None) -> dict:
    """Pure function: `(spec, tuning, existing_plan) -> plan document`. No file I/O of its own
    beyond the roster/vocabulary reads `vocabulary.py` already isolates."""
    ladder_cfg = tuning["tierLadder"]
    budget_cfg = tuning["budget"]
    potency_cfg = tuning["potency"]
    mechanism_cfg = tuning["mechanism"]
    archetype_cfg = tuning["archetype"]
    unlock_cfg = tuning["unlockCost"]

    k = ladder_cfg["reqScalePoints"]
    ladder = TierLadder(tier_count=TIER_COUNT, req_scale_points=k, b=1)

    archetype = assign_archetype(spec.ordinal, SHIPPED_ARCHETYPES)

    # R-A1 refuses BEFORE anything is minted — cheap, and it is arithmetic over the shipped
    # archetype set alone, not over this one tree. Wrapped so every corpus-level invariant this
    # module owns surfaces through the SAME EmitError contract the CLI already catches (task C1:
    # invariants.py owns the arithmetic, emit.py owns turning a violation into a refused emit).
    try:
        check_reward_spread(SHIPPED_ARCHETYPES, TIER_COUNT, unlock_cfg["firstPoints"], unlock_cfg["stepPoints"],
                            archetype_cfg["rewardSpreadMaxRatioMilli"])
        # C1's inverse guard — the archetype set itself must not have collapsed into "every tree
        # feels the same" (spec-tree-plan.md §3's own Testing row, archetype_shapes_actually_differ).
        invariants_mod.check_archetype_shapes_actually_differ(SHIPPED_ARCHETYPES, TIER_COUNT)
    except invariants_mod.PlanInvariantError as ex:
        raise EmitError(str(ex)) from ex

    tier_shares = tier_budget_milli(TIER_COUNT)
    if sum(tier_shares) != 1000:
        raise EmitError(f"tier budget column sums to {sum(tier_shares)}, not 1000 — C1 violated")

    mech_nodes_per_tier = mechanism_nodes(archetype, TIER_COUNT, mechanism_cfg["rampStartMilli"],
                                          mechanism_cfg["rampEndMilli"])
    try:
        # R-M1, proven numerically rather than trusted from mechanism_nodes's own docstring claim.
        invariants_mod.check_r_m1_deepest_tier_is_all_mechanism(
            archetype, TIER_COUNT, mechanism_cfg["rampStartMilli"], mechanism_cfg["rampEndMilli"])
        # R-M2 — the shared ramp must never get LESS mechanical as it deepens.
        invariants_mod.check_r_m2_mechanism_share_is_monotone(
            TIER_COUNT, mechanism_cfg["rampStartMilli"], mechanism_cfg["rampEndMilli"])
    except invariants_mod.PlanInvariantError as ex:
        raise EmitError(str(ex)) from ex

    # P-1 — the tuning file's own potency.maxNodeShareMilli must equal what this tierCount and
    # minTerminalWidth derive, never a hand-edited constant (spec-tree-plan.md §5.1/§5.2).
    try:
        invariants_mod.check_p1_potency_ceiling_is_derived(
            potency_cfg["maxNodeShareMilli"], TIER_COUNT, potency_cfg["minTerminalWidth"])
    except invariants_mod.PlanInvariantError as ex:
        raise EmitError(str(ex)) from ex

    existing_keys = _existing_node_keys(existing_plan)
    next_ordinal: "dict[tuple[str, str, int], int]" = {}

    nodes: "list[dict]" = []
    for branch in ("offensive", "defensive"):
        for t in range(1, TIER_COUNT + 1):
            width = archetype.widths[t - 1]
            shares = node_budget_milli(tier_shares[t - 1], width)
            mech_count = mech_nodes_per_tier[t - 1]
            keys = ids_mod.mint_node_keys(spec.tree_id, branch, t, width, existing_keys, next_ordinal)
            # R3's refusal: a freshly-minted key must never collide with one already committed at a
            # DIFFERENT index in this same (branch, tier) slot — nodeKey is unique per slot, not
            # globally, so this check is scoped here rather than across the whole tree. This is the
            # real "mint over an existing key" case: a partial existing set with a gap (e.g. index 0
            # already has "n0") could otherwise make a fresh mint for a later index reuse "n0" too.
            ids_mod.refuse_if_key_reused(keys)
            for index in range(width):
                node_id = ids_mod.node_id(spec.tree_id, branch, t, keys[index])
                node_class = "mechanism" if index < mech_count else "magnitude"
                share = shares[index]
                if share > potency_cfg["maxNodeShareMilli"]:
                    raise EmitError(
                        f"P-2 violated: {node_id} budgetShareMilli={share} exceeds "
                        f"potency.maxNodeShareMilli={potency_cfg['maxNodeShareMilli']}")
                nodes.append({
                    "id": node_id, "nodeKey": keys[index], "branch": branch, "tier": t,
                    "indexInTier": index, "nodeClass": node_class, "budgetShareMilli": share,
                })

    offensive_total = sum(n["budgetShareMilli"] for n in nodes if n["branch"] == "offensive")
    defensive_total = sum(n["budgetShareMilli"] for n in nodes if n["branch"] == "defensive")
    if offensive_total != 1000 or defensive_total != 1000:
        raise EmitError(
            f"C1 violated: offensive={offensive_total}, defensive={defensive_total}, both must be 1000")

    # `nodes[].budgetPoints` — what the binder actually prices against (spec-tree-plan.md's frozen
    # schema table), converted from the already-checked ‰ shares. `budgetPerBranch` is
    # `budgetTotal * branchSplitMilli / 1000` (D6's 50/50 split), widened before multiplying,
    # divided once, last.
    budget_total = budget_cfg["treeTotalPoints"]
    budget_per_branch = _widen_mul(budget_total, budget_cfg["branchSplitMilli"]) // 1000
    for branch in ("offensive", "defensive"):
        branch_nodes = [n for n in nodes if n["branch"] == branch]
        points = _milli_shares_to_points(budget_per_branch, [n["budgetShareMilli"] for n in branch_nodes])
        for node, node_points in zip(branch_nodes, points):
            node["budgetPoints"] = node_points

    try:
        invariants_mod.check_branch_budget_symmetry(spec.tree_id, nodes)
    except invariants_mod.PlanInvariantError as ex:
        raise EmitError(str(ex)) from ex

    root = seed_root or (REPO_ROOT / "data" / "seed")
    roster = load_roster(root)
    vocab = load_property_vocabulary(TIER_COUNT, root)
    demon_families, families_pending = load_family_roster_or_pending(root)

    pending: "list[str]" = []
    if families_pending:
        pending.append("demonFamilies")

    gate_currency = "aptitudePoints"
    if gate_currency != "aptitudePoints":
        raise GateCurrencyRefusal(f"ladder.gateCurrency='{gate_currency}' — R-G0 requires 'aptitudePoints'")

    # R-G2 (task C2): generationWave is DERIVED from gateState, never hand-assigned
    # (invariants.derive_generation_wave's own docstring for the exact rule and its resolved
    # ambiguity against §7.1's four-wave worked table).
    generation_wave = invariants_mod.derive_generation_wave(spec.gate_state)

    plan = {
        "schemaVersion": 1,
        "version": 1,
        "treeId": spec.tree_id,
        "category": spec.category,
        "archetype": archetype.id,
        "gateQuantity": spec.gate_quantity,
        "gateIndexKind": spec.gate_index_kind,
        "gateState": spec.gate_state,
        "generationWave": generation_wave,
        "ladder": {
            "gateCurrency": gate_currency,
            "reqScalePoints": k,
            "reqByTier": list(ladder.req_by_tier),
            "tierCount": TIER_COUNT,
        },
        "budget": {
            "tierShareMilli": list(tier_shares),
            "branchSplitMilli": budget_cfg["branchSplitMilli"],
        },
        "budgetTotal": budget_total,
        "budgetPerBranch": budget_per_branch,
        "potency": {
            "maxNodeShareMilli": potency_cfg["maxNodeShareMilli"],
            "minTerminalWidth": potency_cfg["minTerminalWidth"],
        },
        "roster": {
            "aptitudes": list(roster.aptitudes),
            "elements": list(roster.elements),
            "statuses": list(roster.statuses),
            "demonFamilies": list(demon_families),
            "counts": {**roster.counts, "demonFamilies": len(demon_families)},
        },
        "_pending": pending,
        "propertyVocabulary": {axis: list(members) for axis, members in vocab.axes.items()},
        "propertyVocabularyCounts": vocab.counts,
        "mechNodesByTier": list(mech_nodes_per_tier),
        "archetypes": _build_archetypes_block(tuning),
        "nodes": nodes,
    }

    try:
        invariants_mod.check_pending_declared_for_empty_rosters(plan)
    except invariants_mod.PlanInvariantError as ex:
        raise EmitError(str(ex)) from ex

    return plan


def plan_path(tree_id: str) -> Path:
    return REPO_ROOT / "data" / "seed" / "passive-tree" / "plan" / f"{tree_id}.v1.json"


def emit(spec: TreeSpec, tuning: dict, seed_root: "Path | None" = None) -> Path:
    """Writes the plan, reading back any already-committed keys first (R3) so a re-emit never
    re-mints. Returns the path written.

    Writes via `canonical_json_bytes` + `Path.write_bytes` (task C2) — NEVER `write_text`, whose
    default `newline` argument re-translates `\\n` into `os.linesep` on write and silently breaks
    the Windows/Linux byte-identical round trip the Reproducibility section requires."""
    path = plan_path(spec.tree_id)
    existing = _read_existing_plan(path)
    plan = build_plan(spec, tuning, existing_plan=existing, seed_root=seed_root)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(canonical_json_bytes(plan))
    return path


def check(spec: TreeSpec, tuning: dict, seed_root: "Path | None" = None) -> "list[str]":
    """Regenerates in memory and diffs against the committed plan. Returns a list of differing
    top-level-or-nested paths (empty means clean). Exit code mapping is the CLI's job."""
    path = plan_path(spec.tree_id)
    existing = _read_existing_plan(path)
    if existing is None:
        raise EmitError(f"EXIT_CANNOT_RUN: no committed plan at {path} to check against")
    regenerated = build_plan(spec, tuning, existing_plan=existing, seed_root=seed_root)
    diffs: "list[str]" = []
    _diff_json(existing, regenerated, "$", diffs)
    return diffs


def _diff_json(a, b, path: str, out: "list[str]") -> None:
    if type(a) is not type(b):
        out.append(f"{path}: type differs ({type(a).__name__} vs {type(b).__name__})")
        return
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a:
                out.append(f"{path}.{key}: only in regenerated")
            elif key not in b:
                out.append(f"{path}.{key}: only in committed")
            else:
                _diff_json(a[key], b[key], f"{path}.{key}", out)
    elif isinstance(a, list):
        if len(a) != len(b):
            out.append(f"{path}: length differs ({len(a)} vs {len(b)})")
            return
        for i, (x, y) in enumerate(zip(a, b)):
            _diff_json(x, y, f"{path}[{i}]", out)
    else:
        if a != b:
            out.append(f"{path}: {a!r} != {b!r}")


# ── The manifest — `data/seed/passive-tree/plan.v1.json` (task C2) ─────────────────────────────
#
# One file per tree (`build_plan`/`emit`/`check` above, task B1/C1) plus ONE manifest indexing all
# of them, `_provenance`, and `planHash` (spec-tree-plan.md's Manifest schema table + the
# Reproducibility section). Building the manifest never re-derives a per-tree value — it calls
# `build_plan` per spec and only adds the corpus-level index and hash on top.

_MANIFEST_INPUT_FILES: "tuple[tuple[str, ...], ...]" = (
    ("aptitudes", "roster.json"),
    ("elements", "roster.json"),
    ("statuses", "roster.json"),
    ("atoms", "vocabulary.json"),
    ("derived-stats", "catalog.json"),
    ("demons", "_registry", "families.v1.json"),  # optional — F=0 is a declared `_pending`, not a refusal
    ("passive-tree", "gate-evidence.v1.json"),     # task C2's own new input (R-G1's evidence row)
)

_MANIFEST_TUNING_FILES: "tuple[tuple[str, str], ...]" = (
    ("passive-tree", "passive-tree.v1.json"),
    ("passive-tree-targets", "passive-tree-targets.v1.json"),
)


def _relative_or_absolute(path: Path) -> str:
    """`_provenance.inputs[].path` — relative to the repo root when the file is actually inside
    this checkout (the normal case), or the raw path otherwise (a test's scratch seed root) so
    provenance never raises over a path it merely cannot make relative."""
    try:
        return path.relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def _provenance_inputs(seed_root: Path) -> "list[dict]":
    """Every mirror `tree-plan` is a pure function of, hashed (spec-tree-plan.md's Reproducibility
    table). Never re-derives their CONTENT — `vocabulary.py` already refused earlier in
    `build_plan` if a required one were missing; this only hashes what is already known to exist,
    except the optional family roster, which is hashed only when present (F=0 is `_pending`, not a
    refusal)."""
    inputs: "list[dict]" = []
    for parts in _MANIFEST_INPUT_FILES:
        path = seed_root.joinpath(*parts)
        if not path.exists():
            continue
        inputs.append({"path": _relative_or_absolute(path), "sha256": content_sha256(path.read_bytes())})
    return inputs


def _provenance_tuning(tuning_root: "Path | None" = None) -> "list[dict]":
    """`_provenance.tuning`: `{domain, version, sha256}` for each of `tree-plan`'s two tuning
    files, read from each file's own `version` field — never a filename-derived guess (R6's
    `classes.v2.json` trap is a different module's job to enforce; this only reports what the file
    itself claims)."""
    root = tuning_root or (REPO_ROOT / "data" / "tuning")
    entries: "list[dict]" = []
    for domain, filename in _MANIFEST_TUNING_FILES:
        path = root / filename
        doc = json.loads(path.read_text(encoding="utf-8"))
        entries.append({
            "domain": domain,
            "version": int(doc["version"]),
            "sha256": content_sha256(path.read_bytes()),
        })
    return entries


def manifest_path(seed_root: "Path | None" = None) -> Path:
    root = seed_root or (REPO_ROOT / "data" / "seed")
    return root / "passive-tree" / "plan.v1.json"


def build_manifest(specs: "list[TreeSpec]", tuning: dict, seed_root: "Path | None" = None,
                   tuning_root: "Path | None" = None) -> "tuple[dict, dict[str, dict]]":
    """Builds the top-level manifest plus every per-tree plan it indexes (spec-tree-plan.md's
    Manifest schema table, task C2). Reads back each tree's already-committed plan first (same R3
    read-back `build_plan` already does per tree) so a re-emit of the manifest never re-mints a
    node id anywhere in the corpus.

    Returns `(manifest, {treeId: tree_plan})` — the caller decides whether to write either."""
    root = seed_root or (REPO_ROOT / "data" / "seed")
    trees_dir = root / "passive-tree" / "plan"

    if not specs:
        raise EmitError("build_manifest: at least one tree spec is required")

    tree_plans: "dict[str, dict]" = {}
    for spec in specs:
        existing = _read_existing_plan(trees_dir / f"{spec.tree_id}.v1.json")
        tree_plans[spec.tree_id] = build_plan(spec, tuning, existing_plan=existing, seed_root=root)

    roster = load_roster(root)
    vocab = load_property_vocabulary(TIER_COUNT, root)
    demon_families, families_pending = load_family_roster_or_pending(root)
    pending: "list[str]" = []
    if families_pending:
        pending.append("demonFamilies")

    ladder_cfg = tuning["tierLadder"]
    potency_cfg = tuning["potency"]
    ladder = TierLadder(tier_count=TIER_COUNT, req_scale_points=ladder_cfg["reqScalePoints"], b=1)
    tier_shares = tier_budget_milli(TIER_COUNT)

    # R-G2: build the index in canonical (spec) order first, THEN sort by (generationWave,
    # ordinal) — `ordinal` is the roster ordinal, used ONLY as the sort tie-break; it is not part
    # of the frozen `trees[]` row shape (spec-tree-plan.md's own Manifest schema table) and is
    # dropped again below, once it has done its one job.
    staged = []
    for spec in specs:
        plan = tree_plans[spec.tree_id]
        staged.append({
            "treeId": spec.tree_id,
            "treeSlug": f"{spec.category}-{spec.tree_id}",
            "file": f"plan/{spec.tree_id}.v1.json",
            "generationWave": plan["generationWave"],
            "gateState": spec.gate_state,
            "sha256": tree_content_hash(plan),
            "ordinal": spec.ordinal,
        })
    staged.sort(key=lambda e: (e["generationWave"], e["ordinal"]))
    invariants_mod.check_r_g2_wave_order(staged)
    trees_index = [{k: v for k, v in e.items() if k != "ordinal"} for e in staged]

    manifest: "dict" = {
        "schemaVersion": MANIFEST_SCHEMA_VERSION,
        "plannerVersion": PLANNER_VERSION,
        "_pending": pending,
        "roster": {
            "aptitudes": list(roster.aptitudes),
            "elements": list(roster.elements),
            "statuses": list(roster.statuses),
            "demonFamilies": list(demon_families),
            "counts": {**roster.counts, "demonFamilies": len(demon_families), "trees": len(specs)},
        },
        "ladder": {
            "tierCount": TIER_COUNT,
            "branches": ["off", "def"],
            "gateCurrency": "aptitudePoints",
            "reqScalePoints": ladder_cfg["reqScalePoints"],
            "req": list(ladder.req_by_tier),
            "tierBudgetMilli": list(tier_shares),
            "branchSplitMilli": tuning["budget"]["branchSplitMilli"],
            "pairingRule": PAIRING_RULE,
        },
        "potency": {
            "maxNodeShareMilli": potency_cfg["maxNodeShareMilli"],
            "minTerminalWidth": potency_cfg["minTerminalWidth"],
        },
        "propertyVocabulary": {axis: list(members) for axis, members in vocab.axes.items()},
        "archetypes": _build_archetypes_block(tuning),
        "trees": trees_index,
    }

    # planHash = sha256(canonical manifest minus `_provenance`, plus the sorted per-tree hashes).
    # Computed BEFORE `_provenance` and `planHash` itself are added to the dict — `_provenance` is
    # excluded per the frozen schema note, and `planHash` necessarily excludes itself too (a hash
    # cannot include its own value; both keys are simply absent from `manifest` at this point, so
    # neither can leak into its own input).
    manifest_bytes = canonical_json_bytes(manifest)
    per_tree_hashes = sorted(tree_content_hash(tree_plans[s.tree_id]) for s in specs)
    plan_hash_input = manifest_bytes + b"\n" + "\n".join(per_tree_hashes).encode("utf-8")
    plan_hash = content_sha256(plan_hash_input)

    manifest["_provenance"] = {
        "emittedUtc": datetime.now(timezone.utc).isoformat(),
        "inputs": _provenance_inputs(root),
        "tuning": _provenance_tuning(tuning_root),
    }
    manifest["planHash"] = plan_hash

    return manifest, tree_plans


def emit_manifest(specs: "list[TreeSpec]", tuning: dict, seed_root: "Path | None" = None,
                  tuning_root: "Path | None" = None) -> Path:
    """Writes the manifest and every per-tree file it indexes, all via `canonical_json_bytes`.
    Returns the manifest's path."""
    root = seed_root or (REPO_ROOT / "data" / "seed")
    manifest, tree_plans = build_manifest(specs, tuning, seed_root=root, tuning_root=tuning_root)
    trees_dir = root / "passive-tree" / "plan"
    trees_dir.mkdir(parents=True, exist_ok=True)
    for spec in specs:
        (trees_dir / f"{spec.tree_id}.v1.json").write_bytes(canonical_json_bytes(tree_plans[spec.tree_id]))
    out_path = manifest_path(root)
    out_path.write_bytes(canonical_json_bytes(manifest))
    return out_path


def check_manifest(specs: "list[TreeSpec]", tuning: dict, seed_root: "Path | None" = None,
                   tuning_root: "Path | None" = None) -> "list[str]":
    """Regenerates the manifest and every per-tree file it indexes in memory, and byte-diffs each
    against its committed counterpart. Returns a list of differing paths, each prefixed with the
    file it belongs to (`plan.v1.json$...` or `plan/<treeId>.v1.json$...`) so "the first differing
    path" (spec-tree-plan.md Commands) names both the file and the field.

    `_provenance` is excluded from the manifest diff for the same reason `planHash` excludes it:
    `emittedUtc` changes on every run by design, and diffing it would make every `--check` report
    drift regardless of content (Reproducibility: "`emittedUtc` is excluded from `planHash`, or
    every run is drift" — the same reasoning applies one level up, to `--check`'s own byte diff)."""
    root = seed_root or (REPO_ROOT / "data" / "seed")
    m_path = manifest_path(root)
    if not m_path.exists():
        raise EmitError(f"EXIT_CANNOT_RUN: no committed manifest at {m_path} to check against")
    committed_manifest = json.loads(m_path.read_text(encoding="utf-8"))

    trees_dir = root / "passive-tree" / "plan"
    committed_trees: "dict[str, dict]" = {}
    for spec in specs:
        path = trees_dir / f"{spec.tree_id}.v1.json"
        if not path.exists():
            raise EmitError(f"EXIT_CANNOT_RUN: no committed tree plan at {path} to check against")
        committed_trees[spec.tree_id] = json.loads(path.read_text(encoding="utf-8"))

    regenerated_manifest, regenerated_trees = build_manifest(specs, tuning, seed_root=root, tuning_root=tuning_root)

    diffs: "list[str]" = []
    for spec in specs:
        _diff_json(committed_trees[spec.tree_id], regenerated_trees[spec.tree_id],
                  f"plan/{spec.tree_id}.v1.json$", diffs)

    committed_for_diff = {k: v for k, v in committed_manifest.items() if k != "_provenance"}
    regenerated_for_diff = {k: v for k, v in regenerated_manifest.items() if k != "_provenance"}
    _diff_json(committed_for_diff, regenerated_for_diff, "plan.v1.json$", diffs)
    return diffs


def load_manifest_and_trees(path: Path) -> "tuple[dict, dict[str, dict]]":
    """Reads one manifest plus every per-tree file it names in `trees[]` (`--diff`'s own loader,
    task C2) — resolved relative to the manifest's own directory, matching `trees[].file`'s
    `plan/<treeId>.v1.json` relative form."""
    if not path.exists():
        raise EmitError(f"EXIT_CANNOT_RUN: no manifest at {path}")
    manifest = json.loads(path.read_text(encoding="utf-8"))
    trees: "dict[str, dict]" = {}
    for entry in manifest.get("trees", []):
        tree_path = path.parent / entry["file"]
        if not tree_path.exists():
            raise EmitError(
                f"EXIT_CANNOT_RUN: {path} names {entry['file']!r} in trees[] but {tree_path} does not exist")
        trees[entry["treeId"]] = json.loads(tree_path.read_text(encoding="utf-8"))
    return manifest, trees


def diff_manifests(path_a: Path, path_b: Path) -> "dict[str, list[str]]":
    """`--diff`'s report (spec-tree-plan.md Commands): budget deltas, archetype reassignments,
    quota-cell moves, and node ids added/removed/re-minted — "the one that matters under D24".

    Nodes are paired by SLOT (`branch`, `tier`, `indexInTier`) rather than by `id`/`nodeKey` —
    pairing by slot is what turns "same slot, different id" into a REMINT finding instead of a
    spurious add+remove pair, which is the actual failure D24 cares about."""
    manifest_a, trees_a = load_manifest_and_trees(path_a)
    manifest_b, trees_b = load_manifest_and_trees(path_b)

    budget_deltas: "list[str]" = []
    archetype_reassignments: "list[str]" = []
    quota_cell_moves: "list[str]" = []
    ids_added: "list[str]" = []
    ids_removed: "list[str]" = []
    ids_reminted: "list[str]" = []

    for tree_id in sorted(set(trees_a) | set(trees_b)):
        ta = trees_a.get(tree_id)
        tb = trees_b.get(tree_id)
        if ta is None:
            ids_added.extend(f"{tree_id}:{n['id']}" for n in tb["nodes"])
            continue
        if tb is None:
            ids_removed.extend(f"{tree_id}:{n['id']}" for n in ta["nodes"])
            continue

        if ta.get("archetype") != tb.get("archetype"):
            archetype_reassignments.append(f"{tree_id}: {ta.get('archetype')!r} -> {tb.get('archetype')!r}")

        slots_a = {(n["branch"], n["tier"], n["indexInTier"]): n for n in ta["nodes"]}
        slots_b = {(n["branch"], n["tier"], n["indexInTier"]): n for n in tb["nodes"]}
        for slot in sorted(set(slots_a) | set(slots_b)):
            na = slots_a.get(slot)
            nb = slots_b.get(slot)
            if na is None:
                ids_added.append(f"{tree_id}:{nb['id']}")
                continue
            if nb is None:
                ids_removed.append(f"{tree_id}:{na['id']}")
                continue
            if na["id"] != nb["id"] or na["nodeKey"] != nb["nodeKey"]:
                ids_reminted.append(f"{tree_id}:{slot}: {na['id']} -> {nb['id']}")
            if na.get("budgetPoints") != nb.get("budgetPoints"):
                budget_deltas.append(
                    f"{tree_id}:{na['id']}: budgetPoints {na.get('budgetPoints')} -> {nb.get('budgetPoints')}")
            qa, qb = na.get("quotaCell"), nb.get("quotaCell")
            if qa != qb:
                quota_cell_moves.append(f"{tree_id}:{na['id']}: quotaCell {qa!r} -> {qb!r}")

    return {
        "budgetDeltas": budget_deltas,
        "archetypeReassignments": archetype_reassignments,
        "quotaCellMoves": quota_cell_moves,
        "idsAdded": sorted(ids_added),
        "idsRemoved": sorted(ids_removed),
        "idsReminted": sorted(ids_reminted),
    }
