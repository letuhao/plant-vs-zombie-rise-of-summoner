"""seedsmith.adapters.items.recipegen.run — the run plan, ledger wiring, reconcile, and the
container-backfill mechanism (`generator-harness`, acceptance #3/#4/#5).

⭐ **Real finding, confirmed against the shipped `data/seed/items/recipes/recipes.json` while
building this module (2026-09-07):**

1. **Acceptance #3's own evidence, positive:** every one of the 30 real `operation` values already
   matches `opvocab.ALL_OPERATIONS` — `reconcile_operations_against_real_corpus()` returns `[]`
   today. The 2026-09-05 hand patch (`"reroll"` -> `"reroll-one"`/`"reroll-all"`) is why: it already
   fixed the one drift this check exists to catch, so today's corpus is clean. Had this run BEFORE
   that patch, it would have flagged `recipe.015`-`018`/`026`-`028` (`operation: "reroll"`) as
   outside the real vocabulary — exactly the mechanical catch the spec asks this module to prove.

2. **Corrected 2026-09-07 (was reported here as a real hard-reference gap; verified false):**
   `recipe.004`'s `outputRef` (`"item.plant-stem-b-001"`) DOES exist —
   `data/seed/items/base-types/plant-stem-b.json` (role `core-guard`, frame `plant`, band `b`).
   `base_type_id_exists()`'s original check only tried the role-id-based filename
   (`plant-core-guard-b.json`), which is genuinely absent — but the real, shipped file for that
   partition was authored under the display word (`frameRoleName: "stem"`), a filename
   inconsistency base-types-gen's own report already documented across the corpus's older,
   pre-generator content. Fixed by adding a frame-role-name-shaped filename fallback (read-only,
   never changes where a NEW entry gets written); `build_reference_reports()` now correctly
   reports zero unresolved `container` references against the real corpus — every `container`/
   `material` outputRef in the 30 entries resolves cleanly.

3. **FIXED 2026-09-07, later the same day, by a sibling module:** seven cost lines
   (`recipe.009`/`010`/`011`/`017`/`018`/`025`/`029`) named one of the four LEGACY shard ids
   (`shard.common`/`rare`/`epic`/`legendary`). `MaterialCatalog.IsLegacyShardId` was `true` for
   these, so `MaterialRecipeCatalog.Load` refused all seven at real C# import time
   (`MaterialUnissuableRule`) — matching that class's own doc comment, *"Refusals ... Never empty
   against today's shipped corpus"* (tasks/item-todo.md P4.1). `materialgen.vocab.require_issuable`
   already refuses a legacy id at GENERATION time (acceptance #1's own gate), so this generator
   could never reproduce the defect — but it also could never repair it, since it only mints NEW
   recipes, never re-authors the 30 pre-existing hand-authored ones. See sibling module
   `recipegen.migrate_legacy_shards` (detect + fix, mirroring `LegacyDemonRarityIds.ForwardMap`
   exactly), now applied to the real corpus — `build_reference_reports()`'s `cost_lines.unresolved`
   is `[]` today.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from seedsmith.pipeline.dependency_validator import (
    HARD,
    BackfillRequest,
    ReferenceManifestEntry,
    ValidationReport,
    plan_backfill,
    validate,
)
from seedsmith.pipeline.run_ledger import RunLedger

from ..basetypegen import emit as basetype_emit
from ..basetypegen import run as basetype_run
from ..basetypegen import tuning as basetype_tuning
from ..materialgen import vocab as material_vocab
from . import opvocab
from .brief import ForgeTarget, RecipeBrief, build_recipe_brief
from .emit import assemble_entry, emit_document, next_seq, write_document

REPO_ROOT = Path(__file__).resolve().parents[6]
RECIPES_CORPUS_PATH = REPO_ROOT / "data" / "seed" / "items" / "recipes" / "recipes.json"
DEFAULT_LEDGER_PATH = REPO_ROOT / "data" / "seed" / "items" / "_runs" / "recipes-gen.ledger.json"

CONTAINER_MANIFEST = [ReferenceManifestEntry("outputRef", HARD, "base-types-gen")]
MATERIAL_OUTPUT_MANIFEST = [ReferenceManifestEntry("outputRef", HARD, "materials-gen")]
COST_LINE_MANIFEST = [ReferenceManifestEntry("costLines[].material", HARD, "materials-gen")]

_TARGET_ID_RE = re.compile(r"^item\.(humanoid|plant)-(.+)-([a-z])-(\d{3,})$")


class BackfillTargetError(ValueError):
    """A missing container reference cannot be parsed back into a `(role, frame, band, seq)`
    partition request, or the parsed seq does not match the partition's own next-mintable id."""


