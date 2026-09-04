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
from ..metrics.quality import FlavourGeneric, FlavourMissing
from ..metrics.corpus_coverage import BasisHistogramMetric, DumpCompletenessMetric
from ..metrics.demon_coverage import DemonUncoveredMetric
from ..metrics.demon_roster import ALL_DEMON_ROSTER_METRICS
from ..metrics.pipeline_health import ALL_PIPELINE_HEALTH_METRICS
from ..metrics.motif_sharing import MotifSharingMetric
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
    registry.register(DemonUncoveredMetric())
    registry.register(MotifSharingMetric())
    registry.register(DumpCompletenessMetric())
    registry.register(BasisHistogramMetric())
    for metric_cls in ALL_DEMON_ROSTER_METRICS:
        registry.register(metric_cls())
    for metric_cls in ALL_PIPELINE_HEALTH_METRICS:
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


def _print_human(findings, *, stream=sys.stdout) -> None:
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


def cmd_check(args: argparse.Namespace) -> int:
    try:
        adapter = resolve_adapter(args.adapter)
    except KeyError:
        print(f"seedsmith: unknown adapter {args.adapter!r} "
              f"(known: {', '.join(known_adapter_names())})", file=sys.stderr)
        return EXIT_CANNOT_RUN

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
    `--write` is the explicit opt-in, and it currently refuses: the generation graph is not wired
    (see the module's own todo entry), and a command that silently writes nothing is worse than one
    that says so.
    """
    if args.items_command != "generate":
        print(f"unknown items command {args.items_command!r}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    from ..adapters.items.setgen import run as run_mod
    from ..adapters.items.setgen import themes as themes_mod
    from ..adapters.items.setgen import tuning as tuning_mod
    from ..adapters.items.setgen import vocab as vocab_mod
    from ..adapters.items.setgen.verdict import GATING_METRICS, missing_thresholds

    tuning = tuning_mod.load()
    vocabulary = vocab_mod.build(tuning)
    try:
        plan = run_mod.plan_run(kind=args.kind, population=args.population,
                                tuning=tuning, vocabulary=vocabulary)
    except ValueError as exc:
        print(f"seedsmith: {exc}", file=sys.stderr)
        return EXIT_CANNOT_RUN

    coverage = themes_mod.coverage_report(themes_mod.load_species_themes())
    summary = {
        **plan.summary(),
        "kind": args.kind,
        "population": args.population,
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

    if args.write:
        print("seedsmith: --write is refused — the generation graph for `items generate` is not "
              "wired yet (module 13's own deferred item). The plan above is real; the model call "
              "is not.", file=sys.stderr)
        return EXIT_REFUSED
    return EXIT_CLEAN


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
                print(f"{len(fixed)} species {verb} (threatBand only — the one field with a real, "
                      f"already-sanctioned deterministic default; aptitude/rarity/element have "
                      f"none and stay unresolved)")
                for f in fixed:
                    print(f"  {f['speciesId']:24} {f['before']:12} -> {f['after']}")
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
    check.add_argument("corpus_root")
    check.add_argument("--adapter", default="stub")
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
                                          "overwrite-all", "fix-unresolved"))
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
                     help="fix-unresolved: report what would change without writing anything")
    demons.set_defaults(func=cmd_demons)

    items = sub.add_parser("items", help="item corpus generation entrypoints (module 13)")
    items_sub = items.add_subparsers(dest="items_command", required=True)
    igen = items_sub.add_parser("generate", help="plan a set/charm generation run")
    igen.add_argument("--kind", default="set", choices=("set", "charm"))
    igen.add_argument("--population", default="species", choices=("species", "build"))
    igen.add_argument("--dry-run", dest="dry_run", action="store_true",
                      help="the default and currently the only mode — assemble the plan, make no "
                           "model calls")
    igen.add_argument("--write", action="store_true",
                      help="refused today: the generation graph is not wired")
    igen.add_argument("--sample-brief", dest="sample_brief", action="store_true",
                      help="print the first subject's assembled brief")
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
    effects.set_defaults(func=cmd_effects)

    return parser


def main(argv: "list[str] | None" = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)
