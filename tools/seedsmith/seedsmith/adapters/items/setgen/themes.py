"""seedsmith.adapters.items.setgen.themes — the theme bridge, one-way.

⛔ **Demons publish; items consume; nothing here writes a demon** (`spec-demon-themes.md` §2.2). This
module opens `data/seed/demons/_registry/themes.v1.json` for reading and opens nothing under
`data/seed/demons/` for writing — `nothing_in_the_generator_writes_the_demons_corpus` asserts that
structurally rather than by promise.

Three populations of `themeKey`, collision-free by prefix:

| prefix | population | source |
|---|---|---|
| `theme.` | 13 legacy, 5 in use | `data/seed/items/_registry/themes.v1.json` (frozen) |
| `demon.` | one per species | `data/seed/demons/_registry/themes.v1.json` (published) |
| `build.` | 36, aptitude x archetype | `data/seed/items/_registry/build-themes.v1.json` (new, D-ruled 2026-09-04) |

⚠ **Two D34 preconditions are not met today, and this module reports them rather than working around
them.** `theme-refresh` (P0.2) and `theme-enrich` (P0.3) are both unbuilt: the registry still holds
**84** themes against **386** shipped species, and **31** are still at `basis = "name"`. A theme at
`basis = "name"` is *held*, never generated from — `generatable` excludes it and `holdback_report`
names the count, so an incomplete run reports `not_measured` instead of a green partial.

⚠ **Nothing here may key on a theme's `rarity`** (§2.4a): `RarityForRank` is proportional in `count`,
so a species moves tier as the roster grows *without moving rank*. A theme records the rarity it was
published against precisely so a later reader sees the drift instead of inheriting it. `rarity` is
carried through to provenance and read by nothing else.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[6]
DEMON_THEME_REGISTRY = REPO_ROOT / "data" / "seed" / "demons" / "_registry" / "themes.v1.json"
DEMON_SPECIES_ROOT = REPO_ROOT / "data" / "seed" / "demons" / "species"
BUILD_THEME_REGISTRY = REPO_ROOT / "data" / "seed" / "items" / "_registry" / "build-themes.v1.json"
LEGACY_THEME_REGISTRY = REPO_ROOT / "data" / "seed" / "items" / "_registry" / "themes.v1.json"

#: The basis a theme must have reached before it may be generated from. `theme-enrich` (P0.3) is the
#: stage that raises `name` to `text`; until it runs, name-basis themes are HELD.
GENERATABLE_BASES: "frozenset[str]" = frozenset({"text", "derived"})


@dataclass(frozen=True)
class Theme:
    """One row of whichever population it came from, normalised to what a brief needs."""

    theme_key: str
    species_id: "str | None"
    display_name: str
    motifs: "tuple[str, ...]"
    anti_motifs: "tuple[str, ...]"
    expression_item: str
    basis: str
    rarity: "str | None"
    retired: bool
    population: str          # "species" | "build" | "legacy"
    aptitude: "str | None" = None
    archetype: "str | None" = None

    @property
    def generatable(self) -> bool:
        return (not self.retired) and self.basis in GENERATABLE_BASES and bool(self.motifs)

    @property
    def hold_reason(self) -> "str | None":
        if self.retired:
            return "retired"
        if self.basis not in GENERATABLE_BASES:
            return f"basis={self.basis}"
        if not self.motifs:
            return "no motifs"
        return None


def load_species_themes(path: "Path | None" = None) -> "list[Theme]":
    doc = json.loads((path or DEMON_THEME_REGISTRY).read_text(encoding="utf-8"))
    out: "list[Theme]" = []
    for theme_key, row in sorted(doc["themes"].items()):
        out.append(Theme(
            theme_key=theme_key,
            species_id=row["speciesId"],
            display_name=row.get("displayName", ""),
            motifs=tuple(row.get("motifs") or ()),
            anti_motifs=tuple(row.get("antiMotifs") or ()),
            expression_item=(row.get("expression") or {}).get("item", ""),
            basis=row.get("basis", ""),
            rarity=row.get("rarity"),
            retired=bool(row.get("retired")),
            population="species",
        ))
    return out


def load_build_themes(path: "Path | None" = None) -> "list[Theme]":
    doc = json.loads((path or BUILD_THEME_REGISTRY).read_text(encoding="utf-8"))
    return [
        Theme(
            theme_key=row["themeKey"], species_id=None, display_name=row["displayName"],
            motifs=tuple(row.get("motifs") or ()), anti_motifs=tuple(row.get("antiMotifs") or ()),
            expression_item=(row.get("expression") or {}).get("item", ""),
            basis=row.get("basis", ""), rarity=None, retired=bool(row.get("retired")),
            population="build", aptitude=row.get("aptitude"), archetype=row.get("archetype"),
        )
        for row in doc["themes"]
    ]


def build_theme_keys(path: "Path | None" = None) -> "frozenset[str]":
    doc = json.loads((path or BUILD_THEME_REGISTRY).read_text(encoding="utf-8"))
    return frozenset(row["themeKey"] for row in doc["themes"])


def legacy_theme_ids(path: "Path | None" = None) -> "frozenset[str]":
    """The bare ids of the legacy `theme.*` population — what a generated id must not collide with."""
    doc = json.loads((path or LEGACY_THEME_REGISTRY).read_text(encoding="utf-8"))
    return frozenset(t["id"] for t in doc["themes"])


def legacy_partition_ids(naming_path: "Path | None" = None) -> "frozenset[str]":
    """The FIVE `themeId`s `naming.v1.json` actually pinned as set partitions — a tighter set than
    the 13 registered legacy themes, and the one a generated set id is checked against."""
    path = naming_path or (REPO_ROOT / "data" / "seed" / "items" / "_registry" / "naming.v1.json")
    doc = json.loads(path.read_text(encoding="utf-8"))
    return frozenset(doc["idNamespaces"]["sets"]["themeIds"])


def generatable(themes: "list[Theme]") -> "list[Theme]":
    return [t for t in themes if t.generatable]


@dataclass(frozen=True)
class HoldbackReport:
    """What a run may generate, and precisely what it may not — the input to the run verdict.

    A run that generated 53 of 84 themes is **not** a pass. It is `not_measured` for the held
    population, which is the honest answer and the one this module must not launder into green.
    """

    total: int
    generatable: int
    held: "tuple[tuple[str, str], ...]"     # (themeKey, reason)

    @property
    def complete(self) -> bool:
        return not self.held

    def held_by_reason(self) -> "dict[str, int]":
        counts: "dict[str, int]" = {}
        for _, reason in self.held:
            counts[reason] = counts.get(reason, 0) + 1
        return counts


def holdback_report(themes: "list[Theme]") -> HoldbackReport:
    held = tuple((t.theme_key, t.hold_reason) for t in themes if t.hold_reason)
    return HoldbackReport(total=len(themes), generatable=len(themes) - len(held), held=held)


def shipped_species_ids(root: "Path | None" = None) -> "frozenset[str]":
    """Every species the anchor tree actually ships, from its own `_index.json` when present and
    from the on-disk files otherwise. Read so the 84-vs-386 staleness cannot recur silently."""
    base = root or DEMON_SPECIES_ROOT
    index = base / "_index.json"
    if index.exists():
        doc = json.loads(index.read_text(encoding="utf-8"))
        ids = _species_ids_from_index(doc)
        if ids:
            return ids
    return frozenset(p.stem for p in base.glob("*/*.json") if not p.stem.startswith("_"))


def _species_ids_from_index(doc) -> "frozenset[str]":
    """The index shape is the demons feature's, not ours — read defensively rather than assume it,
    and fall back to the tree when it is a shape this module does not recognise.

    ⛔ **The shape that matters, and the one a filename count gets wrong.** `_index.json` is a flat
    `{speciesId: "plant/family.json"}` map — the *files* under `species/` are FAMILY files holding
    many species each, so `ls data/seed/demons/species/{plant,zombie} | wc -l` counts families, not
    species. Measured 2026-09-04: **496 family files, 840 species.** Anything comparing the theme
    registry against the file count is comparing against the wrong denominator.
    """
    if isinstance(doc, dict):
        for key in ("species", "entries", "anchors"):
            rows = doc.get(key)
            if isinstance(rows, list):
                out = {r.get("speciesId") or r.get("id") for r in rows if isinstance(r, dict)}
                return frozenset(s for s in out if s)
            if isinstance(rows, dict):
                return frozenset(rows)
        # The real, shipped shape: speciesId -> relative family path.
        if doc and all(isinstance(v, str) for v in doc.values()):
            return frozenset(doc)
    return frozenset()


def species_family_file_count(root: "Path | None" = None) -> int:
    """The count a naive `ls | wc -l` produces — kept as its own function precisely so a test can
    pin that it is NOT the species count, and the two can never be confused again."""
    base = root or DEMON_SPECIES_ROOT
    return sum(1 for p in base.glob("*/*.json") if not p.stem.startswith("_"))


@dataclass(frozen=True)
class CoverageReport:
    """⛔ D34: the theme registry must cover every shipped species. 84 vs 386 was a stale snapshot of
    a GENERATED corpus quoted as a design proportion — the defect this report exists to make loud."""

    species: int
    themes: int
    uncovered: "tuple[str, ...]"
    orphaned: "tuple[str, ...]"    # themes naming a species the tree no longer ships

    @property
    def complete(self) -> bool:
        return not self.uncovered and not self.orphaned


def coverage_report(themes: "list[Theme]", species_root: "Path | None" = None) -> CoverageReport:
    species = shipped_species_ids(species_root)
    covered = {t.species_id for t in themes if t.species_id}
    lowered = {s.lower() for s in species}
    covered_lower = {c.lower() for c in covered}
    return CoverageReport(
        species=len(species), themes=len(themes),
        uncovered=tuple(sorted(s for s in species if s.lower() not in covered_lower)),
        orphaned=tuple(sorted(c for c in covered if c.lower() not in lowered)),
    )