# ---------------------------------------------------------------------------------------------
# Loading the real corpus
# ---------------------------------------------------------------------------------------------


def load_entries(path: "Path | None" = None) -> "dict[str, dict]":
    p = path or RECIPES_CORPUS_PATH
    if not p.exists():
        return {}
    doc = json.loads(p.read_text(encoding="utf-8"))
    return {e["id"]: e for e in doc.get("entries", []) if isinstance(e.get("id"), str)}


# ---------------------------------------------------------------------------------------------
# Acceptance #3 — reconcile `operation` against the real, current CraftOperations vocabulary
# ---------------------------------------------------------------------------------------------


def reconcile_operations_against_real_corpus(path: "Path | None" = None) -> "list[tuple[str, str]]":
    """`[]` today (see module docstring finding #1) — a non-empty result names exactly the entries
    whose `operation` a future C# rename left behind, the mechanical catch that would have caught
    2026-09-05's drift before it needed a hand patch."""
    entries = load_entries(path)
    return opvocab.reconcile_operations(dict(sorted(entries.items())))


# ---------------------------------------------------------------------------------------------
# Acceptance #5 — HARD reference validation, split by outputKind (see module docstring: `outputRef`
# means a DIFFERENT target module depending on a SIBLING field, which `dependency_validator`'s own
# single-target-per-field-path manifest cannot express directly — so this module validates each
# outputKind subset against its own manifest, rather than one manifest against every entry.)
# ---------------------------------------------------------------------------------------------


def base_type_id_exists(entry_id: str, *, base_types_dir: "Path | None" = None) -> bool:
    """True if `entry_id` (an `item.*` id) exists in base-types-gen's real, current corpus. Parses
    the id back into its owning `(role, frame, band)` partition via `naming.v1.json`'s own
    `item.<frame>-<frameRoleName>-<band>-<seq>` grammar, then reads that partition — never a
    full-corpus scan, matching how `basetypegen.run.load_existing` already loads one partition at
    a time. An id that does not even match the grammar, or whose frame-role-name maps to no real
    role, is simply not found — never an error, since a validator checking "does X exist" must
    treat "not even well-formed" as "no" and nothing else.

    ⭐ **Real, previously-undiscovered false positive, found and fixed 2026-09-07** (this session's
    own follow-up verification, going past this module's original report): `basetypegen.run.
    load_existing`'s own `_partition_file` builds exactly one filename,
    `{frame}-{role}-{band}.json` — but a real, shipped file this exact check needs to see,
    `data/seed/items/base-types/plant-stem-b.json` (role `core-guard`, containing the real, valid
    `item.plant-stem-b-001` `recipe.004`'s own `outputRef` names), was authored under the DISPLAY
    WORD (`stem`, `frame_role_name`) instead — a real, already-documented inconsistency
    `base-types-gen`'s own module (`tasks/item-seedgen-todo.md`, base-types-gen's real findings)
    found across the corpus's OLDER, pre-generator content: "filenames don't consistently follow
    either roleId or frameRoleName". The original single-filename check reported `recipe.004`'s real,
    existing target as missing. Fixed with a second, frame-role-name-shaped filename as a fallback —
    read-only, never changes which filename a NEW entry is written under (that stays
    `basetypegen`'s own `{frame}-{role}-{band}.json` convention going forward), only widens what
    counts as "found" when checking EXISTING, possibly inconsistently-named content.
    """
    m = _TARGET_ID_RE.match(entry_id)
    if not m:
        return False
    frame, frame_role_name, band, _seq = m.groups()
    role = _role_for_frame_role_name(frame, frame_role_name)
    if role is None:
        return False
    existing = basetype_run.load_existing(role, frame, band, base_types_dir=base_types_dir)
    if entry_id in existing:
        return True
    return entry_id in _load_by_frame_role_name(frame, frame_role_name, band, base_types_dir=base_types_dir)


def _load_by_frame_role_name(frame: str, frame_role_name: str, band: str, *,
                             base_types_dir: "Path | None" = None) -> "dict[str, dict]":
    """The fallback filename shape older, pre-generator content used:
    `{frame}-{frameRoleName}-{band}.json` (the display word, e.g. `plant-stem-b.json`), rather than
    `basetypegen`'s own forward-going `{frame}-{role}-{band}.json`. Read-only lookup, same shape as
    `basetypegen.run.load_existing`, deliberately not merged into that function since it must stay
    the single source of truth for where a NEW entry gets WRITTEN — this is a widened READ only."""
    directory = base_types_dir or basetype_tuning.BASE_TYPES_DIR
    p = directory / f"{frame}-{frame_role_name}-{band}.json"
    if not p.exists():
        return {}
    doc = json.loads(p.read_text(encoding="utf-8"))
    return {e["id"]: e for e in doc.get("entries", []) if isinstance(e.get("id"), str)}


