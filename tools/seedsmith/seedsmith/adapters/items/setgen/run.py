"""seedsmith.adapters.items.setgen.run — the run plan, the resume ledger, and the dry run.

⚠ **What this module does and does not do.** It assembles the whole run *deterministically*: which
themes are in, which are held and why, the brief for each, the id each entry will take, and the
ledger that makes an interrupted run resumable. The **model call itself is not made here** — the
graph that makes it is the same `workflow` package `effects generate` and `demons generate` use, and
this module hands it a subject list. A `--dry-run` therefore exercises everything except the call,
which is what makes the run inspectable before a token is spent.

⛔ **Resume is not optional at ~1,800 entries.** The ledger is a single JSON file keyed by subject
id, written after each subject completes. `plan_run` reads it and returns only the subjects not
already done — so re-running is idempotent and an interrupt costs the work in flight, not the run.
The demon harness's own resume path holds a real atomic file lock; this ledger reuses that
discipline by writing through a temporary file and replacing, so a killed process cannot leave a
half-written ledger behind.
"""
from __future__ import annotations

import json
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path

from . import brief as brief_mod
from . import emit, themes as themes_mod
from .themes import Theme
from .tuning import SetCharmGenTuning
from .vocab import Vocabulary

REPO_ROOT = Path(__file__).resolve().parents[6]
DEFAULT_LEDGER = REPO_ROOT / "data" / "seed" / "items" / "_runs" / "set-charm-gen.ledger.json"


@dataclass(frozen=True)
class Subject:
    """One unit of work: one theme, one kind, one id already decided."""

    subject_id: str
    kind: str               # "set" | "charm"
    population: str         # "species" | "build"
    theme_key: str
    entry_id: str
    brief: str

    def to_dict(self) -> dict:
        return {"subjectId": self.subject_id, "kind": self.kind, "population": self.population,
                "themeKey": self.theme_key, "entryId": self.entry_id}


@dataclass
class RunPlan:
    subjects: "list[Subject]"
    held: "list[tuple[str, str]]"
    already_done: "list[str]"

    @property
    def complete(self) -> bool:
        """A plan with a held partition is NOT complete, and the run verdict must reflect that."""
        return not self.held

    def summary(self) -> dict:
        by_reason: "dict[str, int]" = {}
        for _, reason in self.held:
            by_reason[reason] = by_reason.get(reason, 0) + 1
        return {"toGenerate": len(self.subjects), "alreadyDone": len(self.already_done),
                "held": len(self.held), "heldByReason": by_reason, "complete": self.complete}


def read_ledger(path: "Path | None" = None) -> "dict[str, dict]":
    ledger_path = path or DEFAULT_LEDGER
    if not ledger_path.exists():
        return {}
    doc = json.loads(ledger_path.read_text(encoding="utf-8"))
    return dict(doc.get("done") or {})


def write_ledger(done: "dict[str, dict]", path: "Path | None" = None) -> Path:
    """Atomic replace. A killed process leaves either the old ledger or the new one, never half of
    one — which is the difference between a resumable run and a corrupt one."""
    ledger_path = path or DEFAULT_LEDGER
    ledger_path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps({"schemaVersion": 1, "done": done}, ensure_ascii=False, indent=2) + "\n"
    handle, tmp_name = tempfile.mkstemp(dir=str(ledger_path.parent), suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp_name, ledger_path)
    except BaseException:
        Path(tmp_name).unlink(missing_ok=True)
        raise
    return ledger_path


def plan_run(*, kind: str, population: str, tuning: SetCharmGenTuning, vocabulary: Vocabulary,
             species_themes: "list[Theme] | None" = None,
             build_themes: "list[Theme] | None" = None,
             ledger: "dict[str, dict] | None" = None,
             legacy_partitions: "frozenset[str] | None" = None) -> RunPlan:
    if kind not in ("set", "charm"):
        raise ValueError(f"kind must be 'set' or 'charm', got {kind!r}")
    if population not in ("species", "build"):
        raise ValueError(f"population must be 'species' or 'build', got {population!r}")
    if kind == "charm" and population == "build":
        # Charms are per species by D12's own grid; there is no build charm population, and
        # inventing one here would be a design decision wearing a CLI flag.
        raise ValueError("there is no build charm population — charms are one per species")

    pool = (species_themes if species_themes is not None else themes_mod.load_species_themes()) \
        if population == "species" else \
        (build_themes if build_themes is not None else themes_mod.load_build_themes())
    partitions = (legacy_partitions if legacy_partitions is not None
                  else themes_mod.legacy_partition_ids())
    done = ledger if ledger is not None else read_ledger()

    subjects: "list[Subject]" = []
    already: "list[str]" = []
    report = themes_mod.holdback_report(pool)

    for theme in pool:
        if theme.hold_reason:
            continue
        subject_id = f"{kind}-{population}-{theme.theme_key}"
        if subject_id in done:
            already.append(subject_id)
            continue
        entry_id = _entry_id(kind, population, theme, partitions)
        text = (brief_mod.build_set_brief(theme, tuning, vocabulary) if kind == "set"
                else brief_mod.build_charm_brief(theme, tuning, vocabulary))
        subjects.append(Subject(subject_id=subject_id, kind=kind, population=population,
                                theme_key=theme.theme_key, entry_id=entry_id, brief=text))

    return RunPlan(subjects=subjects, held=list(report.held), already_done=already)


def _entry_id(kind: str, population: str, theme: Theme,
              partitions: "frozenset[str]") -> str:
    if kind == "charm":
        # The axis group is not known until the model picks the axis, so a charm's id is minted at
        # persist time, not at plan time. Recording the species keeps the plan readable without
        # pretending to know an id it cannot know yet.
        return f"charm.(axis-group)-NNN for {theme.species_id}"
    if population == "build":
        return emit.build_set_id(theme.aptitude or "", theme.archetype or "", 1)
    return emit.set_id(theme.species_id or "", 1, legacy_partitions=partitions)
