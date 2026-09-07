"""seedsmith.report.cli — `seedsmith check`, exit codes (spec-foundation §7.3).

Exit codes are a stable contract CI depends on: 0 clean, 1 findings at GAP, 2 could not run
(corpus unreadable, unknown adapter), 3 refused (planner refuses an unsatisfiable work order —
not reachable from `check`; W2).

Two review modes over the same run, not two contradictory truths:
- plain `check`: exit 1 if ANY GAP-severity finding exists, from any metric — "tell me everything
  currently wrong," for local dev. This is exactly what tasks/seedsmith-todo.md's S1/S2
  acceptance tests exercise.
- `--gate`: exit 1 only if a GAP comes from a metric with `gates=True` — the CI-safe mode, usable
  once a metric family has been calibrated and promoted (spec-metrics.md §4). Every metric ships
  `gates=False` for the whole of W1 (by design — new metrics are measure-only until calibrated),
  so `--gate` always exits clean for now. That is correct, not a bug: nothing has been promoted.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from ..adapters.registry import known_adapter_names, resolve_adapter
from ..corpus import Corpus, CorpusLoadError
from ..metrics import Ctx, MetricRegistry, Severity, run_all
from ..metrics.coverage import EmptyPartitionMetric
from ..metrics.linkage import ALL_LINKAGE_METRICS
from ..metrics.pairwise import PairwiseHole
from ..metrics.balance import LadderInversion, OutOfEnvelope
from ..metrics.cell_occupancy import CellOccupancy
from ..metrics.distribution import CellDeviation, Evenness, Inequality
from ..metrics.constraint import Constraint
from ..metrics.exemplar import ExemplarConformance
from ..metrics.dedup import SemanticDedup
from ..metrics.quality import FLAVOR_EXPECTED_KINDS, FlavourGeneric, FlavourMissing
from ..metrics.content_completeness import (
    CompletenessSpec, ContentFieldMissing, ContentFieldStale, ContentLanguageContamination,
    register_completeness,
)
from ..adapters.actions.description_backfill import ACTIONS_COMPLETENESS_SPEC
from ..metrics.corpus_coverage import BasisHistogramMetric, DumpCompletenessMetric
from ..metrics.demon_coverage import DemonUncoveredMetric
from ..metrics.demon_roster import ALL_DEMON_ROSTER_METRICS
from ..metrics.pipeline_health import ALL_PIPELINE_HEALTH_METRICS
from ..metrics.motif_sharing import MotifSharingMetric
from ..metrics.passive_tree import TreeEqualValueMetric, ALL_PASSIVE_TREE_METRICS
from ..numerics import BattleRulesetProgression, NumericsContext, TierBands
from ..budget import derive_all

EXIT_CLEAN = 0
EXIT_GAP = 1
EXIT_CANNOT_RUN = 2
EXIT_REFUSED = 3

_SEVERITY_ORDER = {Severity.GAP: 0, Severity.NOTE: 1, Severity.NOT_MEASURED: 2}


def build_registry() -> MetricRegistry:
    """Every metric that exists so far. S2-S8 each add their own metrics here — one line per
    metric, never a rewrite of this function."""
    registry = MetricRegistry()
    registry.register(EmptyPartitionMetric())
    for metric_cls in ALL_LINKAGE_METRICS:
        registry.register(metric_cls())
    registry.register(PairwiseHole())
    registry.register(LadderInversion())
    registry.register(OutOfEnvelope())
    registry.register(CellDeviation())
    registry.register(Evenness())
    registry.register(Inequality())
    registry.register(CellOccupancy())
    registry.register(Constraint())
    registry.register(ExemplarConformance())
    registry.register(SemanticDedup())
    registry.register(FlavourMissing())
    registry.register(FlavourGeneric())
    # Content/FieldMissing + Content/FieldStale (seedsmith-content-standard, content-completeness-
    # core, Task 3): a generalized registry a domain adopts via `register_completeness`, rather
    # than editing FLAVOR_EXPECTED_KINDS's own hardcoded frozenset for every new domain. Registered
    # unconditionally here (same as every other metric in this function) — reporting zero findings
    # when no domain has registered a spec yet is correct, not a bug (matches `gates=False`'s own
    # measure-only-until-adopted posture).
    #
    # `content-completeness-items` (Task 6): items is the FIRST real domain adoption — a real gap
    # found building this task: nothing outside `tests/test_content_completeness.py` ever called
    # `register_completeness`, so `Content/FieldMissing` reported nothing at all on a real `check`
    # run even though the registry/metric machinery was fully built in Phase 0. Registering here
    # (not inside `metrics/quality.py` at import time) means every real invocation of `check` gets
    # the items spec regardless of module import order, and `register_completeness`'s own
    # idempotency (content_completeness.py) makes it safe that this function runs more than once in
    # one process. `FlavourMissing` itself is left registered too, unchanged (Task 3's own
    # byte-identical guarantee) — this is an ADDITIVE second reporting path onto the same real data,
    # not a replacement.
    register_completeness(CompletenessSpec(
        domain="items", kinds=FLAVOR_EXPECTED_KINDS, field="flavor"))
    # `content-completeness-actions` (Task 8): actions had NOTHING before this task (no ledger, no
    # `_provenance`, no missing-field metric — `seedsmith-content-standard-ideal.md`'s own "Real
    # gap" finding). `ACTIONS_COMPLETENESS_SPEC` (`adapters/actions/description_backfill/
    # __init__.py`) names `description` — the AUTHORED flavour-text field this task added to
    # `action-seed` (`adapters/actions/kinds.py`'s own `ACTION_SEED_OPTIONAL`), distinct from the
    # already-existing `descriptionKey` (a minted, empty i18n key with nothing behind it yet).
    register_completeness(ACTIONS_COMPLETENESS_SPEC)
    registry.register(ContentFieldMissing())
    registry.register(ContentFieldStale())
    registry.register(ContentLanguageContamination())
    registry.register(DemonUncoveredMetric())
    registry.register(MotifSharingMetric())
    registry.register(DumpCompletenessMetric())
    registry.register(BasisHistogramMetric())
    for metric_cls in ALL_DEMON_ROSTER_METRICS:
        registry.register(metric_cls())
    for metric_cls in ALL_PIPELINE_HEALTH_METRICS:
        registry.register(metric_cls())
    registry.register(TreeEqualValueMetric())
    # H4 (spec-tree-language.md §7 gates 15-22): the eight PassiveTree/* corpus metrics, registered
    # exactly once, matching the ALL_LINKAGE_METRICS/ALL_DEMON_ROSTER_METRICS/ALL_PIPELINE_HEALTH_METRICS
    # loop pattern above. This is the wiring H4's own evidence named as deliberately left for a later
    # task: the metric classes existed with the correct `gates` attribute, but `--write`'s own registry
    # never carried them, so `assert_exactly_one_hard_gate` always found zero and refused every write.
    # DeepMechanismValueMetric/HiddenFileCountMetric (H5) are correctly NOT included here -- both need
    # externally-supplied data (CombatSim samples / seed roots) build_registry()'s generic construction
    # has no parameter for, matching H5's own stated precedent for keeping them standalone-registrable.
    for metric_cls in ALL_PASSIVE_TREE_METRICS:
        registry.register(metric_cls())
    return registry


def _build_numerics_context(adapter_name: str, adapter) -> "NumericsContext | None":
    """Only the `items` adapter has a `tier-bands.v{n}.json` to load (spec-numerics.md §3.1's
    path is item-corpus-specific); any other adapter runs without a numerics context, and
    numerics-dependent metrics correctly report NOT_MEASURED via their declared `needs`."""
    if adapter_name != "items":
        return None
    try:
        tuning = TierBands.load("latest")
    except FileNotFoundError:
        return None
    return NumericsContext(tuning=tuning, progression=BattleRulesetProgression.from_adapter(adapter))


def _print_human(findings, *, stream=None) -> None:
    # `stream` used to default to `sys.stdout` directly -- an early-binding bug (the same class
    # already fixed once this session in `generate_affixes.py`'s own `output_dir`/`id_prefix`
    # defaults): a default evaluated at function-DEFINITION time captures whatever `sys.stdout` WAS
    # when this module first imported, not whatever it is at call time, so
    # `contextlib.redirect_stdout` in a test (or any caller that temporarily swaps `sys.stdout`)
    # silently failed to capture anything printed here -- found 2026-09-07 the moment a real test
    # first asserted on this function's own captured output instead of only an exit code.
    if stream is None:
        stream = sys.stdout
    if not findings:
        print("no findings", file=stream)
        return
    for f in sorted(findings, key=lambda f: (_SEVERITY_ORDER[f.severity], f.metric, f.subject)):
        print(f"[{f.severity.value.upper()}] {f.metric} — {f.subject}: {f.message}", file=stream)
    counts: dict[Severity, int] = {}
    for f in findings:
        counts[f.severity] = counts.get(f.severity, 0) + 1
    summary = ", ".join(f"{v} {k.value}" for k, v in counts.items())
    print(f"\n{summary}", file=stream)


def _cmd_check_family(args: argparse.Namespace) -> int:
    """`seedsmith check --family <X> --gate` (spec-tree-language.md §Commands) — a family-scoped
    check that needs no `corpus_root`/`--adapter` at all, the same shape `_cmd_demons_metrics`
    already uses for `DemonRoster`. Only `PassiveTree` exists today (task H2); a later family adds
    its own branch here rather than a second command.

    Exit codes, reusing the shipped four (`cli.py`'s own docstring) rather than inventing new
    semantics: `EXIT_CANNOT_RUN` (2) when there is nothing to check at all (no committed plan);
    `EXIT_REFUSED` (3) when `--gate` is asked to trust a hard-gate contract that is not wired yet —
    §7.1's own rule is "exactly one gate is promoted to hard-fail first," and today's real registry
    has ZERO `PassiveTree/*` metrics at `gates=True` (`PassiveTree/UnresolvedCount` is task H4's,
    not yet built) — reporting a clean pass here would be exactly the lie H9 §6.4 rule 1 names:
    "an absent check is never a pass." Without `--gate`, every registered finding is reported and
    `EXIT_GAP` (1) fires on a real one, `EXIT_CLEAN` (0) otherwise — the same two codes `check`
    already uses for a corpus.
    """
    if args.family != "PassiveTree":
        print(f"seedsmith: check --family only supports 'PassiveTree' today; got {args.family!r}",
              file=sys.stderr)
        return EXIT_CANNOT_RUN

    from ..adapters.trees.nodegen import emit as nodegen_emit
    from ..adapters.trees.nodegen import plan_read as nodegen_plan_read
    from ..adapters.trees.nodegen import run as nodegen_run
    from ..adapters.trees.nodegen import verdict as tree_verdict
    from ..adapters.trees.plan import emit as plan_emit
    from ..adapters.trees.plan import tuning as plan_tuning
    from ..adapters.trees.plan.archetypes import SHIPPED_ARCHETYPES, TIER_COUNT
    from ..adapters.trees.targets import PassiveTreeTargetsError
    from ..adapters.trees.targets import load as load_tree_targets
    from ..metrics.passive_tree import HiddenFileCountMetric, PassiveTreePlanCtx

    seed_root = (Path(args.plan_root) if getattr(args, "plan_root", None)
                else plan_emit.REPO_ROOT / "data" / "seed")
    plan_dir = seed_root / "passive-tree" / "plan"
    plan_paths = sorted(plan_dir.glob("*.v1.json")) if plan_dir.exists() else []
    if not plan_paths:
        print(f"seedsmith: check --family PassiveTree: no committed plan under {plan_dir} — "
              f"nothing to check (run `trees plan --emit` first)", file=sys.stderr)
        return EXIT_CANNOT_RUN

    try:
        tuning_doc = plan_tuning.load()
    except plan_tuning.PassiveTreePlanTuningError as ex:
        print(f"seedsmith: {ex}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    plans = [json.loads(p.read_text(encoding="utf-8")) for p in plan_paths]

    # Real corpus-side wiring (closes the gap a same-day J1 investigation found: this ctx never
    # carried `targets`/`tree_plans`/`nodes_by_tree`/`outcomes_by_tree`, so `PassiveTree/
    # UnresolvedCount` — the ONE hard gate this family promotes — always reported NOT_MEASURED
    # here, never a real pass or fail, regardless of what the real committed corpus looked like).
    # Every input below is real, already-committed, local data — no model call, no corpus fixture:
    # `tree-language.ledger.json` (what was actually accepted) and `nodegen.plan_run` (the SAME
    # function `run_language_stage` itself uses to tell "already done" from "still needed" on a
    # resume) applied to each committed plan tells us, per tree, exactly which node ids never got
    # an accepted record. `plan_run` cannot distinguish "genuinely stuck after retries" from "never
    # attempted yet" — no run history survives past a live run (named, not fixed, in J9/J1's own
    # notes) — so every ledger-absent node is reported as `"unresolved"`, matching this gate's own
    # purpose: a node with no accepted record is a hole `tree-binder` has nothing to price, for
    # either reason, and `check --family` is a completion check, not a mid-run progress probe.
    try:
        tree_targets = load_tree_targets()
    except (PassiveTreeTargetsError, OSError):
        tree_targets = None
    ledger = nodegen_run.read_ledger(seed_root / "passive-tree" / "_runs" / "tree-language.ledger.json")
    tree_plans: "list[object]" = []
    nodes_by_tree: "dict[str, list]" = {}
    outcomes_by_tree: "dict[str, list]" = {}
    for plan_doc in plans:
        tree_id = plan_doc.get("treeId")
        if not tree_id:
            continue
        tree_plan = nodegen_plan_read.load_from_dict(plan_doc, source_label=f"plan:{tree_id}")
        tree_plans.append(tree_plan)
        run_plan = nodegen_run.plan_run(tree_plan, ledger=ledger)
        seed_doc = nodegen_emit.read_seed_document(tree_id, seed_root=seed_root)
        nodes_by_tree[tree_id] = list(seed_doc["nodes"]) if seed_doc else []
        outcomes = [{"nodeId": subject_id.split(":", 1)[1], "outcome": "accepted"}
                   for subject_id in run_plan.already_done]
        outcomes.extend({"nodeId": subject.node_id, "outcome": "unresolved"}
                        for subject in run_plan.subjects)
        outcomes_by_tree[tree_id] = outcomes

    registry = build_registry()
    # H5's own reason `build_registry()` never carries `HiddenFileCountMetric`/`DeepMechanismValueMetric`
    # (see that function's own comment) is real: BOTH need externally-supplied data no generic caller
    # has. `_cmd_check_family` is different -- it just computed a real `seed_root` above, exactly what
    # `HiddenFileCountMetric` needs and nothing `DeepMechanismValueMetric` (CombatSim samples) can use
    # here either way -- so THIS caller registers it locally, closing H5's own remaining acceptance gap
    # ("populated somewhere a real run reaches") without touching the shared registry's own documented
    # exclusion for every other caller.
    registry.register(HiddenFileCountMetric())
    passive_tree_ctx = PassiveTreePlanCtx(
        plans=plans, archetypes=SHIPPED_ARCHETYPES, tier_count=TIER_COUNT,
        unlock_first_points=tuning_doc["unlockCost"]["firstPoints"],
        unlock_step_points=tuning_doc["unlockCost"]["stepPoints"],
        reward_spread_max_ratio_milli=tuning_doc["archetype"]["rewardSpreadMaxRatioMilli"],
        min_terminal_width=tuning_doc["potency"]["minTerminalWidth"],
        targets=tree_targets, tree_plans=tuple(tree_plans),
        nodes_by_tree=nodes_by_tree, outcomes_by_tree=outcomes_by_tree,
        # H5's own real acceptance gap (spec-tree-review.md §7): `HiddenFileCountMetric` was fully
        # built and tested but had ZERO real call site, `tree_seed_roots` always defaulting to `()`
        # everywhere outside its own test. A single root here already covers every category's own
        # seed tree via `rglob` (species's `data/seed/passive-tree/species/`,
        # spec-species-tree.md §2.1 rule 2's own requirement, included for free the moment anything
        # is committed under it — no second root needed).
        tree_seed_roots=(seed_root / "passive-tree",))
    ctx = Ctx(corpus=Corpus(), adapter=resolve_adapter("stub"), passive_tree_plan=passive_tree_ctx)
    family_ids = [m.id for m in registry.all() if m.family == "PassiveTree"]
    findings = run_all(registry, ctx, metric_ids=family_ids)

    if args.gate:
        try:
            tree_verdict.assert_exactly_one_hard_gate(registry, "PassiveTree")
        except ValueError as ex:
            print(f"seedsmith: check --family PassiveTree --gate: refused — {ex}", file=sys.stderr)
            return EXIT_REFUSED
        gating_ids = {m.id for m in registry.all() if m.family == "PassiveTree" and m.gates}
        relevant = [f for f in findings if f.metric in gating_ids]
    else:
        relevant = findings

    _print_human(findings)
    return EXIT_GAP if any(f.severity is Severity.GAP for f in relevant) else EXIT_CLEAN


def cmd_check(args: argparse.Namespace) -> int:
    if getattr(args, "family", None):
        return _cmd_check_family(args)

    if not args.corpus_root:
        print("seedsmith: check needs either corpus_root or --family <name>", file=sys.stderr)
        return EXIT_CANNOT_RUN

    try:
        adapter = resolve_adapter(args.adapter)
    except KeyError:
        print(f"seedsmith: unknown adapter {args.adapter!r} "
              f"(known: {', '.join(known_adapter_names())})", file=sys.stderr)
        return EXIT_CANNOT_RUN

    if args.adapter == "dungeon":
        # `Corpus.load()` requires a top-level `kind`/`entries` wrapper (`corpus/model.py:183-186`)
        # -- dungeon's own real content is one bare object per file (`emit.py`'s own docstring), so
        # the generic loader silently sees zero entries for this adapter. `load_dungeon_corpus`
        # (`adapters/dungeon/completeness.py`) bridges that gap; `ensure_completeness_registered`
        # wires the `Content/FieldMissing`/`Content/LanguageContamination` check for dungeon events
        # into `content_completeness`'s own registry (`seedsmith-content-standard` Task 12).
        from ..adapters.dungeon.completeness import ensure_completeness_registered, load_dungeon_corpus
        ensure_completeness_registered()
        corpus = load_dungeon_corpus(Path(args.corpus_root))
    elif args.adapter == "demons":
        # `demon`/`commander-effect` kind files under this root already are real `{kind, entries}`
        # documents and load correctly via the generic path below -- but `species/**/*.json` is a
        # bare JSON ARRAY per file (`anchor/emit.py`'s own `render_family_file`), the same shape gap
        # dungeon has for ALL its kinds. `load_species_corpus` (`adapters/demons/completeness.py`)
        # ADDS the one kind the generic loader cannot see on top of what it already loads correctly,
        # rather than replacing the whole load the way dungeon's own bridge does (`seedsmith-
        # content-standard` Task 10).
        from ..adapters.demons.completeness import ensure_completeness_registered, load_species_corpus
        ensure_completeness_registered()
        try:
            corpus = Corpus.load(Path(args.corpus_root))
        except CorpusLoadError as e:
            print(f"seedsmith: could not load corpus: {e}", file=sys.stderr)
            return EXIT_CANNOT_RUN
        load_species_corpus(Path(args.corpus_root), into=corpus)
    else:
        try:
            corpus = Corpus.load(Path(args.corpus_root))
        except CorpusLoadError as e:
            print(f"seedsmith: could not load corpus: {e}", file=sys.stderr)
            return EXIT_CANNOT_RUN

    numerics_ctx = _build_numerics_context(args.adapter, adapter)
    budget_rows = derive_all(corpus, adapter) if args.adapter == "items" else None
    ctx = Ctx(corpus=corpus, adapter=adapter, numerics=numerics_ctx, budget=budget_rows)
    registry = build_registry()
    findings = run_all(registry, ctx, metric_ids=args.metric or None)

    if args.json:
        Path(args.json).write_text(
            json.dumps([f.to_dict() for f in findings], indent=2), encoding="utf-8")

    _print_human(findings)

    relevant = findings
    if args.gate:
        gating_ids = {m.id for m in registry.all() if m.gates}
        relevant = [f for f in findings if f.metric in gating_ids]
    return EXIT_GAP if any(f.severity is Severity.GAP for f in relevant) else EXIT_CLEAN


def _load_demon_anchors(anchors_root: Path) -> "list[dict] | None":
    """Reads the `_index.json` an `anchor-emit` tree publishes and loads every family file it
    names, deduplicated by file — the same O(1)-lookup structure `run-control` resumes from."""
    index_path = anchors_root / "_index.json"
    if not index_path.exists():
        return None
    index = json.loads(index_path.read_text(encoding="utf-8"))
    anchors: "list[dict]" = []
    for rel_path in sorted(set(index.values())):
        path = anchors_root / rel_path
        if path.exists():
            anchors.extend(json.loads(path.read_text(encoding="utf-8")))
    return anchors


def cmd_report(args: argparse.Namespace) -> int:
    """`seedsmith report [--gate] [--corpus DIR --adapter NAME] [--demon-dump DIR]` — runs the
    FULL registry (every metric family, item-corpus and demon-dump alike) in one pass. Each
    metric's own `needs` decides whether it runs against what was actually supplied; a metric
    whose need is absent reports NOT_MEASURED rather than being silently skipped (`run_all`'s own
    contract) — this is the single command T1.10/T2.12/T3.8's own metrics are meant to appear in,
    so a later phase adding a metric family never has to invent a second report command.

    At least one of `--corpus`/`--demon-dump` should normally be given; running with neither is
    legal (every metric reports NOT_MEASURED) but produces no real signal.
    """
    corpus = Corpus() if args.corpus is None else None
    if args.corpus is not None:
        try:
            corpus = Corpus.load(Path(args.corpus))
        except CorpusLoadError as e:
            print(f"seedsmith: could not load corpus: {e}", file=sys.stderr)
            return EXIT_CANNOT_RUN

    try:
        adapter = resolve_adapter(args.adapter)
    except KeyError:
        print(f"seedsmith: unknown adapter {args.adapter!r} "
              f"(known: {', '.join(known_adapter_names())})", file=sys.stderr)
        return EXIT_CANNOT_RUN

    demon_dump = None
    if args.demon_dump is not None:
        from ..adapters.demons.dump_ctx import load_demon_dump_ctx
        demon_dump = load_demon_dump_ctx(Path(args.demon_dump))
        if demon_dump is None:
            print(f"seedsmith: no readable corpus-dump tree at {args.demon_dump}", file=sys.stderr)
            return EXIT_CANNOT_RUN

    demon_anchors = None
    if getattr(args, "demon_anchors", None) is not None:
        demon_anchors = _load_demon_anchors(Path(args.demon_anchors))
        if demon_anchors is None:
            print(f"seedsmith: no readable anchor tree at {args.demon_anchors} "
                  f"(expected an _index.json)", file=sys.stderr)
            return EXIT_CANNOT_RUN

    numerics_ctx = _build_numerics_context(args.adapter, adapter) if args.corpus is not None else None
    budget_rows = derive_all(corpus, adapter) if args.corpus is not None and args.adapter == "items" else None
    ctx = Ctx(corpus=corpus, adapter=adapter, numerics=numerics_ctx, budget=budget_rows,
             demon_dump=demon_dump, demon_anchors=demon_anchors)

    registry = build_registry()
    findings = run_all(registry, ctx, metric_ids=args.metric or None)

    if args.json:
        Path(args.json).write_text(json.dumps([f.to_dict() for f in findings], indent=2), encoding="utf-8")
    _print_human(findings)

    relevant = findings
    if args.gate:
        gating_ids = {m.id for m in registry.all() if m.gates}
        relevant = [f for f in findings if f.metric in gating_ids]
    return EXIT_GAP if any(f.severity is Severity.GAP for f in relevant) else EXIT_CLEAN


def cmd_metrics(args: argparse.Namespace) -> int:
    registry = build_registry()
    if args.coverage:
        from ..metrics.appendix_a import coverage_report
        report = coverage_report(registry.all())
        for row, metric_ids in sorted(report["claimed"], key=lambda pair: pair[0].number):
            print(f"[CLAIMED]    #{row.number} {row.family}: {row.description} — "
                  f"{', '.join(metric_ids)}")
        for row in sorted(report["known_gap"], key=lambda r: r.number):
            print(f"[KNOWN GAP]  #{row.number} {row.family}: {row.description} "
                  f"(out of W1 scope)")
        for row in sorted(report["unclaimed"], key=lambda r: r.number):
            print(f"[UNCLAIMED]  #{row.number} {row.family}: {row.description}")
        unclaimed_count = len(report["unclaimed"])
        print(f"\n{len(report['claimed'])} claimed, {len(report['known_gap'])} known gap, "
              f"{unclaimed_count} unclaimed")
        return EXIT_GAP if unclaimed_count else EXIT_CLEAN

    for metric in sorted(registry.all(), key=lambda m: m.id):
        print(f"{metric.id} ({metric.family}, {metric.loop.value}, "
              f"gates={metric.gates})")
    return EXIT_CLEAN



def cmd_effects(args: argparse.Namespace) -> int:
    """`seedsmith effects generate --kind affix` (T7.1, `affix-authoring`, effect-pipeline module 9).

    Same defect class `cmd_demons`'s own docstring already names (D1.4): a real entrypoint reachable
    only as `python -m seedsmith.adapters.effects.affix.generate_affixes` is a documented interface
    that only works if you know the private module path — not an interface. Import deferred for the
    same reason `cmd_demons` defers its own: `effects generate` pulls in the workflow package, and
    `langgraph` is an optional extra a base `seedsmith check` install must not require.
    """
    if args.effects_command == "generate":
        if args.kind != "affix":
            print(f"unknown kind {args.kind!r}; only 'affix' has a generator today")
            return EXIT_CANNOT_RUN
        from ..adapters.effects.affix.generate_affixes import main as run
        passthrough: "list[str]" = []
        if args.only:
            passthrough += ["--only", args.only]
        if args.theme:
            passthrough += ["--theme", args.theme]
        if args.endpoint:
            passthrough += ["--endpoint", args.endpoint]
        if args.model:
            passthrough += ["--model", args.model]
        if args.count:
            passthrough += ["--count", str(args.count)]
        if args.dry_run:
            passthrough.append("--dry-run")
        if args.workers:
            passthrough += ["--workers", str(args.workers)]
        if args.species_id:
            passthrough += ["--species-id", args.species_id]
        return run(passthrough)

    print(f"unknown effects command {args.effects_command!r}")
    return EXIT_CANNOT_RUN


def cmd_items(args: argparse.Namespace) -> int:
    """`seedsmith items generate --kind set|charm --population build|species` (item module 13).

    ⚠ **No `items` subcommand existed** — `build_parser` registered `check`, `report`, `metrics`,
    `demons` and `effects` and nothing else, so every command the module-13 spec listed was a
    documented interface that did not exist. The same defect class `cmd_demons`'s own docstring
    records twice. Made true here rather than softened in the spec.

    ⛔ **`--dry-run` is the default, and that is deliberate.** A real run is ~1,800 model calls; a
    flag you must remember to pass to avoid spending them is a flag someone eventually forgets.
    `--write` is the explicit opt-in.

    ⭐ **`--write` now writes along either of two transports (module 13, `set-charm-live-endpoint`).**
    The generation graph is `workflow/graphs/item_set.py`; its `call` is injected. `--answers <file>`
    keeps the deterministic path from before — `setgen.answers.replay_caller` reads answers a model
    has already authored against briefs this command emitted (`--briefs-out`); CI and dry runs stay
    on this path. `--endpoint <url> [--model <name>]` is the new live path — `setgen.run.live_caller`
    bound to `pipeline.llm_caller.call_model`, the same real HTTP transport `effects generate` and
    `demons generate` already use. `--write` with neither flag still refuses with the reason, because
    a command that silently writes nothing is worse than one that says so.
    """
    if args.items_command == "validate":
        return _cmd_items_validate(args)
    if args.items_command == "combogen-migrate":
        return _cmd_items_combogen_migrate(args)
    if args.items_command != "generate":
        print(f"unknown items command {args.items_command!r}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    if args.kind == "combination":
        return _cmd_items_combination(args)

    from ..adapters.items.setgen import run as run_mod
    from ..adapters.items.setgen import themes as themes_mod
    from ..adapters.items.setgen import tuning as tuning_mod
    from ..adapters.items.setgen import vocab as vocab_mod
    from ..adapters.items.setgen.verdict import GATING_METRICS, missing_thresholds

    tuning = tuning_mod.load()
    vocabulary = vocab_mod.build(tuning)
    ledger = {} if args.ignore_ledger else None
    try:
        plan = run_mod.plan_run(kind=args.kind, population=args.population,
                                tuning=tuning, vocabulary=vocabulary, ledger=ledger)
    except ValueError as exc:
        print(f"seedsmith: {exc}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    planned_total = len(plan.subjects)
    if args.limit and args.limit > 0:
        plan.subjects = plan.subjects[:args.limit]

    coverage = themes_mod.coverage_report(themes_mod.load_species_themes())
    summary = {
        **plan.summary(),
        "kind": args.kind,
        "population": args.population,
        "plannedBeforeLimit": planned_total,
        "capabilityPicks": vocabulary.capability_count,
        "statPicks": vocabulary.stat_count,
        "themeCoverage": {"species": coverage.species, "themes": coverage.themes,
                          "uncovered": len(coverage.uncovered),
                          "orphaned": len(coverage.orphaned)},
        "gatingMetrics": sorted(GATING_METRICS),
        "gatesMissingAThreshold": missing_thresholds(tuning),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))

    if args.sample_brief and plan.subjects:
        print("\n--- sample brief ---")
        print(plan.subjects[0].brief)

    if args.briefs_out:
        target = Path(args.briefs_out)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps({
            "schemaVersion": 1, "kind": args.kind, "population": args.population,
            "promptVersion": _prompt_version(),
            "subjects": [{**s.to_dict(), "brief": s.brief} for s in plan.subjects],
        }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"\nwrote {len(plan.subjects)} brief(s) to {target}", file=sys.stderr)

    if args.write:
        return _cmd_items_write(args, plan=plan, tuning=tuning, vocabulary=vocabulary)
    return EXIT_CLEAN


def _prompt_version() -> str:
    from ..adapters.items.setgen.brief import PROMPT_VERSION
    return PROMPT_VERSION


def _cmd_items_write(args: argparse.Namespace, *, plan, tuning, vocabulary) -> int:
    """The `--write` half. Refuses loudly and specifically rather than writing an empty run.

    ⚠ **`--out-dir` has no default, and a path inside `data/seed/items/` is refused unless
    `--allow-production-tree` is passed.** Every items metric globs that tree recursively, so a
    sample written there moves finding counts other streams baseline against — the failure is
    silent and shows up as someone else's regression.

    ⭐ **Two transports, one refusal (module 13, `set-charm-live-endpoint`).** `--answers <file>` is
    the deterministic replay path, unchanged. `--endpoint <url>` is the live path — a real call
    through `pipeline.llm_caller.call_model`, the same transport `effects generate`/`demons generate`
    already use — and it makes `--answers` optional, not `--out-dir`: a write still needs somewhere
    to land. Only when NEITHER transport is named does this refuse, the same safety net as before.
    """
    import dataclasses

    from ..adapters.items.setgen import authored as authored_mod
    from ..adapters.items.setgen import answers as answers_mod
    from ..adapters.items.setgen import run as run_mod
    from ..adapters.items.setgen import seedfile as seedfile_mod
    from ..pipeline.llm_caller import load_config

    if not args.out_dir:
        print("seedsmith: --write is refused — no --out-dir given; a write needs somewhere to "
              "land.", file=sys.stderr)
        return EXIT_REFUSED
    if not args.answers and not args.endpoint:
        print("seedsmith: --write is refused — no transport. The generation graph "
              "(workflow/graphs/item_set.py) is wired to two: an authored-answer file (emit briefs "
              "with --briefs-out, have a model answer them, then pass --answers <file>), or a live "
              "model endpoint (--endpoint <url> [--model <name>]).",
              file=sys.stderr)
        return EXIT_REFUSED

    try:
        out_dir = seedfile_mod.resolve_out_dir(
            args.out_dir, allow_production_tree=args.allow_production_tree)
    except seedfile_mod.OutDirRefused as exc:
        print(f"seedsmith: {exc}", file=sys.stderr)
        return EXIT_REFUSED

    call = None
    if args.answers:
        try:
            answers = answers_mod.load_answers(Path(args.answers))
        except answers_mod.AnswerFileError as exc:
            print(f"seedsmith: {exc}", file=sys.stderr)
            return EXIT_REFUSED
        if answers.kind != args.kind or answers.population != args.population:
            print(f"seedsmith: the answer file is for --kind {answers.kind} --population "
                  f"{answers.population}; this run is {args.kind}/{args.population}",
                  file=sys.stderr)
            return EXIT_REFUSED
        effective_model = args.model
    else:
        # No answer file at all this time — `run_batch`'s own `answers` parameter is consulted
        # only to build the DEFAULT (replay) caller, which never happens here because `call` is
        # given explicitly. This empty stand-in satisfies the parameter's type without pretending
        # an answer file exists.
        answers = answers_mod.AnswerFile(kind=args.kind, population=args.population,
                                         prompt_version=_prompt_version(), by_subject={})
        # ⛔ Real bug, found 2026-09-08: this used to build `LlmCallerConfig(endpoint=..., model=...)`
        # directly, which NEVER called `load_config()` — every `.env`/`seedsmith.toml` override
        # (model, timeout, attempts, retry_delay, max_heal, max_tokens) was silently ignored on this,
        # the actual live-generation path, no matter what was set. `--model unrecorded` (the CLI's own
        # not-passed sentinel) fell back to `LlmCallerConfig`'s hardcoded dataclass default, not to
        # `.env`. Fixed: `load_config()` is now the base, and only `--endpoint`/`--model` (when the
        # operator actually passed them) override it — every other `.env`/toml-set field survives.
        base_config = load_config()
        effective_model = (args.model if args.model and args.model != "unrecorded"
                           else base_config.model)
        config = dataclasses.replace(base_config, endpoint=args.endpoint, model=effective_model)
        call = run_mod.live_caller(config)

    ledger_path = Path(args.ledger) if args.ledger else out_dir / "set-charm-gen.ledger.json"
    result = authored_mod.run_batch(
        plan=plan, answers=answers, tuning=tuning, vocabulary=vocabulary, out_dir=out_dir,
        kind=args.kind, population=args.population, authored_utc=args.authored_utc,
        model=effective_model, ledger_path=ledger_path, call=call)
    print("\n--- write report ---")
    print(json.dumps(result.to_dict(), ensure_ascii=False, indent=2))
    return EXIT_CLEAN if result.persisted and not any(
        o.outcome == "escalated" for o in result.outcomes) else EXIT_GAP


def _cmd_items_combination(args: argparse.Namespace) -> int:
    """`seedsmith items generate --kind combination --shape strain|splice` (item module 21).

    ⛔ **`--dry-run` is the default here too.** A real run is 102 model calls; the plan, the ids, the
    gem-supply precheck and the learnability report all run without one, which is what makes the run
    inspectable before a token is spent.

    ⚠ **`--population` is meaningless for a combination and is refused rather than ignored.** The
    grid is closed — 12 aptitudes x 3 archetypes and C(12,2) — so there is no species/build split to
    make, and silently accepting the flag would let a caller believe they had selected something.
    """
    from ..adapters.items.combogen import migrate as migrate_mod
    from ..adapters.items.combogen import run as run_mod
    from ..adapters.items.combogen import supply as supply_mod
    from ..adapters.items.combogen import tuning as tuning_mod

    if getattr(args, "population", None) not in (None, "species"):
        # "species" is the parser default, i.e. "not passed"; anything else was passed on purpose.
        print("seedsmith: --population does not apply to --kind combination — the grid is closed "
              "(12 aptitudes x 3 archetypes, and C(12,2)); use --shape strain|splice",
              file=sys.stderr)
        return EXIT_CANNOT_RUN

    tuning = tuning_mod.load()
    try:
        supply = supply_mod.build()
        plan = run_mod.plan_run(shape=args.shape, tuning=tuning, supply=supply)
    except (ValueError, supply_mod.SupplyRefused) as exc:
        print(f"seedsmith: {exc}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    planned_total = len(plan.subjects)
    if args.limit and args.limit > 0:
        import dataclasses
        plan = dataclasses.replace(plan, subjects=plan.subjects[:args.limit])

    legality = migrate_mod.legality_report(tuning, host_roles=plan.host_roles)
    summary = {
        **plan.summary(),
        "kind": "combination",
        "plannedBeforeLimit": planned_total,
        "ingredientCount": tuning.ingredient_count,
        "maxCombosPerActor": tuning.max_combos_per_actor,
        "attunedTierBonus": tuning.attuned_tier_bonus,
        "legacyRetirement": legality.to_dict(),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))

    if args.sample_brief and plan.subjects:
        print("\n--- sample brief ---")
        print(plan.subjects[0].brief)

    if args.briefs_out:
        from ..adapters.items.combogen.brief import PROMPT_VERSION as combo_prompt_version

        target = Path(args.briefs_out)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps({
            "schemaVersion": 1, "kind": "combination", "shape": args.shape,
            "promptVersion": combo_prompt_version,
            "subjects": [{**s.to_dict(), "brief": s.brief} for s in plan.subjects],
        }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"\nwrote {len(plan.subjects)} brief(s) to {target}", file=sys.stderr)

    if args.write:
        return _cmd_items_combination_write(args, plan=plan, tuning=tuning)
    return EXIT_CLEAN


def _cmd_items_combination_write(args: argparse.Namespace, *, plan, tuning) -> int:
    """The `--write` half for `--kind combination` (`combination-write-unblock`, item module 21).

    ⛔ **Investigation finding, not an assumption.** The refusal this replaces used to read *"the
    generation graph... is not wired, and the kind rename touches a FROZEN registry"* as one
    blocker. They are two SEPARATE facts and only the first was real:

    1. **The graph really was unwired** — `combogen/grid.py`, `catalogue.py`, `schema.py`,
       `supply.py`, `emit.py`, `brief.py` and `run.py` were already complete (`run.plan_run`
       produced real subjects and briefs before this module touched anything); what did not exist
       anywhere was `workflow/graphs/item_combination.py` — the file that connects a subject's
       brief to an LLM caller — or a batch driver to run subjects through it
       (`combogen/authored.py`, mirroring `setgen.authored.run_batch`). Both now exist. This
       mirrors module 13's own history almost exactly (`workflow/graphs/item_set.py`'s docstring
       names the identical defect it fixed).
    2. **The frozen-registry claim does not hold up.** `naming.v1.json`'s `idNamespaces.socketWords`
       (`registryVersion 4`, `frozen: true`) allocates the WAVE-1 AUTHORING FLEET's tracking-id
       template (`sockword.{seq:03}`) — a collision-avoidance scheme for ~125 PARALLEL human/LLM
       partitions. The new `combination` generator is a single deterministic pipeline (one grid,
       zero parallel partitions, `run.plan_run` already asserts its 102 ids are unique by
       construction) that mints `combo.strain-*`/`combo.splice-*` directly from the grid cell —
       it never draws from `sockword.{seq:03}` at all. The same precedent already exists,
       unregistered, for other deterministic post-wave-1 batches: `build-themes.v1.json`'s 36
       Strain themes and module 16's C# `ResonanceGenerator` output both have NO `naming.v1.json`
       idNamespaces entry either, because that registry exists to coordinate parallel AUTHORING
       AGENTS, and neither of those is one. **A workaround exists and this module takes it: the
       frozen registry is not bumped, and no `decisions.md` entry is added for a change that is
       not made** — per the spec's own instruction, the pre-approval to bump was conditional on no
       workaround existing, and one does.

    **A real, separate, and NOT worked around gap:** `tools/ItemSeedValidator/Registries/
    KindCatalog.cs` has no `combination` entry (only the legacy `socket-word`), so
    `NamespaceAllocation` cannot allocate a prefix for it and the C# validator will not recognize a
    `combination`-kind seed file today. Closing that needs a C# change, and this module's own
    boundary is read-only C# — so it is reported here, not silently patched around and not hidden.
    Content this command writes is verified against the checks this program's OWN Python tooling
    owns (`items validate --deps`, schema/`audit_schema` conformance, `dependency_validator`), not
    against `tools/ItemSeedValidator`.
    """
    from ..adapters.items.combogen import authored as authored_mod
    from ..adapters.items.setgen import answers as answers_mod
    from ..adapters.items.setgen import seedfile as seedfile_mod

    if not args.answers or not args.out_dir:
        print("seedsmith: --write is refused — no transport. The generation graph "
              "(workflow/graphs/item_combination.py) is wired, but its only transport today is an "
              "authored-answer file: emit briefs with --briefs-out, have a model answer them, then "
              "pass --answers <file> --out-dir <dir>. There is no live-endpoint path here yet "
              "(that is set-charm-live-endpoint's own scope, not this one's).", file=sys.stderr)
        return EXIT_REFUSED
    try:
        out_dir = seedfile_mod.resolve_out_dir(
            args.out_dir, allow_production_tree=args.allow_production_tree)
        answers = answers_mod.load_answers(Path(args.answers))
    except (seedfile_mod.OutDirRefused, answers_mod.AnswerFileError) as exc:
        print(f"seedsmith: {exc}", file=sys.stderr)
        return EXIT_REFUSED
    if answers.kind != "combination":
        print(f"seedsmith: the answer file is for --kind {answers.kind!r}; this run is "
              f"'combination'", file=sys.stderr)
        return EXIT_REFUSED

    ledger_path = Path(args.ledger) if args.ledger else None
    result = authored_mod.run_batch(
        plan=plan, answers=answers, tuning=tuning, out_dir=out_dir,
        authored_utc=args.authored_utc, model=args.model, ledger_path=ledger_path)
    print("\n--- write report ---")
    print(json.dumps(result.to_dict(), ensure_ascii=False, indent=2))
    return EXIT_CLEAN if result.persisted and not any(
        o.outcome == "escalated" for o in result.outcomes) else EXIT_GAP


def _cmd_items_validate(args: argparse.Namespace) -> int:
    """`seedsmith items validate --deps` (acceptance 3a, `combination-write-unblock`).

    Scoped to `combination` for now — the module this command was built for. Runs the pre-flight
    `dependency_validator` report over the REAL corpus, BEFORE any subject is planned: every
    `hostRole` a run could request must have >=1 real base type whose socket ceiling reaches the
    ingredient count, and every `ingredients` family must have >=1 real gem. This is the REPORTED
    form of a guarantee `combogen.schema.combination_schema` already enforces structurally (it
    refuses to build a schema for an empty universe); reported so "resolves, but only barely" stays
    visible (`dependency_validator.py`'s own reason for a count, never a bare bool).
    """
    if not args.deps:
        print("seedsmith: `items validate` needs --deps today — no other check is wired to this "
              "command yet.", file=sys.stderr)
        return EXIT_CANNOT_RUN

    from ..adapters.items.combogen import deps as deps_mod
    from ..adapters.items.combogen import tuning as tuning_mod

    tuning = tuning_mod.load()
    report = deps_mod.preflight(tuning)
    print(json.dumps({"kind": "combination", **report.to_dict()}, ensure_ascii=False, indent=2))
    return EXIT_REFUSED if report.refused else EXIT_CLEAN


def _cmd_items_combogen_migrate(args: argparse.Namespace) -> int:
    """`seedsmith items combogen-migrate --dry-run` (acceptance 4, `combination-write-unblock`).

    ⛔ **`--dry-run` is the only mode this command has.** `combogen.migrate`'s own ✅ ruling
    ("regenerate, do not retain", 2026-09-04) retires `sockwords.json` only once real combination
    content exists to replace it — this module ships a representative sample, not the full 102, so
    actually deleting the 25 legacy entries here would leave the replacement incomplete. This
    command therefore only ever reports `migrate.py`'s own plan (the legality report, proving 0 of
    the 25 legacy entries are legal combinations today, and the migration-sites existence check) —
    it does not execute the retirement.
    """
    if not args.dry_run:
        print("seedsmith: `items combogen-migrate` only supports --dry-run today — the actual "
              "retirement (deleting sockwords.json) is gated on full 102-entry coverage existing to "
              "replace it, which this module's own representative sample does not provide.",
              file=sys.stderr)
        return EXIT_REFUSED

    from ..adapters.items.combogen import migrate as migrate_mod
    from ..adapters.items.combogen import tuning as tuning_mod

    tuning = tuning_mod.load()
    from ..adapters.items.combogen import supply as supply_mod
    from ..adapters.items.combogen import run as run_mod

    supply = supply_mod.build()
    plan = run_mod.plan_run(shape="strain", tuning=tuning, supply=supply)
    legality = migrate_mod.legality_report(tuning, host_roles=plan.host_roles)
    missing = migrate_mod.missing_sites()
    summary = {
        "dryRun": True,
        "legacyRetirement": legality.to_dict(),
        "migrationSitesMissing": missing,
        "planStillHolds": not missing,
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return EXIT_CLEAN if not missing else EXIT_GAP


def cmd_demons(args: argparse.Namespace) -> int:
    """`seedsmith demons <motifs|generate>` — the demon generation entrypoints.

    ⛔ Why this exists. Two of the audit's own `Verify` lines named commands that did not exist:
    `python -m seedsmith demons motifs` (G1.3) and
    `python -m seedsmith demons generate --kind commander-effect` (G4.3). The real entrypoints were
    reachable only as `python -m seedsmith.adapters.demons.<module>`, so both Verify lines failed
    when actually executed during the 2026-09-01 final-proof pass.

    This is the same defect D1.4 already caught once ("the real CLI — `report` from the spec's own
    example doesn't exist"). There it was fixed by correcting the command; here the claim is made
    true instead, matching P6's own precedent of "making the claim true rather than softening it" —
    a documented interface that only works if you know the private module path is not an interface.

    Imports are deferred: `demons generate` pulls in the workflow package, and `langgraph` is an
    optional extra. A top-level import would make `seedsmith check` fail on a base install.
    """
    if args.demon_command == "families":
        from ..adapters.demons.generate_families import run as run_families
        passthrough: "list[str]" = []
        for flag in ("dry_run", "write", "ack"):
            if getattr(args, flag, False):
                passthrough.append(
                    {"dry_run": "--dry-run", "write": "--write",
                     "ack": "--i-have-read-the-append-only-note"}[flag])
        return run_families(passthrough)

    if args.demon_command == "motifs":
        import json as _json

        from ..adapters.demons.generate_motifs import regenerate
        print(_json.dumps(regenerate(), ensure_ascii=False, indent=2))
        return EXIT_CLEAN

    if args.demon_command == "power-parse":
        return _cmd_demons_power_parse(args)

    if args.demon_command == "threat-band":
        return _cmd_demons_threat_band(args)

    if args.demon_command == "contract":
        return _cmd_demons_contract(args)

    if args.demon_command == "preflight":
        return _cmd_demons_preflight(args)

    if args.demon_command == "permute":
        return _cmd_demons_permute(args)

    if args.demon_command == "metrics":
        return _cmd_demons_metrics(args)

    if args.demon_command == "run":
        return _cmd_demons_run(args)

    if args.demon_command == "diff-legacy":
        return _cmd_demons_diff_legacy(args)

    if args.kind == "anchor":
        return _cmd_demons_generate_anchor(args)

    from ..adapters.demons.generate_commander_effects import main as run

    if args.kind != "commander-effect":
        print(f"unknown kind {args.kind!r}; only 'commander-effect' has a generator today")
        return EXIT_CANNOT_RUN
    passthrough: "list[str]" = []
    for flag in ("only", "endpoint", "model"):
        value = getattr(args, flag, None)
        if value:
            passthrough += [f"--{flag}", str(value)]
    for flag in ("dry_run", "stale", "force"):
        if getattr(args, flag, False):
            passthrough.append("--" + flag.replace("_", "-"))
    if args.workers:
        passthrough += ["--workers", str(args.workers)]
    return run(passthrough)


def _cmd_demons_power_parse(args: argparse.Namespace) -> int:
    """`seedsmith demons power-parse --dump <dir> [--report]` (demon-seed module 3,
    spec-power-parse.md). Zero model calls: reads the committed `corpus-dump` tree
    (`almanac/plant.json` + `almanac/zombie.json`) and runs the deterministic parse over it.
    """
    from ..adapters.demons.power.parse import basis_histogram, disagreements, parse_power_seed

    dump_dir = Path(args.dump)
    plant_path = dump_dir / "almanac" / "plant.json"
    zombie_path = dump_dir / "almanac" / "zombie.json"
    if not plant_path.exists() or not zombie_path.exists():
        print(f"seedsmith: no corpus-dump tree at {dump_dir} "
              f"(expected almanac/plant.json and almanac/zombie.json)", file=sys.stderr)
        return EXIT_CANNOT_RUN

    rows = json.loads(plant_path.read_text(encoding="utf-8")) + json.loads(zombie_path.read_text(encoding="utf-8"))
    seeds = [
        parse_power_seed(
            side=r["side"], type_id=r["typeId"], stats_observed=r["statsObserved"],
            hp=r["hp"], attack=r["attack"], flavor_text=r["flavorInfo"])
        for r in rows
    ]

    hist = basis_histogram(seeds)
    total = len(seeds)
    print(f"power-parse: {total} species — "
          f"observed={hist['observed']} stated={hist['stated']} "
          f"inferred={hist['inferred']} blocked={hist['blocked']}")

    if args.report:
        for basis, count in hist.items():
            pct = 100 * count / total if total else 0.0
            print(f"  {basis}: {count} ({pct:.1f}%)")
        dis = disagreements(seeds)
        tempo_stated = sum(1 for s in seeds if s.interval_ms is not None)
        print(f"  attackTempo stated (interval on the damage line): {tempo_stated} "
              f"({100 * tempo_stated / total:.1f}%)" if total else "  attackTempo stated: 0")
        print(f"  disagreements: {len(dis)}")
        for d in dis:
            print(f"    {d.side}:{d.type_id} toughness={d.toughness} (text={d.text_toughness}) "
                  f"damage={d.damage} (text={d.text_damage})")

    return EXIT_CLEAN


def _cmd_demons_threat_band(args: argparse.Namespace) -> int:
    """`seedsmith demons threat-band --dump <dir> [--histogram]` (demon-seed module 4,
    spec-threat-band.md). Zero model calls: power-parse's score, looked up in the tuning table.
    """
    from ..adapters.demons.power.bands import ThreatTuning, classify, histogram
    from ..adapters.demons.power.parse import parse_power_seed

    dump_dir = Path(args.dump)
    plant_path = dump_dir / "almanac" / "plant.json"
    zombie_path = dump_dir / "almanac" / "zombie.json"
    if not plant_path.exists() or not zombie_path.exists():
        print(f"seedsmith: no corpus-dump tree at {dump_dir} "
              f"(expected almanac/plant.json and almanac/zombie.json)", file=sys.stderr)
        return EXIT_CANNOT_RUN

    rows = json.loads(plant_path.read_text(encoding="utf-8")) + json.loads(zombie_path.read_text(encoding="utf-8"))
    seeds = [
        parse_power_seed(
            side=r["side"], type_id=r["typeId"], stats_observed=r["statsObserved"],
            hp=r["hp"], attack=r["attack"], flavor_text=r["flavorInfo"])
        for r in rows
    ]

    tuning = ThreatTuning.load(1)
    rungs: "list[int]" = []
    unscored = 0
    for s in seeds:
        result = classify(s, tuning)
        if result is None:
            unscored += 1
            continue
        rungs.append(result.rung)

    print(f"threat-band: {len(seeds)} species — {len(rungs)} scored (observed/stated), "
          f"{unscored} inferred/blocked (no score at this layer)")

    if args.histogram:
        h = histogram(rungs, tuning)
        for t in tuning.thresholds:
            marker = " ⚠️ EMPTY" if h[t.id] == 0 else ""
            print(f"  rung {t.rung:2d} {t.id:<10s}: {h[t.id]:4d}{marker}")

    return EXIT_CLEAN


def _cmd_demons_contract(args: argparse.Namespace) -> int:
    """`seedsmith demons contract --print|--audit` (demon-seed module 2, spec-anchor-contract.md).
    No model calls: prints or numerically audits the resolved anchor schema.
    """
    from ..adapters.demons.anchor.audit import numeric_audit
    from ..adapters.demons.anchor.schema import build_anchor_schema

    schema = build_anchor_schema()

    if args.print_schema:
        print(json.dumps(schema, indent=2, ensure_ascii=False))
        return EXIT_CLEAN

    # --audit (also the default when neither flag is passed)
    defects = numeric_audit(schema)
    if not defects:
        print(f"contract --audit: clean — {len(schema['properties'])} fields, "
              f"0 numeric-smuggling findings")
        return EXIT_CLEAN
    for d in defects:
        print(f"[FINDING] {d}")
    print(f"\n{len(defects)} numeric-smuggling finding(s)")
    return EXIT_GAP


def cmd_trees(args: argparse.Namespace) -> int:
    """`seedsmith trees <plan|generate>` — passive-tree entrypoints (task B1 plan, task H2
    generate, spec-tree-plan.md / spec-tree-language.md)."""
    if args.trees_command == "plan":
        return _cmd_trees_plan(args)
    if args.trees_command == "generate":
        return _cmd_trees_generate(args)
    if args.trees_command == "review":
        return _cmd_trees_review(args)

    print(f"unknown trees command {args.trees_command!r}")
    return EXIT_CANNOT_RUN


def _every_planned_tree_id(seed_root: "Path | None" = None) -> "list[str]":
    """Every tree id with a committed plan under `<seed_root>/passive-tree/plan/*.v1.json` —
    `seed_root` is the SAME parameter `plan_read.load`'s own `seed_root` takes (defaults to
    `data/seed`), so a caller passes one root to both this function and `plan_read.load`."""
    from ..adapters.trees.plan import emit as plan_emit

    root = seed_root or (plan_emit.REPO_ROOT / "data" / "seed")
    plan_dir = root / "passive-tree" / "plan"
    if not plan_dir.exists():
        return []
    return sorted(p.name[: -len(".v1.json")] for p in plan_dir.glob("*.v1.json"))


def _cmd_trees_generate(args: argparse.Namespace) -> int:
    """`seedsmith trees generate --tree <id>|--all [--dry-run|--write] [--sample-brief]`
    (task H2/H3, spec-tree-language.md §Commands).

    ⛔ **`--dry-run` is the default, and that is deliberate** — the SAME rule `items generate` and
    `items generate --kind combination` already state for their own real-call costs: a real run is
    thousands of model calls, so a flag you must remember to pass to AVOID spending them is a flag
    someone eventually forgets. The dry run prints `gatingMetrics`/`gatesMissingAThreshold`
    (§7 gate 5) and the call-count arithmetic BEFORE a single call is made — `--sample-brief` prints
    one rendered brief on top, still zero calls.

    **H3 closes the gap this docstring used to name.** Every planned tree's own `QuotaCell`s are
    now resolved for real (`quota.quota_for_plan`), and every node's `permittedIds`
    (`quota.permitted_ids_for_cell`) plus its branch-tagged affix subset
    (`vocab.AffixVocabulary.permitted_for_branch`) are real, non-empty subsets — `resolvedSubjects`
    in the summary below proves it, and `--sample-brief` renders an ACTUAL brief against a real
    node's real permitted subset rather than the placeholder string this command used to print.

    `--write` is STILL refused, but for a different, still-true reason today: §7.1's own rule is
    "exactly one gate is promoted to hard-fail first," and the real metric registry has ZERO
    `PassiveTree/*` metrics at `gates=True` (`PassiveTree/UnresolvedCount` is task H4's, not built
    yet) — the exact same contract `check --family PassiveTree --gate` already refuses on
    (`_cmd_check_family`'s own docstring). Spending thousands of real calls with nowhere for a
    systematic failure to land would be the defect §7.1 exists to prevent.
    """
    from ..adapters.trees.nodegen import brief as brief_mod
    from ..adapters.trees.nodegen import emit as emit_mod
    from ..adapters.trees.nodegen import plan_read
    from ..adapters.trees.nodegen import quota as quota_mod
    from ..adapters.trees.nodegen import run as run_mod
    from ..adapters.trees.nodegen import tuning as tuning_mod
    from ..adapters.trees.nodegen import vocab as vocab_mod
    from ..adapters.trees.nodegen import verdict as tree_verdict
    from ..adapters.trees.nodegen.verdict import GATING_METRICS, missing_thresholds

    if not args.tree and not args.all:
        print("seedsmith: --tree <id> or --all is required", file=sys.stderr)
        return EXIT_CANNOT_RUN

    seed_root = Path(args.plan_root) if args.plan_root else None
    tree_ids = [args.tree] if args.tree else _every_planned_tree_id(seed_root)
    if not tree_ids:
        print(f"seedsmith: no committed plan found — run `trees plan --emit` first", file=sys.stderr)
        return EXIT_CANNOT_RUN

    try:
        targets = tuning_mod.load()
    except tuning_mod.PassiveTreeTargetsError as ex:
        print(f"seedsmith: {ex}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    try:
        affix_vocab = vocab_mod.build()
    except vocab_mod.AffixVocabularyError as ex:
        print(f"seedsmith: {ex}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    total_subjects = 0
    resolved_subjects = 0
    sample_brief_text = None
    plans_and_cells: "dict[str, tuple[Any, Any]]" = {}
    for tree_id in tree_ids:
        try:
            plan = plan_read.load(tree_id, seed_root)
        except plan_read.TreePlanReadError as ex:
            print(f"seedsmith: {ex}", file=sys.stderr)
            return EXIT_CANNOT_RUN
        run_plan = run_mod.plan_run(plan)
        total_subjects += len(run_plan.subjects) + len(run_plan.already_done)

        category = plan.raw.get("category")
        try:
            cells = quota_mod.quota_for_plan(
                plan, targets, category=str(category),
                forced_element=plan.raw.get("forcedElement"),
                forced_status=plan.raw.get("forcedStatus"))
        except (ValueError, KeyError) as ex:
            print(f"seedsmith: {tree_id}: quota resolution refused — {ex}", file=sys.stderr)
            return EXIT_CANNOT_RUN
        plans_and_cells[tree_id] = (plan, cells)

        for node in plan.nodes:
            cell = cells[node.node_id]
            permitted_ids = quota_mod.permitted_ids_for_cell(cell, plan.property_vocabulary)
            permitted_affixes = affix_vocab.permitted_for_branch(node.branch)
            if permitted_affixes and any(permitted_ids.values()):
                resolved_subjects += 1
            if args.sample_brief and sample_brief_text is None:
                sample_brief_text = brief_mod.render_brief(
                    node_id=node.node_id, sample_index=0, tree_display_name=tree_id,
                    tree_reading=tree_id, branch=node.branch, tier=node.tier,
                    node_class=node.node_class, motifs=(), anti_motifs=(),
                    permitted_affixes=permitted_affixes,
                    permitted_properties=sorted(plan.property_vocabulary))

    # §6.1's own cost arithmetic (D29's corpus table), COMPUTED from `total_subjects` — never a
    # hardcoded literal. At the generic corpus's real size (1,560 subjects: 39 trees x 40 nodes)
    # `run.calls_for` returns exactly the spec's own 4,680; `test_nodegen_generate.py` proves that
    # arithmetic directly at 1,560 without depending on the real corpus being fully committed yet.
    summary = {
        "trees": tree_ids,
        "totalSubjects": total_subjects,
        "resolvedSubjects": resolved_subjects,
        **run_mod.calls_for(total_subjects),
        "gatingMetrics": sorted(GATING_METRICS),
        "gatesMissingAThreshold": missing_thresholds(targets),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))

    if args.sample_brief and sample_brief_text:
        print("\n--- sample brief ---")
        print(sample_brief_text)

    if args.write:
        registry = build_registry()
        try:
            tree_verdict.assert_exactly_one_hard_gate(registry, "PassiveTree")
        except ValueError as ex:
            print(f"seedsmith: --write is refused — {ex}. The plan, the resolved quota cells and "
                  f"the call count above are real; the model call is not, because a systematic "
                  f"failure would have nowhere to land (§7.1).", file=sys.stderr)
            return EXIT_REFUSED

        # Real generation, one tree at a time — `run_language_stage` (task H2) is already the
        # complete, idempotent runner (ledger read, per-node gates 2/6/7/9/10/11/12/13, ledger
        # write, seed-document emit, gate-23 RunReport); this is that function's first real
        # production caller. `inputs_for` supplies exactly the fields `--sample-brief` above
        # already resolves per node (`permitted_affixes`/`permitted_properties`, via the SAME
        # `cells`/`affix_vocab` this function already computed during the dry-run pass, never
        # re-derived) plus the two fields a single rendered brief did not need: `tree_display_name`/
        # `tree_reading` default to the tree's own id (matching the dry-run's own
        # `--sample-brief` call above) since a shared/mechanical tree like `might` carries no
        # authored display name yet (tree-language's own naming pass, I10, has not run); `motifs`/
        # `anti_motifs`/`anti_motif_tags` are empty for the same reason a primary/mechanical tree's
        # own `--sample-brief` output already showed "(none named)" — this corpus category has no
        # motif system, unlike the demon corpus's themed content.
        overall_outcomes: "dict[str, int]" = {}
        per_tree_reports: "list[dict]" = []
        for tree_id in tree_ids:
            plan, cells = plans_and_cells[tree_id]

            def inputs_for(subject, _plan=plan):
                # Matches the dry-run's own --sample-brief call above exactly: permitted_properties
                # is the sorted set of property AXIS NAMES (e.g. "aptitude", "element") a node's
                # exclusion may reference, never a per-cell-narrowed id list — `permitted_ids_for_cell`
                # (used above only for the dry run's own resolved_subjects boolean check) is not an
                # input `NodeGenerationInputs`/`render_brief` takes.
                permitted_affixes = affix_vocab.permitted_for_branch(subject.branch)
                return run_mod.NodeGenerationInputs(
                    tree_display_name=tree_id, tree_reading=tree_id,
                    motifs=(), anti_motifs=(), anti_motif_tags=(),
                    permitted_affixes=permitted_affixes,
                    permitted_properties=sorted(_plan.property_vocabulary),
                    property_vocabulary=_plan.property_vocabulary,
                    affix_vocab=affix_vocab,
                )

            # 2026-09-06 real-call finding (`might`, a real generation run): two DIFFERENT accepted
            # nodes collided on `nameKey` ("Deep Rooting" generated twice, independently, for two
            # different tiers) -- `build_seed_document`'s own `assert_no_duplicate_name_keys` refused
            # exactly as designed ("refused, never renamed out from under the model's answer" --
            # NodeKeyRefused's own message), but nothing here caught it, so a real, well-defined,
            # already-reported-elsewhere content defect crashed the whole CLI with a raw traceback
            # instead of a clean report. The ledger itself is NOT at risk: `run_language_stage` writes
            # it before ever building the seed document, so every already-accepted node from this run
            # (and prior runs) stays safely recorded regardless of this refusal -- confirmed by reading
            # the function's own body, not assumed. Caught here, once, at the one place that already
            # aggregates per-tree reports, so `--all` still reports every OTHER tree that succeeded.
            try:
                result = run_mod.run_language_stage(
                    plan, inputs_for,
                    ledger_path=Path(args.ledger_path) if getattr(args, "ledger_path", "") else None,
                    seed_root=seed_root,
                    unresolved_max_share_permille=targets.unresolved_count_max_share_permille,
                    max_workers=max(1, getattr(args, "workers", 1)))
            except emit_mod.NodeKeyRefused as ex:
                per_tree_reports.append({
                    "tree": tree_id, "seedPath": None,
                    "nameKeyRefused": str(ex),
                })
                overall_outcomes["nameKeyRefused"] = overall_outcomes.get("nameKeyRefused", 0) + 1
                continue
            for outcome in result.outcomes:
                overall_outcomes[outcome.outcome] = overall_outcomes.get(outcome.outcome, 0) + 1
            # `detail` is a real, model-authored reason (§H9's own diagnostic history: "current tree
            # is empty", "'might' is a property/stat, not an effect id" were both only ever found by
            # reading this field) — printed here, per non-accepted subject, so a future run's real
            # blocks/escalations are diagnosable from the CLI's own output rather than requiring a
            # second real-call reproduction just to see why.
            per_tree_reports.append({
                "tree": tree_id,
                "seedPath": str(result.seed_path) if result.seed_path else None,
                "outcomeCounts": {
                    o: sum(1 for x in result.outcomes if x.outcome == o)
                    for o in ("accepted", "blocked", "unresolved", "escalated")
                },
                "nonAcceptedDetail": [
                    {"subject": o.subject_id, "outcome": o.outcome, "detail": o.detail}
                    for o in result.outcomes if o.outcome != "accepted"
                ],
                "runReport": result.report.to_dict(),
            })

        print(json.dumps({"perTree": per_tree_reports, "outcomeTotals": overall_outcomes},
                          ensure_ascii=False, indent=2))
        any_refused = any("nameKeyRefused" in r for r in per_tree_reports)
        any_fail = any(r.get("runReport", {}).get("verdict") == "FAIL" for r in per_tree_reports)
        return EXIT_GAP if (any_fail or any_refused) else EXIT_CLEAN
    return EXIT_CLEAN


def _cmd_trees_review(args: argparse.Namespace) -> int:
    """`seedsmith trees review --census --lot <lot>` (task H7, spec-tree-review.md §5.5).

    **Scope, stated honestly.** This lands exactly the gate the todo's H7 acceptance names — the
    `sheetRead` refusal in front of a census — and nothing past it. The sheet itself, the three
    sampling tiers (§3.2) and the acceptance ladder (§6.2/§6.3) are `adapters.trees.review.sample`
    / `.fingerprint` / `.verdict`, none of which exist yet (H8 and later, per the spec's own
    "Project structure" list) — `--census` here proves the gate holds and stops; it does not run
    a census, because there is no census machinery to hand off to yet. That is a wiring gap this
    task names rather than a scope this task quietly narrowed.
    """
    from ..adapters.trees.review import census_gate

    if not args.census:
        print("seedsmith: trees review currently only implements --census (§5.5's gate) — the "
              "sampling tiers (§3.2) and the acceptance ladder (§6.2/§6.3) are unbuilt (H8+)",
              file=sys.stderr)
        return EXIT_CANNOT_RUN

    lot = args.lot
    sheet_dir = Path(args.sheet_dir) if args.sheet_dir else census_gate.DEFAULT_SHEET_DIR
    review_dir = Path(args.review_dir) if args.review_dir else census_gate.DEFAULT_REVIEW_DIR

    try:
        row = census_gate.assert_may_start_census(sheet_dir, review_dir, lot)
    except census_gate.SheetNotRendered as ex:
        print(f"seedsmith: {ex}", file=sys.stderr)
        return EXIT_CANNOT_RUN
    except census_gate.CensusRefused as ex:
        print(f"EXIT_REFUSED: {ex}", file=sys.stderr)
        return EXIT_REFUSED

    print(json.dumps({
        "lot": lot,
        "sheetRead": {"sheetRevision": row.sheet_revision, "by": row.by, "utc": row.utc},
        "note": "gate cleared — the census tiers themselves are not wired yet (H8+)",
    }, ensure_ascii=False, indent=2))
    return EXIT_CLEAN


def _run_tree_equal_value(plans: "list[dict]", tuning_doc: dict) -> "str | None":
    """`PassiveTree/TreeEqualValue` (spec-tree-plan.md §3.2, task C1) — runs at BOTH `--emit` and
    `--check`, before anything else that could spend a model call, over the emitted plan(s) alone.
    Returns `None` on a clean pass, or the refusal message naming the tree/branch/tier/archetype
    it found. Never clamps — the caller turns a message into `EXIT_REFUSED`."""
    from ..adapters.trees.plan import invariants as plan_invariants
    from ..adapters.trees.plan.archetypes import TIER_COUNT, SHIPPED_ARCHETYPES

    try:
        plan_invariants.check_tree_equal_value(
            plans, SHIPPED_ARCHETYPES, TIER_COUNT,
            tuning_doc["unlockCost"]["firstPoints"], tuning_doc["unlockCost"]["stepPoints"],
            tuning_doc["archetype"]["rewardSpreadMaxRatioMilli"],
            tuning_doc["potency"]["minTerminalWidth"],
        )
    except plan_invariants.PlanInvariantError as ex:
        return str(ex)
    return None


def _cmd_trees_plan(args: argparse.Namespace) -> int:
    """`seedsmith trees plan --emit|--check [--tree <id>] [--manifest] [--generate] [--diff a b]`
    (spec-tree-plan.md Commands). No model calls, no RNG — every value is a pure function of
    tuning + roster mirrors + (for --check) the already-committed plan."""
    from ..adapters.trees.plan import emit as plan_emit
    from ..adapters.trees.plan import invariants as plan_invariants
    from ..adapters.trees.plan import tuning as plan_tuning

    if args.diff:
        path_a, path_b = Path(args.diff[0]), Path(args.diff[1])
        try:
            report = plan_emit.diff_manifests(path_a, path_b)
        except plan_emit.EmitError as ex:
            print(f"EXIT_CANNOT_RUN: {ex}")
            return EXIT_CANNOT_RUN
        any_diff = any(report.values())
        for label, entries in report.items():
            if entries:
                print(f"{label}: {len(entries)}")
                for entry in entries:
                    print("  " + entry)
        if not any_diff:
            print(f"{path_a} and {path_b} are equivalent — no budget deltas, archetype "
                 f"reassignments, quota-cell moves, or id changes.")
            return EXIT_CLEAN
        return EXIT_GAP

    try:
        tuning_doc = plan_tuning.load()
    except plan_tuning.PassiveTreePlanTuningError as ex:
        print(f"EXIT_CANNOT_RUN: {ex}")
        return EXIT_CANNOT_RUN

    # H9 (2026-09-06): generalized past "might" alone — `primary_tree_spec` is a mechanical
    # extension of `might_tree_spec`'s own logic (proven byte-identical for "might" itself, see its
    # own docstring), reading each of the 12 primary trees' `ordinal`/`gate_quantity` from the SAME
    # roster/gate-evidence files `might_tree_spec` always read, never a new content decision. "might"
    # keeps calling the original named function verbatim — zero behavior change for the one tree
    # every existing test already exercises.
    if args.tree == "might":
        try:
            spec = plan_emit.might_tree_spec()
        except Exception as ex:  # gates.GateEvidenceError, etc. — never resolved silently
            print(f"EXIT_CANNOT_RUN: {ex}")
            return EXIT_CANNOT_RUN
    else:
        from ..adapters.trees.plan.vocabulary import load_roster
        roster = load_roster()
        aptitude_by_lower = {a.lower(): a for a in roster.aptitudes}
        aptitude_id = aptitude_by_lower.get(args.tree)
        # J1 (spec-tree-plan.md §7 table): elemental_tree_spec/status_tree_spec, the two remaining
        # mechanical extensions of primary_tree_spec's own generalization pattern. Checked after
        # aptitudes (the pre-existing, most-exercised path stays first and unchanged) — roster.elements
        # and roster.statuses are already lowercase (generated from ElementTypeId/
        # StatusCategoryRegistry), so no case-folding lookup is needed the way aptitudes' PascalCase
        # roster required.
        if aptitude_id is not None:
            try:
                spec = plan_emit.primary_tree_spec(aptitude_id)
            except Exception as ex:  # ValueError, gates.GateEvidenceError, etc. — never resolved silently
                print(f"EXIT_CANNOT_RUN: {ex}")
                return EXIT_CANNOT_RUN
        elif args.tree in roster.elements:
            try:
                spec = plan_emit.elemental_tree_spec(args.tree)
            except Exception as ex:
                print(f"EXIT_CANNOT_RUN: {ex}")
                return EXIT_CANNOT_RUN
        elif args.tree in roster.statuses:
            try:
                spec = plan_emit.status_tree_spec(args.tree)
            except Exception as ex:
                print(f"EXIT_CANNOT_RUN: {ex}")
                return EXIT_CANNOT_RUN
        else:
            print(f"seedsmith: {args.tree!r} is not one of the {len(roster.aptitudes)} primary trees "
                 f"{sorted(aptitude_by_lower)!r}, the {len(roster.elements)} elemental trees "
                 f"{sorted(roster.elements)!r}, or the {len(roster.statuses)} status trees "
                 f"{sorted(roster.statuses)!r}", file=sys.stderr)
            return EXIT_CANNOT_RUN

    if args.generate:
        try:
            plan_invariants.check_r_g1_generation_allowed(spec.tree_id, spec.gate_state, spec.gate_quantity)
        except plan_invariants.PlanInvariantError as ex:
            print(f"EXIT_REFUSED: {ex}")
            return EXIT_REFUSED
        print(f"{spec.tree_id}: gateState='carrier' — R-G1 allows stage-2 generation. "
             f"tree-language (H1) is not built yet, so nothing is generated here beyond the gate check.")
        return EXIT_CLEAN

    if args.manifest:
        specs = [spec]
        if args.check:
            try:
                diffs = plan_emit.check_manifest(specs, tuning_doc)
            except plan_emit.EmitError as ex:
                print(f"EXIT_CANNOT_RUN: {ex}")
                return EXIT_CANNOT_RUN
            if not diffs:
                print(f"{plan_emit.manifest_path()} is byte-identical to a fresh regeneration.")
                return EXIT_CLEAN
            print(f"{len(diffs)} difference(s):")
            for d in diffs:
                print("  " + d)
            return EXIT_GAP

        try:
            path = plan_emit.emit_manifest(specs, tuning_doc)
        except (plan_emit.EmitError, plan_invariants.PlanInvariantError) as ex:
            print(f"EXIT_REFUSED: {ex}")
            return EXIT_REFUSED
        print(f"wrote {path}")
        return EXIT_CLEAN

    if args.check:
        try:
            diffs = plan_emit.check(spec, tuning_doc)
        except plan_emit.EmitError as ex:
            print(f"EXIT_CANNOT_RUN: {ex}")
            return EXIT_CANNOT_RUN
        refusal = _run_tree_equal_value([plan_emit.build_plan(spec, tuning_doc)], tuning_doc)
        if refusal is not None:
            print(f"EXIT_REFUSED: PassiveTree/TreeEqualValue: {refusal}")
            return EXIT_REFUSED
        if not diffs:
            print(f"{plan_emit.plan_path(spec.tree_id)} is byte-identical to a fresh regeneration.")
            return EXIT_CLEAN
        print(f"{len(diffs)} difference(s):")
        for d in diffs:
            print("  " + d)
        return EXIT_GAP

    try:
        built = plan_emit.build_plan(spec, tuning_doc)
    except (plan_emit.EmitError, plan_invariants.PlanInvariantError) as ex:
        print(f"EXIT_REFUSED: {ex}")
        return EXIT_REFUSED

    refusal = _run_tree_equal_value([built], tuning_doc)
    if refusal is not None:
        print(f"EXIT_REFUSED: PassiveTree/TreeEqualValue: {refusal}")
        return EXIT_REFUSED

    try:
        path = plan_emit.emit(spec, tuning_doc)
    except (plan_emit.EmitError, plan_invariants.PlanInvariantError) as ex:
        print(f"EXIT_REFUSED: {ex}")
        return EXIT_REFUSED
    print(f"wrote {path}")
    return EXIT_CLEAN


def _parse_set_pairs(pairs: "list[str]") -> "dict[str, float]":
    """`channelWeight.<id>=<value>` / `baseShare=<value>` -> the override map `TierBands.adjust`
    already consumes (spec-numerics.md §3.2's own worked example). Values are plain ratio
    multipliers (`1.0` == 1000‰), never per-mille integers — the same grammar the shipped
    `tier-bands.v1.json` `_meta` line names."""
    overrides: "dict[str, float]" = {}
    for pair in pairs:
        key, sep, raw = pair.partition("=")
        if not sep:
            raise ValueError(f"--set expects KEY=VALUE, got {pair!r}")
        key = key.strip()
        if key != "baseShare" and not key.startswith("channelWeight."):
            raise ValueError(
                f"--set key must be 'baseShare' or 'channelWeight.<id>', got {key!r}")
        overrides[key] = float(raw)
    return overrides


def _cmd_numerics_rebalance(args: argparse.Namespace) -> int:
    """`seedsmith numerics rebalance --set channelWeight.<id>=<value> [--publish]` — the command
    `data/seed/items/_tuning/tier-bands.v1.json`'s own `_meta.rebalance` line has named since the
    file shipped, and which did not exist until now (the `numerics` package was library-only).

    Dry by default: it prints what would move and exits without touching disk, matching
    spec-numerics.md §3.2's "nothing until publish". `--publish` writes `tier-bands.v{n+1}.json`
    and leaves the old version in place for revert, exactly as that `_meta` line promises.
    """
    from ..numerics import TierBands, tier_bands_io

    pairs = list(args.set_pairs or [])
    if args.set_file:
        for line in Path(args.set_file).read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#"):
                pairs.append(line)

    try:
        overrides = _parse_set_pairs(pairs)
    except ValueError as ex:
        print(f"EXIT_CANNOT_RUN: {ex}")
        return EXIT_CANNOT_RUN

    if not overrides:
        print("EXIT_CANNOT_RUN: no --set/--set-file overrides given — refusing a no-op publish")
        return EXIT_CANNOT_RUN

    before = TierBands.load("latest")
    after = before.adjust(overrides)

    added = sorted(set(after.channel_weight_permille) - set(before.channel_weight_permille))
    changed = sorted(
        k for k in before.channel_weight_permille
        if after.channel_weight_permille[k] != before.channel_weight_permille[k])

    print(f"tier-bands v{before.version}: {len(before.channel_weight_permille)} channel weights, "
          f"baseShare {before.base_share_permille}‰")
    print(f"  + {len(added)} added, ~{len(changed)} changed, "
          f"= {len(before.channel_weight_permille) - len(changed)} unchanged")
    if after.base_share_permille != before.base_share_permille:
        print(f"  baseShare {before.base_share_permille}‰ -> {after.base_share_permille}‰")
    for key in changed:
        print(f"  ~ {key}: {before.channel_weight_permille[key]}‰ -> "
              f"{after.channel_weight_permille[key]}‰")
    for key in added:
        print(f"  + {key}: {after.channel_weight_permille[key]}‰")
    print(f"total after: {len(after.channel_weight_permille)} channel weights")

    if not args.publish:
        print("dry run — nothing written (pass --publish to write the next version)")
        return EXIT_CLEAN

    published = TierBands(version=before.version + 1,
                          base_share_permille=after.base_share_permille,
                          channel_weight_permille=after.channel_weight_permille,
                          op_weight_permille=after.op_weight_permille)
    path = tier_bands_io.save(published, meta=tier_bands_io.read_meta("latest"))
    print(f"wrote {path}")
    return EXIT_CLEAN


def cmd_numerics(args: argparse.Namespace) -> int:
    if args.numerics_command == "rebalance":
        return _cmd_numerics_rebalance(args)
    print(f"unknown numerics command {args.numerics_command!r}")
    return EXIT_CANNOT_RUN


def cmd_structures(args: argparse.Namespace) -> int:
    """`seedsmith structures <contract>` — base-defense structure corpus entrypoints. Mirrors
    `cmd_demons`'s own dispatch shape exactly (module 23+, spec-structure-schema.md)."""
    if args.structures_command == "contract":
        return _cmd_structures_contract(args)

    print(f"unknown structures command {args.structures_command!r}")
    return EXIT_CANNOT_RUN


def _cmd_structures_contract(args: argparse.Namespace) -> int:
    """`seedsmith structures contract --print|--audit` (base-defense module 23,
    spec-structure-schema.md). No model calls: prints or numerically audits the resolved structure
    anchor schema — a line-for-line copy of `_cmd_demons_contract` pointed at
    `adapters.structures.anchor.{schema,audit}` instead of `adapters.demons.anchor.{schema,audit}`.
    """
    from ..adapters.structures.anchor.audit import numeric_audit
    from ..adapters.structures.anchor.schema import build_structure_anchor_schema

    schema = build_structure_anchor_schema()

    if args.print_schema:
        print(json.dumps(schema, indent=2, ensure_ascii=False))
        return EXIT_CLEAN

    # --audit (also the default when neither flag is passed)
    defects = numeric_audit(schema)
    if not defects:
        print(f"contract --audit: clean — {len(schema['properties'])} fields, "
              f"0 numeric-smuggling findings")
        return EXIT_CLEAN
    for d in defects:
        print(f"[FINDING] {d}")
    print(f"\n{len(defects)} numeric-smuggling finding(s)")
    return EXIT_GAP


def _cmd_demons_preflight(args: argparse.Namespace) -> int:
    """`seedsmith demons preflight [--json] [--skip-model]` (demon-seed module 5,
    spec-dump-preflight.md). Refuses to start a run unless every prerequisite is present.
    """
    from ..adapters.demons.preflight import run_preflight, write_preflight_record

    report = run_preflight(skip_model=args.skip_model)

    if args.json:
        print(json.dumps({
            "fullPass": report.full_pass,
            "dumpHash": report.dump_hash,
            "checks": [
                {"id": c.id, "name": c.name, "ok": c.ok, "observed": c.observed,
                 "expected": c.expected, "action": c.action, "fixCommand": c.fix_command}
                for c in report.checks
            ],
        }, indent=2))
    else:
        for c in report.checks:
            status = "OK" if c.ok else c.action.upper()
            print(f"[{status:6s}] check {c.id} {c.name}: observed={c.observed!r} expected={c.expected!r}")
            if not c.ok:
                print(f"           fix: {c.fix_command}")
        print(f"\n{'PASS' if report.full_pass else 'NOT READY'} — "
              f"{len(report.refusals)} refusal(s), {len(report.asks)} thing(s) to ask about")

    if report.full_pass:
        write_preflight_record(report, skip_model=args.skip_model)

    return EXIT_CLEAN if report.full_pass else EXIT_GAP


def _cmd_demons_permute(args: argparse.Namespace) -> int:
    """`seedsmith demons permute --species <id> --field <name>` (demon-seed module 6,
    spec-option-permutation.md) — shows the three deterministic orders a species/field pair would
    see, so a reviewer can see the shuffle without instrumenting a real pipeline call."""
    from ..adapters.demons.anchor.permute import order_for
    from ..adapters.demons.anchor.schema import build_anchor_schema

    schema = build_anchor_schema()
    prop = schema["properties"].get(args.field)
    if prop is None or "enum" not in prop:
        print(f"seedsmith: {args.field!r} is not an enum field in the anchor schema "
              f"(known enum fields: {sorted(k for k, v in schema['properties'].items() if 'enum' in v)})",
              file=sys.stderr)
        return EXIT_CANNOT_RUN

    options = [v for v in prop["enum"] if v != "none"]
    print(f"permute: species={args.species!r} field={args.field!r} ({len(options)} options)")
    for i in range(3):
        print(f"  sample {i}: {order_for(args.species, args.field, i, options)}")
    return EXIT_CLEAN


def _cmd_demons_generate_anchor(args: argparse.Namespace) -> int:
    """`seedsmith demons generate --kind anchor --pipeline <id> --species <id> [--dry-run]`
    (demon-seed module 7, spec-classify-pipelines.md). `--dry-run` renders every prompt without
    calling — the cheapest way to review a description change across the roster before spending
    hours on a real run. `--all` here is refused on purpose: a real multi-hour run needs the
    pause/resume/cancel state machine, which is `demons run start --all` (module 9, run-control),
    not this single-shot command.
    """
    from ..adapters.demons.anchor.prompts import PIPELINES, SpeciesLore, threat_audit_spec_for_basis
    from ..adapters.demons.dump_ctx import load_demon_dump_ctx

    if args.all:
        print("seedsmith: this command has no run-control (pause/resume/checkpoint) — "
              "use `seedsmith demons run start --all` instead, or --species with --dry-run "
              "to review one species at a time here", file=sys.stderr)
        return EXIT_CANNOT_RUN

    dump_dir = Path(args.dump) if args.dump else Path("../../data/seed/demons/_dump")
    demon_dump = load_demon_dump_ctx(dump_dir)
    if demon_dump is None:
        print(f"seedsmith: no readable corpus-dump tree at {dump_dir}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    manifest_rows = json.loads((dump_dir / "almanac" / "plant.json").read_text(encoding="utf-8")) + \
                    json.loads((dump_dir / "almanac" / "zombie.json").read_text(encoding="utf-8"))
    by_species = {r["typeName"] or f"{r['side']}-{r['typeId']}": r for r in manifest_rows}

    def lore_for(row: dict) -> SpeciesLore:
        return SpeciesLore(
            species_id=row["typeName"] or f"{row['side']}-{row['typeId']}", side=row["side"],
            display_name=row["displayName"], flavor_info=row["flavorInfo"],
            flavor_introduce=row["flavorIntroduce"], enrichment=row.get("enrichment"))

    seed_by_species = {s.side + ":" + str(s.type_id): s for s in demon_dump.seeds}

    def basis_for(row: dict) -> str:
        s = seed_by_species.get(row["side"] + ":" + str(row["typeId"]))
        return s.basis if s else "blocked"

    if args.dry_run:
        rows = [by_species[args.species]] if args.species else manifest_rows
        rendered = 0
        for row in rows:
            lore = lore_for(row)
            basis = basis_for(row)
            for pid, spec in PIPELINES.items():
                if pid == "threat-audit":
                    spec = threat_audit_spec_for_basis(basis)
                spec.build_brief(lore, {"order": [], "elementPrimary": "fire",
                                        "aptitudePrimary": "Might", "rungId": "nuisance", "rungOrdinal": 1})
                rendered += 1
        print(f"generate --dry-run: rendered {rendered} prompts across {len(rows)} species x "
              f"{len(PIPELINES)} pipelines — zero model calls made")
        return EXIT_CLEAN

    if not args.pipeline or not args.species:
        print("seedsmith: --pipeline and --species are both required for a real (non-dry-run) call",
              file=sys.stderr)
        return EXIT_CANNOT_RUN
    if args.species not in by_species:
        print(f"seedsmith: species {args.species!r} not found in {dump_dir}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    from ..workflow.graphs.demon_anchor import build_pipeline_graph, state_for_pipeline

    row = by_species[args.species]
    lore = lore_for(row)
    basis = basis_for(row)
    graph = build_pipeline_graph(args.pipeline, basis=basis)
    state = state_for_pipeline(args.pipeline, lore, basis=basis)
    result = graph.invoke(state)
    print(json.dumps({"species": args.species, "pipeline": args.pipeline,
                      "outcome": result.get("outcome"), "draft": result.get("draft")},
                     indent=2, ensure_ascii=False))
    return EXIT_CLEAN if result.get("outcome") == "persisted" else EXIT_GAP


def _selector_from_args(args: argparse.Namespace) -> "dict":
    """One of the eight `run-control` selector shapes (spec-run-control.md §4), chosen by which
    flag the caller actually passed. `--all` and the `start`/`rerun` defaults both resolve to
    `{"kind": "all"}` when nothing more specific is given — `start` already skips
    already-emitted species on its own, so "all" is the right default rather than a refusal.

    `--pipeline` is two DIFFERENT things depending on what else is set (demon-corpus-self-heal B1,
    2026-09-04, found live: `rerun --pipeline kit-shape --species Peashooter,...` silently did a
    FULL 8-pipeline reclassification instead of the intended kit-shape-only smoke test, because
    `--species` won the if-elif chain and `--pipeline`'s own value was discarded entirely). When no
    OTHER selecting flag is given, `--pipeline` picks WHICH species (every classified one). When a
    species-selecting flag IS also given, `--pipeline` instead narrows EXECUTION scope for those
    selected species — attached as an extra `pipeline` key `_run_loop` reads regardless of `kind`,
    never silently dropped.
    """
    if args.species:
        selector = {"kind": "species", "species": [s.strip() for s in args.species.split(",") if s.strip()]}
    elif args.side:
        selector = {"kind": "side", "side": args.side}
    elif args.family:
        selector = {"kind": "family", "family": args.family}
    elif args.pipeline:
        return {"kind": "pipeline", "pipeline": args.pipeline}  # --pipeline alone: selects AND scopes
    elif args.basis:
        selector = {"kind": "basis", "basis": args.basis}
    elif args.unresolved:
        selector = {"kind": "unresolved"}
    elif args.stale:
        selector = {"kind": "stale"}
    else:
        selector = {"kind": "all"}

    if args.pipeline:
        selector["pipeline"] = args.pipeline
    return selector


def _cmd_demons_run(args: argparse.Namespace) -> int:
    """`seedsmith demons run <start|pause|resume|cancel|rerun|status|overwrite-all> [selector]`
    (demon-seed module 9, spec-run-control.md). Ties the pure `machine`/`record`/`selectors`
    modules to the real classification loop via `adapters.demons.run.runner` — every refusal
    (`RunRefused`) is printed and turned into a non-zero exit, never a silent no-op.
    """
    from ..adapters.demons.run import runner as run_module

    paths = run_module.RunPaths(
        dump_dir=Path(args.dump) if args.dump else run_module.DEFAULT_DUMP_DIR,
        anchors_dir=Path(args.anchors) if args.anchors else run_module.DEFAULT_ANCHORS_DIR)

    def progress(species_id: str, done: int, total: int) -> None:
        print(f"  [{done}/{total}] {species_id}")

    workers = max(1, args.workers)

    try:
        if args.run_verb == "start":
            record = run_module.start(_selector_from_args(args), paths=paths, progress=progress,
                                      workers=workers)
        elif args.run_verb == "resume":
            record = run_module.resume(paths=paths, progress=progress, workers=workers)
        elif args.run_verb == "pause":
            run_module.request_pause(paths=paths)
            print("pause requested — the in-flight species finishes, then the run stops")
            return EXIT_CLEAN
        elif args.run_verb == "cancel":
            record = run_module.cancel(paths=paths)
        elif args.run_verb == "rerun":
            record = run_module.rerun(_selector_from_args(args), paths=paths, progress=progress,
                                      workers=workers)
        elif args.run_verb == "overwrite-all":
            if not args.confirm:
                dump_hash = run_module._compute_dump_hash(paths.dump_dir)
                from ..adapters.demons.run.record import overwrite_all_token
                print(f"seedsmith: overwrite-all needs --confirm <token>; "
                      f"the token for the current dump is {overwrite_all_token(dump_hash)}", file=sys.stderr)
                return EXIT_CANNOT_RUN
            record = run_module.overwrite_all(args.confirm, paths=paths, progress=progress,
                                              workers=workers)
        elif args.run_verb == "fix-unresolved":
            fixed = run_module.fix_unresolved(paths=paths, dry_run=args.dry_run)
            verb = "would fix" if args.dry_run else "fixed"
            if args.json:
                print(json.dumps({"dryRun": args.dry_run, "fixed": fixed}, indent=2))
            else:
                print(f"{len(fixed)} field-fixes {verb} (threatBand, rarity, aptitudePrimary — "
                      f"each has a deterministic fallback now; element has none and stays "
                      f"unresolved)")
                for f in fixed:
                    print(f"  {f['speciesId']:24} {f['before']:12} -> {f['after']}")
            return EXIT_CLEAN
        elif args.run_verb == "fix-secondary-from-fusion":
            recipes_path = Path(args.fusion_recipes) if args.fusion_recipes else run_module.DEFAULT_FUSION_RECIPES_PATH
            fixed = run_module.fix_secondary_from_fusion_lineage(
                paths=paths, recipes_path=recipes_path, dry_run=args.dry_run)
            verb = "would fix" if args.dry_run else "fixed"
            if args.json:
                print(json.dumps({"dryRun": args.dry_run, "fixed": fixed}, indent=2))
            else:
                print(f"{len(fixed)} elementSecondary fix(es) {verb} from fusion-recipe lineage "
                      f"(one fusion parent's own real element supplied the signal; species with "
                      f"no clean single-candidate signal are left unchanged)")
                for f in fixed:
                    print(f"  {f['speciesId']:24} {f['before']:6} -> {f['after']}")
            return EXIT_CLEAN
        elif args.run_verb == "status":
            s = run_module.status(paths=paths)
            print(json.dumps(s, indent=2) if args.json else
                  " ".join(f"{k}={v}" for k, v in s.items()))
            return EXIT_CLEAN
        else:  # unreachable — argparse `choices` already guards this
            print(f"seedsmith: unknown run verb {args.run_verb!r}", file=sys.stderr)
            return EXIT_CANNOT_RUN
    except run_module.RunRefused as e:
        print(f"seedsmith: run {args.run_verb} refused: {e}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    print(f"run {record.run_id}: state={record.state} completed={len(record.completed)} "
          f"failed={len(record.failed)} callsMade={record.calls_made}")
    return EXIT_CLEAN if record.state in ("completed", "paused", "cancelled") and not record.failed else EXIT_GAP


def _cmd_demons_metrics(args: argparse.Namespace) -> int:
    """`seedsmith demons metrics [--gate] [--grid] [--queue] [--anchors DIR]` (demon-seed module
    14, spec-roster-metrics.md). A thin wrapper over `DemonRoster/*`'s own registry entries so the
    spec's literal command line works, without a second metrics engine beside `report`.
    """
    from ..adapters.demons.anchor.review_queue import read_review_queue
    from ..metrics.demon_roster import ALL_ELEMENT_PAIRS, GridFillMetric

    anchors_root = Path(args.anchors) if args.anchors else Path("../../data/seed/demons/species")
    anchors = _load_demon_anchors(anchors_root)
    if anchors is None:
        print(f"seedsmith: no readable anchor tree at {anchors_root} (expected an _index.json) — "
              f"run `demons run start --all` first (the full corpus run is a real, hours-long "
              f"commitment; run a small --species selector first to prove the mechanism)", file=sys.stderr)
        return EXIT_CANNOT_RUN

    if args.queue:
        queue_path = anchors_root.parent / "_runs" / "threat-audit-queue.json"
        entries = read_review_queue(queue_path)
        if not entries:
            print(f"metrics --queue: no review queue at {queue_path} (or it is empty)")
            return EXIT_CLEAN
        for e in entries:
            print(f"  {e.side}:{e.species_id} computed={e.computed_rung_id} verdict={e.verdict} — {e.reason}")
        return EXIT_CLEAN

    ctx = Ctx(corpus=Corpus(), adapter=resolve_adapter("stub"), demon_anchors=anchors)
    registry = build_registry()
    demon_ids = [m.id for m in registry.all() if m.family == "DemonRoster"]
    findings = run_all(registry, ctx, metric_ids=demon_ids)

    if args.grid:
        grid_findings = [f for f in findings if f.metric == GridFillMetric.id]
        empty = set()
        for f in grid_findings:
            empty.update(f.evidence.get("emptyCells", []))
        print(f"grid: {len(ALL_ELEMENT_PAIRS)} pairs x 12 aptitudes = {len(ALL_ELEMENT_PAIRS) * 12} cells; "
              f"{len(empty)} empty (showing up to 20)")
        for cell in sorted(empty):
            print(f"  EMPTY: {cell}")
        return EXIT_CLEAN

    _print_human(findings)
    if args.gate:
        gating_ids = {m.id for m in registry.all() if m.gates}
        relevant = [f for f in findings if f.metric in gating_ids]
        return EXIT_GAP if any(f.severity is Severity.GAP for f in relevant) else EXIT_CLEAN
    return EXIT_GAP if any(f.severity is Severity.GAP for f in findings) else EXIT_CLEAN


def _cmd_demons_diff_legacy(args: argparse.Namespace) -> int:
    """`seedsmith demons diff-legacy --legacy PATH [--anchors DIR]` (T2.7, spec-anchor-emit.md §6).

    Closes the "no committed entrypoint" gap `legacy_diff.py`'s own function had — the same class of
    defect `families`/`generate --kind commander-effect` each hit once before this
    (`cmd_demons`'s own docstring). `--legacy` points at the plain-JSON export
    `dotnet run --project tools/DemonSpeciesGen -- --export-legacy PATH` produces from the real,
    compiled, shipped catalog; this module never reads C# source, matching `legacy_diff.py`'s own
    stated boundary.
    """
    from ..adapters.demons.anchor.legacy_diff import diff_legacy, format_report

    if not args.legacy:
        print("seedsmith: diff-legacy needs --legacy <path> — produce it with "
              "`dotnet run --project tools/DemonSpeciesGen -- --export-legacy <path>`", file=sys.stderr)
        return EXIT_CANNOT_RUN

    legacy_path = Path(args.legacy)
    if not legacy_path.exists():
        print(f"seedsmith: no file at {legacy_path}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    anchors_root = Path(args.anchors) if args.anchors else Path("../../data/seed/demons/species")
    anchors = _load_demon_anchors(anchors_root)
    if anchors is None:
        print(f"seedsmith: no readable anchor tree at {anchors_root} (expected an _index.json)", file=sys.stderr)
        return EXIT_CANNOT_RUN

    import json as _json
    legacy_raw = _json.loads(legacy_path.read_text(encoding="utf-8"))
    # Case-sensitivity: the compiled catalog's own ids are lowercase (DemonSpeciesCatalog.Validate's
    # own rule), the real anchor's speciesId is the captured TitleCase typeName -- the same mismatch
    # class `_load_families` already found and fixed once this session (runner.py).
    legacy = [{**e, "id": e["id"].lower()} for e in legacy_raw]
    new_anchors = [{**a, "speciesId": a["speciesId"].lower()} for a in anchors]

    report = diff_legacy(new_anchors, legacy, legacy_id_key="id", new_id_key="speciesId")
    overlap = len({a["speciesId"] for a in new_anchors} & {e["id"] for e in legacy})
    print(format_report(report))
    print(f"\nlegacy species: {len(legacy)}, new anchors: {len(new_anchors)}, "
          f"species present in both sets: {overlap}")
    return EXIT_CLEAN


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="seedsmith")
    sub = parser.add_subparsers(dest="command", required=True)

    check = sub.add_parser("check", help="run metrics against a corpus")
    check.add_argument("corpus_root", nargs="?", default=None,
                       help="not used with --family (a family-scoped check needs no corpus)")
    check.add_argument("--adapter", default="stub")
    check.add_argument("--family", default="", help="a family-scoped check (e.g. PassiveTree) "
                                                    "instead of a corpus_root one (task H2)")
    check.add_argument("--plan-root", dest="plan_root", default="",
                       help="--family PassiveTree only: override the seed root the committed "
                            "plan is read under, i.e. <root>/passive-tree/plan (default data/seed)")
    check.add_argument("--gate", action="store_true",
                       help="exit non-zero only for metrics promoted with gates=True")
    check.add_argument("--json", default=None, metavar="PATH")
    check.add_argument("--metric", action="append", default=None, metavar="ID",
                       help="run only this metric id (repeatable)")
    check.set_defaults(func=cmd_check)

    report = sub.add_parser(
        "report", help="run the FULL metric registry (item-corpus and demon-dump metrics alike)")
    report.add_argument("--corpus", default=None, metavar="DIR", help="an item/seed corpus root")
    report.add_argument("--adapter", default="stub")
    report.add_argument("--demon-dump", dest="demon_dump", default=None, metavar="DIR",
                        help="a corpus-dump tree root (data/seed/demons/_dump)")
    report.add_argument("--demon-anchors", dest="demon_anchors", default=None, metavar="DIR",
                        help="an emitted anchor tree root (data/seed/demons/species)")
    report.add_argument("--gate", action="store_true",
                        help="exit non-zero only for metrics promoted with gates=True")
    report.add_argument("--json", default=None, metavar="PATH")
    report.add_argument("--metric", action="append", default=None, metavar="ID",
                        help="run only this metric id (repeatable)")
    report.set_defaults(func=cmd_report)

    metrics = sub.add_parser("metrics", help="list registered metrics")
    metrics.add_argument("--coverage", action="store_true",
                         help="print Appendix-A coverage: claimed / known gap / unclaimed")
    metrics.set_defaults(func=cmd_metrics)

    demons = sub.add_parser("demons", help="demon corpus generation entrypoints")
    demon_sub = demons.add_subparsers(dest="demon_command", required=True)
    demon_sub.add_parser("motifs", help="re-derive motifs + the motif registry (no model calls)")
    power_parse = demon_sub.add_parser(
        "power-parse", help="numeric power seed + basis per species (no model calls)")
    power_parse.add_argument("--dump", required=True, help="corpus-dump tree root")
    power_parse.add_argument("--report", action="store_true",
                             help="print the basis histogram + disagreement list")
    threat_band = demon_sub.add_parser(
        "threat-band", help="score -> threat rung -> Theta offset per species (no model calls)")
    threat_band.add_argument("--dump", required=True, help="corpus-dump tree root")
    threat_band.add_argument("--histogram", action="store_true",
                             help="print rung occupancy, including empty rungs")
    contract = demon_sub.add_parser(
        "contract", help="the species anchor JSON Schema — print or numerically audit it")
    contract.add_argument("--print", dest="print_schema", action="store_true",
                          help="print the resolved JSON Schema")
    contract.add_argument("--audit", action="store_true",
                          help="run the numeric-smuggling audit, exit 1 on a finding (default)")
    preflight = demon_sub.add_parser(
        "preflight", help="the nine run-readiness checks — refuses or asks, never guesses")
    preflight.add_argument("--json", action="store_true", help="machine-readable output")
    preflight.add_argument("--skip-model", dest="skip_model", action="store_true",
                           help="checks 1-4, 7-9 only — CI's escape hatch, refused by run-control before a real run")
    permute = demon_sub.add_parser(
        "permute", help="show the three deterministic option orders for a species/field pair")
    permute.add_argument("--species", required=True)
    permute.add_argument("--field", required=True)
    dmetrics = demon_sub.add_parser(
        "metrics", help="roster-shape metrics over the emitted anchors (element grid, threat/rarity distribution, ...)")
    dmetrics.add_argument("--anchors", default="", help="anchor tree root (default data/seed/demons/species)")
    dmetrics.add_argument("--gate", action="store_true", help="exit non-zero only on gates=True findings")
    dmetrics.add_argument("--grid", action="store_true", help="print the 21x12 occupancy matrix")
    dmetrics.add_argument("--queue", action="store_true", help="print the threat-audit open-loop review queue")
    fam = demon_sub.add_parser("families", help="extract + consolidate demon families (model calls)")
    fam.add_argument("--dry-run", dest="dry_run", action="store_true")
    fam.add_argument("--write", action="store_true")
    fam.add_argument("--i-have-read-the-append-only-note", dest="ack", action="store_true")
    gen = demon_sub.add_parser("generate", help="generate content for a demon kind")
    gen.add_argument("--kind", default="commander-effect")
    gen.add_argument("--only", default="", help="comma-separated demon ids")
    gen.add_argument("--stale", action="store_true",
                     help="only entries whose recorded motifs no longer match")
    gen.add_argument("--force", action="store_true", help="regenerate everything")
    gen.add_argument("--dry-run", dest="dry_run", action="store_true")
    gen.add_argument("--workers", type=int, default=0)
    gen.add_argument("--endpoint", default="")
    gen.add_argument("--model", default="")
    # --kind anchor (demon-seed module 7, classify-pipelines):
    gen.add_argument("--pipeline", default="", help="one of the 8 classify-pipelines ids (--kind anchor)")
    gen.add_argument("--species", default="", help="a speciesId (--kind anchor)")
    gen.add_argument("--dump", default="", help="corpus-dump tree root (--kind anchor, default ../../data/seed/demons/_dump)")
    gen.add_argument("--all", action="store_true", help="refused here — use `demons run start --all` (--kind anchor)")
    difflegacy = demon_sub.add_parser(
        "diff-legacy",
        help="field agreement between the new classification and the shipped, compiled legacy catalog")
    difflegacy.add_argument("--legacy", default="",
                            help="path to the JSON `DemonSpeciesGen --export-legacy` produced (required)")
    difflegacy.add_argument("--anchors", default="", help="anchor tree root (default data/seed/demons/species)")
    run = demon_sub.add_parser(
        "run", help="run-control: pause/resume/cancel/rerun/overwrite-all over the anchor classification run")
    run.add_argument("run_verb", choices=("start", "pause", "resume", "cancel", "rerun", "status",
                                          "overwrite-all", "fix-unresolved", "fix-secondary-from-fusion"))
    run.add_argument("--all", action="store_true", help="selector: every species in the dump")
    run.add_argument("--side", default="", help="selector: plant | zombie")
    run.add_argument("--family", default="", help="selector: one family id")
    run.add_argument("--species", default="", help="selector: comma-separated species ids")
    run.add_argument("--pipeline", default="", help="selector: one of the 8 classify-pipelines ids")
    run.add_argument("--basis", default="", help="selector: observed | stated | inferred | blocked")
    run.add_argument("--unresolved", action="store_true", help="selector: only fields a vote could not settle")
    run.add_argument("--stale", action="store_true", help="selector: only entries whose inputs moved")
    run.add_argument("--confirm", default="", help="overwrite-all: the confirmation token")
    run.add_argument("--dump", default="", help="corpus-dump tree root (default data/seed/demons/_dump)")
    run.add_argument("--anchors", default="", help="anchor tree root (default data/seed/demons/species)")
    run.add_argument("--json", action="store_true", help="machine-readable output (status)")
    run.add_argument("--workers", type=int, default=4,
                     help="parallel model-call workers for start/resume/rerun/overwrite-all "
                          "(default 4; 1 = sequential, today's original behaviour)")
    run.add_argument("--dry-run", action="store_true",
                     help="fix-unresolved/fix-secondary-from-fusion: report what would change without writing anything")
    run.add_argument("--fusion-recipes", default="",
                     help="fix-secondary-from-fusion: path to the committed _fusion-recipes.json "
                          "(default data/generated/demons/_fusion-recipes.json)")
    demons.set_defaults(func=cmd_demons)

    items = sub.add_parser("items", help="item corpus generation entrypoints (modules 13, 21)")
    items_sub = items.add_subparsers(dest="items_command", required=True)
    igen = items_sub.add_parser("generate", help="plan a set/charm/combination generation run")
    igen.add_argument("--kind", default="set", choices=("set", "charm", "combination"))
    igen.add_argument("--population", default="species", choices=("species", "build"),
                      help="set/charm only; a combination's grid is closed, so --shape selects it")
    igen.add_argument("--shape", default="strain", choices=("strain", "splice"),
                      help="combination only: 36 Strains (12 aptitudes x 3 archetypes) or 66 "
                           "Splices (C(12,2))")
    igen.add_argument("--dry-run", dest="dry_run", action="store_true",
                      help="the default and currently the only mode — assemble the plan, make no "
                           "model calls")
    igen.add_argument("--write", action="store_true",
                      help="set/charm: run the planned subjects through the generation graph and "
                           "write seed files; needs --out-dir and one of --answers or --endpoint. "
                           "combination: same shape, --answers + --out-dir only (no live endpoint "
                           "yet, that is set-charm-live-endpoint's own scope)")
    igen.add_argument("--sample-brief", dest="sample_brief", action="store_true",
                      help="print the first subject's assembled brief")
    igen.add_argument("--limit", type=int, default=0,
                      help="set/charm: plan only the first N subjects (0 = all). A small batch is "
                           "how a run is evaluated before the full population is committed to")
    igen.add_argument("--briefs-out", dest="briefs_out", default="",
                      help="set/charm: write the planned subjects and their assembled briefs to "
                           "this JSON file, for a model to answer")
    igen.add_argument("--answers", default="",
                      help="set/charm --write: an authored-answer file keyed by subjectId (a list "
                           "of attempts per subject is legal — the graph's repair edge consumes "
                           "them in order). The deterministic path; mutually exclusive with "
                           "--endpoint in practice (--answers wins if both are given)")
    igen.add_argument("--endpoint", default="",
                      help="set/charm --write: call a real model endpoint via "
                           "pipeline.llm_caller.call_model instead of replaying an answer file — "
                           "the same live transport --endpoint already selects for effects/demons "
                           "generate. Ignored when --answers is also given")
    igen.add_argument("--out-dir", dest="out_dir", default="",
                      help="set/charm --write: where the seed files land. No default, and a path "
                           "inside data/seed/items/ is refused unless --allow-production-tree")
    igen.add_argument("--allow-production-tree", dest="allow_production_tree",
                      action="store_true",
                      help="set/charm --write: permit an --out-dir inside data/seed/items/. This "
                           "is the production run; every items metric globs that tree")
    igen.add_argument("--ledger", default="",
                      help="set/charm --write: resume-ledger path (default: <out-dir>/"
                           "set-charm-gen.ledger.json, so a sample never touches the real one)")
    igen.add_argument("--ignore-ledger", dest="ignore_ledger", action="store_true",
                      help="plan every generatable subject, even ones a previous run recorded")
    igen.add_argument("--model", default="unrecorded",
                      help="set/charm --write: with --answers, metadata only — the model id "
                           "stamped into each seed file's _meta. With --endpoint, also the model "
                           "id sent on the live call (falls back to llm_caller's own default if "
                           "left unset)")
    igen.add_argument("--authored-utc", dest="authored_utc", default="1970-01-01T00:00:00Z",
                      help="set/charm --write: the _meta timestamp. Injected, never read from the "
                           "clock — a wall-clock stamp is the one field that makes a generated "
                           "file non-reproducible (pipeline/provenance.py's own rule)")
    ivalidate = items_sub.add_parser(
        "validate", help="pre-flight dependency checks over the real corpus (module 21)")
    ivalidate.add_argument("--deps", action="store_true",
                           help="combination: confirm every hostRole/ingredients family a run "
                                "could request resolves against real content, before any subject "
                                "is planned (acceptance 3a, spec-combination-write-unblock.md)")
    imigrate = items_sub.add_parser(
        "combogen-migrate",
        help="report combogen.migrate's own socket-word retirement plan (module 21)")
    imigrate.add_argument("--dry-run", dest="dry_run", action="store_true",
                          help="the only supported mode — reports the legality/migration-sites "
                               "check, writes and deletes nothing")
    items.set_defaults(func=cmd_items)

    effects = sub.add_parser("effects", help="effect-pipeline generation entrypoints")
    effects_sub = effects.add_subparsers(dest="effects_command", required=True)
    gen = effects_sub.add_parser("generate", help="generate content for an effect-pipeline kind")
    gen.add_argument("--kind", default="affix")
    gen.add_argument("--only", default="", help="comma-separated atom ids narrowing the eligible pool (--kind affix)")
    gen.add_argument("--theme", default="", help="optional theme hint in the brief (--kind affix)")
    gen.add_argument("--count", type=int, default=0, help="how many independent bundles to draw (--kind affix)")
    gen.add_argument("--dry-run", dest="dry_run", action="store_true")
    gen.add_argument("--workers", type=int, default=0)
    gen.add_argument("--endpoint", default="")
    gen.add_argument("--model", default="")
    gen.add_argument(
        "--species-id", default="",
        help="task J7: author into affix.species.<speciesId>.* / "
             "data/seed/effects/affixes/species/<speciesId>.json instead of the shared corpus "
             "(--kind affix)")
    effects.set_defaults(func=cmd_effects)

    structures = sub.add_parser("structures", help="base-defense structure corpus entrypoints (module 23+)")
    structures_sub = structures.add_subparsers(dest="structures_command", required=True)
    structures_contract = structures_sub.add_parser(
        "contract", help="the structure anchor JSON Schema — print or numerically audit it")
    structures_contract.add_argument("--print", dest="print_schema", action="store_true",
                                     help="print the resolved JSON Schema")
    structures_contract.add_argument("--audit", action="store_true",
                                     help="run the numeric-smuggling audit, exit 1 on a finding (default)")
    structures.set_defaults(func=cmd_structures)

    trees = sub.add_parser("trees", help="passive-tree plan entrypoints (task B1, spec-tree-plan.md)")
    trees_sub = trees.add_subparsers(dest="trees_command", required=True)
    trees_plan = trees_sub.add_parser("plan", help="emit or check the deterministic tree plan")
    trees_plan.add_argument("--emit", action="store_true", help="write the plan (default if no flag given)")
    trees_plan.add_argument("--check", action="store_true",
                            help="regenerate in memory and diff against the committed plan")
    trees_plan.add_argument("--tree", default="might",
                            help="tree id to plan — any of the 12 primary trees named by the roster, "
                                 "or (J1) any of the roster's elemental or status tree ids "
                                 "(default: might, B1's own named tree)")
    trees_plan.add_argument("--manifest", action="store_true",
                            help="operate on the top-level manifest (plan.v1.json) + its trees[], "
                                 "not just --tree alone (task C2)")
    trees_plan.add_argument("--generate", action="store_true",
                            help="R-G1: ask stage 2 to generate content for --tree; exits 3 naming "
                                 "the tree and gate quantity if its gateState is 'pending' (task C2). "
                                 "tree-language (H1) is not built yet, so a 'carrier' tree still "
                                 "generates nothing here beyond passing the gate.")
    trees_plan.add_argument("--diff", nargs=2, metavar=("PLAN_A", "PLAN_B"),
                            help="compare two manifests: budget deltas, archetype reassignments, "
                                 "quota-cell moves, and node ids added/removed/re-minted (task C2)")
    trees_generate = trees_sub.add_parser(
        "generate", help="generate node content for a tree, or print the run's gate report "
                         "(task H2, spec-tree-language.md)")
    trees_generate.add_argument("--tree", default="", help="a single tree id")
    trees_generate.add_argument("--all", action="store_true", help="every tree with a committed plan")
    trees_generate.add_argument("--dry-run", dest="dry_run", action="store_true",
                                help="the default and currently the only mode — assemble subjects "
                                     "and print the gate report, make no model calls")
    trees_generate.add_argument("--write", action="store_true",
                                help="real generation — refused unless the registry carries "
                                     "exactly one PassiveTree/* gates=True metric (§7.1)")
    trees_generate.add_argument("--sample-brief", dest="sample_brief", action="store_true",
                                help="render one real brief against the first subject's own "
                                     "resolved quota cell and permitted affix subset")
    trees_generate.add_argument("--plan-root", dest="plan_root", default="",
                                help="override the seed root the committed plan is read under, "
                                     "i.e. <root>/passive-tree/plan (default data/seed)")
    trees_generate.add_argument("--ledger-path", dest="ledger_path", default="",
                                help="override the idempotence ledger path --write reads/writes "
                                     "(default data/seed/passive-tree/_runs/tree-language.ledger.json) "
                                     "— tests point this at a temp file so a CLI wiring test never "
                                     "touches the real ledger. --write's emitted seed documents "
                                     "(data/seed/passive-tree/nodes/<treeId>.json) follow "
                                     "--plan-root, the same seed root the committed plan is read "
                                     "under, for the identical reason")
    trees_generate.add_argument("--workers", type=int, default=1,
                                help="parallel model-call workers WITHIN a (tier, nodeClass) batch "
                                     "only — batches themselves stay sequential so a later one still "
                                     "sees every earlier one's real accepted tier-siblings (§6.2). "
                                     "Default 1 = sequential, byte-identical to pre-2026-09-06 "
                                     "behaviour. Mirrors workflow.runner.MAX_WORKERS=4's own "
                                     "rationale for a local model queue, but cannot reuse that helper "
                                     "directly (it is built around a LangGraph app.invoke() interface "
                                     "this pipeline never adopted)")
    trees_review = trees_sub.add_parser(
        "review", help="tree-review entrypoints (task H7, spec-tree-review.md §5.5, §Commands)")
    trees_review.add_argument("--lot", required=True, help="the review lot id")
    trees_review.add_argument(
        "--census", action="store_true",
        help="refuse to start a census without a CURRENT sheetRead row (§5.5) — the only mode "
             "wired today; the census tiers themselves are H8+ and unbuilt")
    trees_review.add_argument(
        "--sheet-dir", dest="sheet_dir", default="",
        help="override where <lot>/sheet.json is read from (default "
             "docs/research/passive-tree/_review)")
    trees_review.add_argument(
        "--review-dir", dest="review_dir", default="",
        help="override where <lot>.json's sheetReads/entries are read from (default "
             "data/seed/passive-tree/_review)")
    trees.set_defaults(func=cmd_trees)

    numerics = sub.add_parser(
        "numerics", help="tier-bands tuning entrypoints (spec-numerics.md §3.1, §3.2)")
    numerics_sub = numerics.add_subparsers(dest="numerics_command", required=True)
    nrebalance = numerics_sub.add_parser(
        "rebalance",
        help="apply channelWeight/baseShare overrides to the latest tier-bands and, with "
             "--publish, write the next version")
    nrebalance.add_argument("--set", dest="set_pairs", action="append", default=None,
                            metavar="KEY=VALUE",
                            help="channelWeight.<id>=<ratio> or baseShare=<ratio>, repeatable "
                                 "(ratio, not per-mille: 1.0 == 1000‰)")
    nrebalance.add_argument("--set-file", dest="set_file", default="", metavar="PATH",
                            help="a file of KEY=VALUE lines (# comments allowed) — the reviewable "
                                 "form of a large --set batch")
    nrebalance.add_argument("--publish", action="store_true",
                            help="write tier-bands.v{n+1}.json; the old version stays for revert")
    nrebalance.set_defaults(func=cmd_numerics)

    return parser


def main(argv: "list[str] | None" = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)