def _role_for_frame_role_name(frame: str, frame_role_name: str) -> "str | None":
    for role_id, info in basetype_tuning.load_role_registry().items():
        if info.frame_role_name(frame) == frame_role_name:
            return role_id
    return None


@dataclass(frozen=True)
class ReferenceReports:
    container: ValidationReport
    material_output: ValidationReport
    cost_lines: ValidationReport

    @property
    def all_unresolved(self) -> "list":
        return [*self.container.unresolved, *self.material_output.unresolved,
                *self.cost_lines.unresolved]


def build_reference_reports(entries: "dict[str, dict]", *,
                            base_types_dir: "Path | None" = None) -> ReferenceReports:
    """Validates the reference manifest table from spec-recipes-gen.md, split by `outputKind`
    exactly as that table requires: `container` rows' `outputRef` against base-types-gen,
    `material` rows' `outputRef` against materials-gen's issuable vocabulary, and EVERY row's
    `costLines[].material` against that same issuable vocabulary. `mutation` rows carry no
    `outputRef` at all (real corpus confirmed), so they are simply absent from both outputRef
    subsets — `dependency_validator._extract` returns `None` for a missing field and `validate`
    skips it, so this needs no special-casing beyond the subset split itself."""
    container_entries = {k: v for k, v in entries.items() if v.get("outputKind") == "container"}
    material_entries = {k: v for k, v in entries.items() if v.get("outputKind") == "material"}

    container_report = validate(
        container_entries, CONTAINER_MANIFEST,
        resolve_hard=lambda _module, value: base_type_id_exists(value, base_types_dir=base_types_dir),
        resolve_categorical=lambda _module, _value: 0)
    material_output_report = validate(
        material_entries, MATERIAL_OUTPUT_MANIFEST,
        resolve_hard=lambda _module, value: material_vocab.is_issuable(value),
        resolve_categorical=lambda _module, _value: 0)
    cost_line_report = validate(
        entries, COST_LINE_MANIFEST,
        resolve_hard=lambda _module, value: material_vocab.is_issuable(value),
        resolve_categorical=lambda _module, _value: 0)

    return ReferenceReports(container=container_report, material_output=material_output_report,
                            cost_lines=cost_line_report)


def plan_container_backfill(entries: "dict[str, dict]", *,
                            base_types_dir: "Path | None" = None) -> "list[BackfillRequest]":
    """The exact `plan_backfill` contract acceptance #5 names: every missing `container` target,
    deduped, never touching `material`/`mutation` rows (those have no backfillable HARD reference
    into base-types-gen at all)."""
    reports = build_reference_reports(entries, base_types_dir=base_types_dir)
    return plan_backfill(reports.container)


def mint_forge_backfill(request: BackfillRequest, *, base_types_dir: "Path | None" = None,
                        ledger: "RunLedger | None" = None,
                        call: Callable[[str, dict], dict]) -> "dict[str, dict]":
    """Mints EXACTLY the missing base-type id a `plan_container_backfill` request names — the
    "--backfill triggers base-types-gen to mint that exact target first" half of acceptance #5.

    Parses the requested id back into its `(role, frame, band, seq)` partition, and refuses (rather
    than minting the wrong thing) if the requested seq is not the partition's own next-mintable
    one — this generator never asks base-types-gen to mint an ARBITRARY id, only the next one in
    sequence, so "exact target" is a real guarantee rather than a coincidence.
    """
    if request.target_module != "base-types-gen" or request.kind != HARD:
        raise BackfillTargetError(f"unexpected backfill request: {request}")
    missing_id = str(request.id_or_selector)
    m = _TARGET_ID_RE.match(missing_id)
    if not m:
        raise BackfillTargetError(f"{missing_id!r} does not match the item.<frame>-<slot>-<band>-<seq> grammar")
    frame, frame_role_name, band, seq_str = m.groups()
    role = _role_for_frame_role_name(frame, frame_role_name)
    if role is None:
        raise BackfillTargetError(
            f"{missing_id!r} names frame-role-name {frame_role_name!r}, which maps to no real "
            f"core.v1.json role on frame {frame!r}")

    existing = basetype_run.load_existing(role, frame, band, base_types_dir=base_types_dir)
    expected_seq = basetype_emit.next_seq(tuple(existing))
    if expected_seq != int(seq_str):
        raise BackfillTargetError(
            f"{missing_id!r} asks for seq {int(seq_str)}, but partition {role}:{frame}:{band}'s "
            f"own next-mintable seq is {expected_seq} — this generator only ever mints the next id "
            f"in sequence, never an arbitrary gap")

    ledger = ledger or RunLedger(DEFAULT_LEDGER_PATH.parent / "base-types-gen.backfill.ledger.json")
    plan = basetype_run.plan_run(role=role, frame=frame, band=band, count=1, ledger=ledger,
                                 base_types_dir=base_types_dir)
    fresh, blocked = basetype_run.run_draws(plan, ledger=ledger, call=call)
    if blocked:
        raise BackfillTargetError(f"backfill mint for {missing_id!r} was blocked: {blocked}")
    (minted_id, minted_entry), = fresh.items()
    if minted_id != missing_id:
        raise BackfillTargetError(
            f"backfill minted {minted_id!r}, not the requested {missing_id!r} — base-types-gen's "
            f"own sequence moved between planning and minting this backfill")
    basetype_run.write_corpus(role, frame, band, fresh, existing=plan.existing,
                              base_types_dir=base_types_dir)
    return {minted_id: minted_entry}


