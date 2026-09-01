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
from ..metrics.distribution import CellDeviation, Evenness, Inequality
from ..metrics.constraint import Constraint
from ..metrics.exemplar import ExemplarConformance
from ..metrics.dedup import SemanticDedup
from ..metrics.quality import FlavourGeneric, FlavourMissing
from ..metrics.demon_coverage import DemonUncoveredMetric
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
    registry.register(Constraint())
    registry.register(ExemplarConformance())
    registry.register(SemanticDedup())
    registry.register(FlavourMissing())
    registry.register(FlavourGeneric())
    registry.register(DemonUncoveredMetric())
    registry.register(MotifSharingMetric())
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

    metrics = sub.add_parser("metrics", help="list registered metrics")
    metrics.add_argument("--coverage", action="store_true",
                         help="print Appendix-A coverage: claimed / known gap / unclaimed")
    metrics.set_defaults(func=cmd_metrics)

    demons = sub.add_parser("demons", help="demon corpus generation entrypoints")
    demon_sub = demons.add_subparsers(dest="demon_command", required=True)
    demon_sub.add_parser("motifs", help="re-derive motifs + the motif registry (no model calls)")
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
    demons.set_defaults(func=cmd_demons)

    return parser


def main(argv: "list[str] | None" = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)
