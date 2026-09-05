"""seedsmith.adapters.items.combogen.migrate — what retiring the `socket-word` corpus actually
touches, computed rather than transcribed.

> ✅ **RULED 2026-09-04: regenerate, do not retain.** The 25 legacy socket-words go; keeping them
> alongside the 102 takes the catalogue to 152 and deepens the learnability failure §4.4 named.
> ⚠ **`Registration/IngredientUnsatisfiable` must follow the kind**, or a `gates = True` check
> quietly stops gating.

⛔ **The rename is a BUNDLE, and only part of it is deterministic.** Five things move together:

| # | site | deterministic? |
|---|---|---|
| 1 | the gating metric's kind lookup (`metrics/linkage.py`) | ✅ **done** — it now reads BOTH ids |
| 2 | the Python `KindSpec` id/directory (`adapters/items/kinds.py`) | with (5) |
| 3 | the C# `KindCatalog` row (`tools/ItemSeedValidator/Registries/KindCatalog.cs`) | with (5) |
| 4 | `naming.v1.json`'s `idNamespaces.socketWords` — **`registryVersion 4, frozen: true`** | ⛔ ask-first |
| 5 | the 25 shipped entries themselves | ⛔ **model calls** |

Not one of (2)-(5) is separable without leaving the corpus worse than either endpoint: renaming the
kind over the legacy rows gives a `combination` kind whose every row fails its own required fields;
renaming the namespace without bumping the frozen registry breaks
`NamespaceAllocation.ByNamespace`; and deleting the 25 with nothing to replace them empties the only
input a `gates = True` metric has. So the bundle lands **with** the regeneration run, and this
module ships (1) — the half that removes the risk the ruling actually named — plus this analysis, so
the run is a `--write` away rather than a rediscovery.

`legality_report()` is the evidence for "regenerate, not retain": it measures the 25 against the
rules they would have to satisfy, and the answer is that **not one of them is a legal combination
today.**
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .tuning import ComboTuning

REPO_ROOT = Path(__file__).resolve().parents[6]
LEGACY_FILE = REPO_ROOT / "data" / "seed" / "items" / "socket-words" / "sockwords.json"

LEGACY_KIND = "socket-word"
TARGET_KIND = "combination"

#: The retired runtime-id prefix. D27 renames combination containers `combo.*`; `definitions.md` §1
#: forces the prefix to match the kind, so a `gem.word-*` runtime id is a combination wearing an
#: insert's prefix.
LEGACY_RUNTIME_PREFIX = "gem.word-"

#: Every file the bundle above touches, as (path, why). Asserted to EXIST by a test — a migration
#: plan naming a file that has since moved is worse than no plan, because it reads as done.
MIGRATION_SITES: "tuple[tuple[str, str], ...]" = (
    ("tools/seedsmith/seedsmith/metrics/linkage.py",
     "the gating metric Registration/IngredientUnsatisfiable — ✅ already follows both kind ids"),
    ("tools/seedsmith/seedsmith/adapters/items/kinds.py",
     "the KindSpec id/directory/namespace, and the 15-kind assertion that must still hold"),
    ("tools/seedsmith/seedsmith/planner/schedule.py",
     "the kind ordering used to schedule authoring waves"),
    ("tools/seedsmith/seedsmith/metrics/quality.py",
     "the never-seen-by-a-player kind list"),
    ("tools/ItemSeedValidator/Registries/KindCatalog.cs",
     "the C# port the Python KindSpec list mirrors — both move or the ports diverge"),
    ("data/seed/items/_registry/naming.v1.json",
     "idNamespaces.socketWords + its sockword.{seq:03} template — registryVersion 4, FROZEN: an "
     "ask-first bump, not an edit"),
    ("data/seed/items/socket-words/sockwords.json",
     "the 25 legacy entries themselves — regenerate, do not retain (RULED 2026-09-04)"),
)


@dataclass(frozen=True)
class LegacyEntry:
    id: str
    name: str
    runtime_id: str
    host_role: "str | None"
    host_frame: "str | None"
    min_sockets: int
    ingredient_families: "tuple[str, ...]"
    has_position: bool

    @property
    def ingredient_count(self) -> int:
        return len(self.ingredient_families)


@dataclass(frozen=True)
class LegalityReport:
    entries: "tuple[LegacyEntry, ...]"
    #: entry id -> every reason it is not a legal combination today
    problems: "dict[str, tuple[str, ...]]"

    @property
    def total(self) -> int:
        return len(self.entries)

    @property
    def legal(self) -> "tuple[str, ...]":
        return tuple(e.id for e in self.entries if not self.problems.get(e.id))

    @property
    def illegal(self) -> "tuple[str, ...]":
        return tuple(e.id for e in self.entries if self.problems.get(e.id))

    def to_dict(self) -> dict:
        return {
            "kind": LEGACY_KIND,
            "targetKind": TARGET_KIND,
            "entries": self.total,
            "legalAsCombinationsToday": len(self.legal),
            "illegal": len(self.illegal),
            "ruling": "regenerate, do not retain (2026-09-04)",
            "problemsByEntry": {k: list(v) for k, v in sorted(self.problems.items())},
        }


def load_legacy(path: "Path | None" = None) -> "tuple[LegacyEntry, ...]":
    doc = json.loads((path or LEGACY_FILE).read_text(encoding="utf-8"))
    if doc.get("kind") not in (LEGACY_KIND, TARGET_KIND):
        raise ValueError(
            f"{(path or LEGACY_FILE).name} declares kind {doc.get('kind')!r}, neither "
            f"{LEGACY_KIND!r} nor {TARGET_KIND!r}")
    out: "list[LegacyEntry]" = []
    for row in doc.get("entries", []):
        ingredients = row.get("ingredients") or []
        out.append(LegacyEntry(
            id=row["id"],
            name=row.get("name", row["id"]),
            runtime_id=row.get("runtimeId", ""),
            host_role=row.get("hostRole"),
            host_frame=row.get("hostFrame"),
            min_sockets=int(row.get("minSockets", 0)),
            ingredient_families=tuple(i.get("family", "") for i in ingredients),
            has_position=any("position" in i for i in ingredients),
        ))
    return tuple(out)


def legality_report(tuning: ComboTuning, *, host_roles: "tuple[str, ...]",
                    path: "Path | None" = None) -> LegalityReport:
    """Measure the 25 against the rules a combination must satisfy. Every reason, not the first."""
    entries = load_legacy(path)
    problems: "dict[str, tuple[str, ...]]" = {}
    for entry in entries:
        reasons: "list[str]" = []
        if entry.ingredient_count != tuning.ingredient_count:
            reasons.append(
                f"takes {entry.ingredient_count} ingredients, not D20's "
                f"{tuning.ingredient_count} (§2f.2)")
        if entry.has_position:
            reasons.append(
                "its ingredients carry `position` — D41 makes a recipe an unordered multiset and "
                "module 16's ComboIngredient has no position field")
        if entry.runtime_id.startswith(LEGACY_RUNTIME_PREFIX):
            reasons.append(
                f"its runtimeId {entry.runtime_id!r} uses the retired {LEGACY_RUNTIME_PREFIX!r} "
                f"spelling; D27 gives combinations the `combo.` prefix")
        if entry.host_role and entry.host_role not in host_roles:
            reasons.append(
                f"is hosted on {entry.host_role!r}, whose socket ceiling cannot reach "
                f"{tuning.ingredient_count} inserts — no item of that role could ever fire it")
        if entry.min_sockets != tuning.ingredient_count:
            reasons.append(
                f"declares minSockets {entry.min_sockets}; a {tuning.ingredient_count}-ingredient "
                f"recipe derives {tuning.ingredient_count}")
        if reasons:
            problems[entry.id] = tuple(reasons)
    return LegalityReport(entries=entries, problems=problems)


def missing_sites(root: "Path | None" = None) -> "list[str]":
    """Any file in `MIGRATION_SITES` that no longer exists. Empty is the healthy answer."""
    base = root or REPO_ROOT
    return [rel for rel, _ in MIGRATION_SITES if not (base / rel).exists()]