def run_container_backfill_loop(entries: "dict[str, dict]", *, base_types_dir: "Path | None" = None,
                                call: Callable[[str, dict], dict],
                                max_depth: int = 3) -> "tuple[list[BackfillRequest], list[BackfillRequest]]":
    """Mints every currently-missing container target, then RE-VALIDATES — matching
    `dependency_validator.run_backfill_loop`'s own cascade-guard discipline (a freshly-minted base
    type could in principle still fail to resolve if the mint itself is refused). Returns
    `(originally_requested, still_unresolved_after)`."""
    requested = plan_container_backfill(entries, base_types_dir=base_types_dir)
    for req in requested:
        mint_forge_backfill(req, base_types_dir=base_types_dir, call=call)
        if max_depth <= 0:
            break
    remaining = plan_container_backfill(entries, base_types_dir=base_types_dir)
    return requested, remaining


# ---------------------------------------------------------------------------------------------
# Open-ended generation — RunLedger-backed sequential draws (mirrors `materialgen.run`'s own
# "one ledger, upsert on reconcile/overwrite" discipline; recipe ids are corpus-sequential, not
# partitioned the way base-types-gen's role/frame/band triples are).
# ---------------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Subject:
    subject_id: str
    seq: int
    brief: "RecipeBrief"


@dataclass(frozen=True)
class RunPlan:
    subjects: "tuple[Subject, ...]"
    existing: "dict[str, dict]"


def _draw_prefix() -> str:
    return "recipe-draw-"


def plan_run(*, count: int, ledger: RunLedger, forge_target: "ForgeTarget | None" = None,
            theme_hint: str = "", recipes_path: "Path | None" = None) -> RunPlan:
    if count < 1:
        raise ValueError(f"count must be >= 1, got {count}")
    existing = load_entries(recipes_path)
    done = ledger.read_done()
    prefix = _draw_prefix()
    indices = [int(sid[len(prefix):]) for sid in done
              if sid.startswith(prefix) and sid[len(prefix):].isdigit()]
    start_draw = (max(indices) + 1) if indices else 0
    start_seq = next_seq(tuple(existing))

    subjects = tuple(
        Subject(subject_id=f"{prefix}{start_draw + i:03d}", seq=start_seq + i,
               brief=build_recipe_brief(forge_target=forge_target, theme_note=theme_hint,
                                        recipes_path=recipes_path))
        for i in range(count))
    return RunPlan(subjects=subjects, existing=existing)


def run_draws(plan: RunPlan, *, ledger: RunLedger,
             call: Callable[[str, dict], dict]) -> "tuple[dict[str, dict], dict[str, dict]]":
    """Mirrors `basetypegen.run.run_draws`: not all-or-nothing, each resolved draw marked done as it
    completes."""
    fresh: "dict[str, dict]" = {}
    blocked: "dict[str, dict]" = {}

    for subject in plan.subjects:
        answer = call(subject.brief.render(), dict(subject.brief.schema))
        if answer.get("blocked"):
            blocked[subject.subject_id] = {"reason": answer["blocked"]}
            continue
        entry = assemble_entry(answer, seq=subject.seq, forge_target=subject.brief.forge_target)
        fresh[entry["id"]] = entry
        ledger.mark_done(subject.subject_id, {"entryId": entry["id"], "operation": entry["operation"]})

    return fresh, blocked


