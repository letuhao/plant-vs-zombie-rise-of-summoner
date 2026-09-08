"""seedsmith.adapters.items.fill — cross-kind resume/fill driver for `items fill`.

Walks item kinds in item-seedgen-map dependency order. Closed grids (set/charm/combination/
material) run once with `--write` and rely on RunLedger resume. Partitioned kinds walk
*discovered* corpus partitions only — never invent a new (role,frame,band) / slot / group.

Batch controls (`--limit` / `--count` / `--batch-size` / `--max-partitions` / `--full`) exist so a
first live run can stay small; unbounded set/charm/combination without `--limit` or `--full` is
refused at plan time.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Callable

from .defaults import FILL_KIND_ORDER, ITEM_SEED_ROOT

DispatchFn = Callable[[list[str]], int]

#: Kinds that write under data/seed/items/ via --out-dir (need allow-production).
PRODUCTION_KINDS = frozenset({"set", "charm", "combination"})

#: Closed grids that explode without --limit unless --full.
BOUNDED_GRID_KINDS = frozenset({"set", "charm", "combination"})

_BASE_TYPE_STEM = re.compile(
    r"^(?P<frame>humanoid|plant)-(?P<role>.+)-(?P<band>[a-z])$")

#: Hard edges from item-seedgen-map.md §3 (kind → must precede these dependents).
#: Used by topo tests; FILL_KIND_ORDER must be a valid linearization.
MAP_DEPENDENCY_EDGES: "tuple[tuple[str, str], ...]" = (
    ("affix-family", "base-type"),
    ("enhancement-milestone", "base-type"),
    ("material", "recipe"),
    ("material", "drop-table"),
    ("gem", "combination"),
    ("gem", "drop-table"),
    ("consumable", "drop-table"),
    ("base-type", "set"),
    ("base-type", "charm"),
    ("base-type", "recipe"),
    ("base-type", "combination"),
    ("base-type", "drop-table"),
)


@dataclass
class FillLimits:
    """Batch bounds for one fill invocation."""

    limit: int = 0          # set/charm/combination subjects (0 = refuse unless full)
    count: int = 1          # base-type / milestone / recipe / drop-table draws
    batch_size: int = 1     # gem partition batch
    max_partitions: int = 1  # cap discovered partition jobs (0 = all when full)
    full: bool = False      # unbounded closed grids + all partitions


@dataclass
class FillStep:
    kind: str
    argv: "list[str]"
    note: str = ""
    seq: int = 0  # stable discovery order within a kind


@dataclass
class FillStepResult:
    kind: str
    argv: "list[str]"
    status: str  # planned | ran | skipped | refused | gap | error
    exit_code: int = 0
    note: str = ""


@dataclass
class FillReport:
    dry_run: bool
    refused_reason: str = ""
    steps: "list[FillStepResult]" = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "dryRun": self.dry_run,
            "refusedReason": self.refused_reason or None,
            "steps": [asdict(s) for s in self.steps],
        }

    @property
    def worst_exit_code(self) -> int:
        from ...report.cli import EXIT_CANNOT_RUN, EXIT_CLEAN, EXIT_GAP, EXIT_REFUSED
        if self.refused_reason:
            return EXIT_REFUSED
        codes = [s.exit_code for s in self.steps if s.status in ("refused", "gap", "error")]
        if not codes:
            return EXIT_CLEAN
        if EXIT_REFUSED in codes:
            return EXIT_REFUSED
        if EXIT_CANNOT_RUN in codes:
            return EXIT_CANNOT_RUN
        if EXIT_GAP in codes or any(c == 1 for c in codes):
            return EXIT_GAP
        return max(codes)


def discover_base_type_partitions(
        base_types_dir: Path | None = None) -> "list[tuple[str, str, str]]":
    """Existing `frame-role-band.json` partition files only."""
    directory = base_types_dir or (ITEM_SEED_ROOT / "base-types")
    found: "list[tuple[str, str, str]]" = []
    if not directory.is_dir():
        return found
    for path in sorted(directory.glob("*.json")):
        m = _BASE_TYPE_STEM.match(path.stem)
        if not m:
            continue
        found.append((m.group("role"), m.group("frame"), m.group("band")))
    return found


def discover_drop_table_slots(drop_tables_dir: Path | None = None) -> "list[int]":
    directory = drop_tables_dir or (ITEM_SEED_ROOT / "drop-tables")
    slots: "list[int]" = []
    if not directory.is_dir():
        return slots
    for path in sorted(directory.glob("d*.json")):
        stem = path.stem
        if len(stem) >= 2 and stem[1:].isdigit():
            slots.append(int(stem[1:]))
    return slots


def discover_gem_slots(gems_dir: Path | None = None) -> "list[int]":
    directory = gems_dir or (ITEM_SEED_ROOT / "gems")
    slots: "list[int]" = []
    if not directory.is_dir():
        return slots
    for path in sorted(directory.glob("g*.json")):
        stem = path.stem
        if len(stem) >= 2 and stem[1:].isdigit():
            slots.append(int(stem[1:]))
    return slots


def discover_affix_family_jobs(
        families_dir: Path | None = None) -> "list[tuple[str, str]]":
    """`(group_id, kind_id)` pairs already present in shipped partition files.

    Only `stat.modify` / `stat.derived` — the only kinds affixfamgen authors
    (`opvocab.legal_ops` / spec-affix-families-gen Acceptance #3). Other kindIds in the
    corpus (e.g. `status.apply`) must not become fill jobs.
    """
    legal = frozenset({"stat.modify", "stat.derived"})
    directory = families_dir or (ITEM_SEED_ROOT / "affix-families")
    jobs: "list[tuple[str, str]]" = []
    if not directory.is_dir():
        return jobs
    seen: set[tuple[str, str]] = set()
    for path in sorted(directory.glob("g-*.json")):
        stem = path.stem
        if not stem.startswith("g-"):
            continue
        group_id = "g." + stem[2:]
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        kinds: set[str] = set()
        for entry in doc.get("entries", []):
            kid = entry.get("kindId") or entry.get("kind")
            if isinstance(kid, str) and kid in legal:
                kinds.add(kid)
        for kid in sorted(kinds):
            key = (group_id, kid)
            if key not in seen:
                seen.add(key)
                jobs.append(key)
    return jobs


def _cap_partitions(items: list, limits: FillLimits) -> list:
    if limits.full or limits.max_partitions <= 0:
        return items
    return items[: limits.max_partitions]


def _affix_has_free_pairs(group_id: str, affix_kind: str) -> bool:
    """True when the partition still has a free (channel, op) for this kind."""
    from .affixfamgen import brief as affix_brief
    try:
        built = affix_brief.build_affix_family_brief(group_id, affix_kind)
        return bool(affix_brief.free_channel_ops(built.partition, affix_kind))
    except Exception:
        # Unreadable / illegal kind — let the generate step surface the real refuse.
        return True


def _gem_slot_has_work(slot: int, batch_size: int) -> bool:
    """True when gemgen plan_partition still has subjects for this slot."""
    from .gemgen.run import plan_partition
    try:
        plan = plan_partition(f"gems/{slot}", batch_size=batch_size)
        return bool(plan.subjects)
    except Exception:
        return True


def plan_fill_steps(*, kinds: "tuple[str, ...] | None" = None,
                    allow_production: bool = True,
                    limits: FillLimits | None = None) -> "tuple[list[FillStep], str]":
    """Build argv lists. Returns `(steps, refuse_reason)` — refuse_reason non-empty means do not run.

    Stable within-kind order = discovery/append order (strain before splice). Cross-kind order =
    FILL_KIND_ORDER. Never sort by note string.
    """
    limits = limits or FillLimits()
    selected = set(kinds) if kinds is not None else set(FILL_KIND_ORDER)
    steps: "list[FillStep]" = []
    seq = 0

    def want(kind: str) -> bool:
        return kind in selected

    def add(kind: str, argv: list[str], note: str = "") -> None:
        nonlocal seq
        steps.append(FillStep(kind=kind, argv=argv, note=note, seq=seq))
        seq += 1

    # Refuse unbounded closed grids unless --limit or --full.
    if selected & BOUNDED_GRID_KINDS and not limits.full and limits.limit <= 0:
        return [], (
            "items fill refused — set/charm/combination need --limit N (smoke) or --full "
            "(unbounded). A default fill without a bound is thousands of model calls.")

    if want("affix-family"):
        all_jobs = discover_affix_family_jobs()
        runnable = [(g, k) for g, k in all_jobs if _affix_has_free_pairs(g, k)]
        full_count = len(all_jobs) - len(runnable)
        jobs = _cap_partitions(runnable, limits)
        if not all_jobs:
            add("affix-family", [],
                "SKIPPED — no affix-family partition files with kindId to discover")
        elif not runnable:
            add("affix-family", [],
                "SKIPPED — every discovered affix partition has no free (channel, op)")
        else:
            for group_id, affix_kind in jobs:
                add("affix-family",
                    ["--kind", "affix-family", "--group", group_id,
                     "--affix-kind", affix_kind, "--write"],
                    f"group={group_id} affix-kind={affix_kind}")
            if full_count and limits.max_partitions > 0 and not limits.full:
                add("affix-family", [],
                    f"SKIPPED — {full_count} partition(s) have no free (channel, op)")

    if want("material"):
        add("material", ["--kind", "material", "--write"])

    if want("gem"):
        all_slots = discover_gem_slots()
        runnable_slots = [s for s in all_slots
                          if _gem_slot_has_work(s, limits.batch_size)]
        empty_count = len(all_slots) - len(runnable_slots)
        slots = _cap_partitions(runnable_slots, limits)
        if not all_slots:
            add("gem", [], "SKIPPED — no gems/gN.json partitions discovered")
        elif not runnable_slots:
            add("gem", [],
                "SKIPPED — every gems/N partition alreadyDone (toGenerate=0)")
        else:
            for slot in slots:
                # Outer `items generate` accepts shared `--count`; cli remaps to
                # gemgen `--batch-size`. Emitting `--batch-size` here makes
                # argparse SystemExit → refused.
                add("gem",
                    ["--kind", "gem", "--slot", str(slot),
                     "--count", str(limits.batch_size), "--write"],
                    f"slot={slot}")
            if empty_count and limits.max_partitions > 0 and not limits.full:
                add("gem", [],
                    f"SKIPPED — {empty_count} slot(s) alreadyDone (toGenerate=0)")

    if want("consumable"):
        add("consumable", ["--kind", "consumable"],
            "reconcile-only (pass --theme on generate to mint)")

    if want("enhancement-milestone"):
        add("enhancement-milestone",
            ["--kind", "enhancement-milestone", "--count", str(limits.count), "--write"])

    if want("base-type"):
        parts = _cap_partitions(discover_base_type_partitions(), limits)
        if not parts:
            add("base-type", [], "SKIPPED — no base-types/*.json partitions discovered")
        for role, frame, band in parts:
            add("base-type",
                ["--kind", "base-type", "--role", role, "--frame", frame,
                 "--band", band, "--count", str(limits.count), "--write"],
                f"{frame}/{role}/{band}")

    for kind in ("set", "charm"):
        if want(kind):
            argv = ["--kind", kind, "--population", "species", "--write"]
            if not limits.full:
                argv += ["--limit", str(limits.limit)]
            if allow_production:
                argv.append("--allow-production-tree")
            add(kind, argv)

    if want("recipe"):
        # count>0 is required for --write/--backfill to run past reconcile in recipegen.
        add("recipe",
            ["--kind", "recipe", "--count", str(max(limits.count, 1)),
             "--write", "--backfill"],
            "reconcile + forge backfill")

    if want("combination"):
        for shape in ("strain", "splice"):  # strain first — never lexicographic note sort
            argv = ["--kind", "combination", "--shape", shape, "--write"]
            if not limits.full:
                argv += ["--limit", str(limits.limit)]
            if allow_production:
                argv.append("--allow-production-tree")
            add("combination", argv, f"shape={shape}")

    if want("drop-table"):
        slots = _cap_partitions(discover_drop_table_slots(), limits)
        if not slots:
            add("drop-table", [], "SKIPPED — no drop-tables/dN.json partitions discovered")
        for slot in slots:
            add("drop-table",
                ["--kind", "drop-table", "--slot", str(slot),
                 "--count", str(limits.count), "--write"],
                f"slot={slot}")

    # Cross-kind order only — within-kind order is append/seq (stable).
    order_index = {k: i for i, k in enumerate(FILL_KIND_ORDER)}
    steps.sort(key=lambda s: (order_index.get(s.kind, 99), s.seq))
    return steps, ""


def run_fill(*, kinds: "tuple[str, ...] | None" = None, dry_run: bool = False,
             allow_production: bool = True,
             limits: FillLimits | None = None,
             dispatch: DispatchFn | None = None,
             stop_on_error: bool = True) -> FillReport:
    """Execute (or print) the fill plan. `dispatch` receives argv after `items generate`."""
    from ...report import cli as cli_mod

    report = FillReport(dry_run=dry_run)
    steps, refuse = plan_fill_steps(
        kinds=kinds, allow_production=allow_production, limits=limits)
    if refuse:
        report.refused_reason = refuse
        return report

    def default_dispatch(argv: list[str]) -> int:
        args = cli_mod.build_parser().parse_args(["items", "generate", *argv])
        return cli_mod.cmd_items(args)

    run = dispatch or default_dispatch
    stop_statuses = frozenset({"refused", "gap", "error"})

    for step in steps:
        if not step.argv:
            report.steps.append(FillStepResult(
                kind=step.kind, argv=[], status="skipped", note=step.note))
            continue
        if dry_run:
            report.steps.append(FillStepResult(
                kind=step.kind, argv=step.argv, status="planned", note=step.note))
            continue
        try:
            code = run(step.argv)
        except SystemExit as exc:
            # Passthrough mains raise SystemExit(str) — map to refused, keep the report.
            code = exc.code if isinstance(exc.code, int) else cli_mod.EXIT_REFUSED
            if code is None:
                code = cli_mod.EXIT_REFUSED
            report.steps.append(FillStepResult(
                kind=step.kind, argv=step.argv, status="refused",
                exit_code=int(code) if isinstance(code, int) else cli_mod.EXIT_REFUSED,
                note=step.note or (str(exc) if exc.args else "SystemExit")))
            if stop_on_error:
                break
            continue
        except Exception as exc:
            report.steps.append(FillStepResult(
                kind=step.kind, argv=step.argv, status="error",
                exit_code=cli_mod.EXIT_CANNOT_RUN,
                note=f"{step.note + '; ' if step.note else ''}{type(exc).__name__}: {exc}"))
            if stop_on_error:
                break
            continue

        if code == cli_mod.EXIT_CLEAN:
            status = "ran"
        elif code == cli_mod.EXIT_GAP:
            status = "gap"
        elif code in (cli_mod.EXIT_REFUSED, cli_mod.EXIT_CANNOT_RUN):
            status = "refused"
        else:
            status = "error"
        report.steps.append(FillStepResult(
            kind=step.kind, argv=step.argv, status=status,
            exit_code=code, note=step.note))
        if stop_on_error and status in stop_statuses:
            break
    return report