def is_valid(subject_id: str, entry: dict, *, existing: "dict[str, dict]") -> bool:
    """The ledger's own reconcile hook — a "done" draw whose recorded entry id no longer exists, or
    whose `operation` was hand-edited out from under it, resurfaces as needing work."""
    entry_id = entry.get("entryId")
    if not entry_id or entry_id not in existing:
        return False
    return existing[entry_id].get("operation") == entry.get("operation")


def write_corpus(fresh: "dict[str, dict]", *, existing: "dict[str, dict] | None" = None,
                 recipes_path: "Path | None" = None, source_ref: str = "",
                 model: str = "seedsmith-recipegen", authored_utc: str = "") -> Path:
    """Additive: merges `fresh` into the existing corpus, matching every sibling module's
    'append+reconcile by default' contract."""
    p = recipes_path or RECIPES_CORPUS_PATH
    ex = existing if existing is not None else load_entries(recipes_path)
    merged = {**ex, **fresh}
    entries = [merged[k] for k in sorted(merged)]
    if p.exists():
        doc = json.loads(p.read_text(encoding="utf-8"))
        doc["entries"] = entries
    else:
        doc = emit_document(entries, batch="recipes-gen", source_ref=source_ref, model=model,
                            authored_utc=authored_utc)
    return write_document(p, doc)


# ---------------------------------------------------------------------------------------------
# CLI — mirrors `basetypegen.run.main`'s/`materialgen`'s own shape: `--dry-run` inspects the plan
# and makes no model calls; a live run REFUSES with no `call` wired in (this file has no transport
# of its own, the same division of labor every sibling module's `main()` states); default with no
# flag at all reconciles the real corpus's `operation` field against the current vocabulary
# (acceptance's own stated default: "reconcile the existing 30 against the current operation
# vocabulary").
# ---------------------------------------------------------------------------------------------


def main(argv=None) -> int:
    import argparse

    ap = argparse.ArgumentParser(
        description="Author or reconcile the crafting-recipe corpus (item-seedgen module 14).")
    ap.add_argument("--brief", default="", help="an optional theme-hint file")
    ap.add_argument("--dry-run", action="store_true", help="assemble briefs, make no model calls")
    ap.add_argument("--count", type=int, default=0,
                    help="how many new recipes to draw this run (0 == reconcile only, the default)")
    ap.add_argument("--write", action="store_true", help="write the merged corpus back to disk")
    ap.add_argument("--overwrite", "--force", default="",
                    help="comma-separated recipe ids to regenerate, or the literal 'all' "
                         "(--force is an accepted alias, matching content-completeness-core's "
                         "own naming convention -- RunLedger.force()/generate_commander_effects.py "
                         "--force)")
    ap.add_argument("--backfill", action="store_true",
                    help="mint any missing container (forge) target before writing")
    args = ap.parse_args(argv)

    ledger = RunLedger(DEFAULT_LEDGER_PATH)

    if args.overwrite:
        done = ledger.read_done()
        ids_needing_work = ledger.force(list(done), args.overwrite) if args.overwrite == "all" \
            else ledger.force([s.strip() for s in args.overwrite.split(",") if s.strip()], "ids")
        print(json.dumps({"overwrite": ids_needing_work}, ensure_ascii=False))
        return 0

    if args.count == 0:
        # The spec's own stated default: "reconcile the existing 30 against the current operation
        # vocabulary" — no model call needed, so this runs regardless of --dry-run/--write.
        drift = reconcile_operations_against_real_corpus()
        reports = build_reference_reports(load_entries())
        print(json.dumps({
            "operationDrift": [{"id": i, "operation": op} for i, op in drift],
            "unresolvedReferences": len(reports.all_unresolved),
        }, ensure_ascii=False, indent=2))
        return 0

    theme_hint = ""
    if args.brief:
        theme_hint = Path(args.brief).read_text(encoding="utf-8")

    if args.dry_run:
        plan = plan_run(count=args.count, ledger=ledger, theme_hint=theme_hint)
        print(json.dumps({"toGenerate": len(plan.subjects), "existingEntries": len(plan.existing)},
                         ensure_ascii=False, indent=2))
        if plan.subjects:
            print("--- sample brief ---")
            print(plan.subjects[0].brief.render())
        return 0

    raise SystemExit(
        "REFUSING TO RUN: no model `call` is wired into this CLI entrypoint yet — use --dry-run to "
        "inspect the plan, or drive `plan_run`/`run_draws` directly with an injected `call` "
        "(matching basetypegen.run.main's / materialgen's own stated boundary).")


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
