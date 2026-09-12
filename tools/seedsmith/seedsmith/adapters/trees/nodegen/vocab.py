"""seedsmith.adapters.trees.nodegen.vocab — the affix pick vocabulary, COUNTED fresh (task H1,
spec-tree-language.md §3 rows 11-12, §5.1); `permitted_for_branch` is task H3's own addition.

⛔ Same standing rule `items/setgen/vocab.py` states for its own vocabulary: **never derive a
design proportion from a snapshot of a generated corpus — count it, or don't quote it.** D22 (§3):
"Every effect is an affix from the shipped catalog. No passive-specific effect vocabulary exists in
this module" — so this module reads the SAME `data/seed/items/affix-families/*.json` the item
program already ships, rather than authoring a tree-specific effect library.

Counted 2026-09-12 (§3 row 11-12, §5.1): **125 families**, **seven** distinct `tags` values —
`offensive` 58, `defensive` 47, `utility` 19, `sturdy` 5, `arcane` 4, `mechanical` 3, `metal` 2
(112 families carry one tag, 13 carry more than one). Only the first three are branch-shaped; the
other four are category tags, which is the honest finding §5.1 records: the tag vocabulary is not
enough to key an exclusion predicate on by itself, which is why `exclusion.py` keys on the plan's own
`propertyVocabulary` instead of these tags.

**`permitted_for_branch` is the narrowest cut this stage can honestly make today.** A node's
`QuotaCell` (`quota.py`, task H3) allocates a `trigger`/`element`/`status`/`channelFamily` — the
full §4.2 design narrows `affixIds`'s own enum to the affixes whose OWN bound atoms match that
cell on every axis. That cross-reference needs a per-affix atom-tag registry which does not exist
yet (§5.1's own "Blocked on other work" finding: "until [it] lands... a predicate can key on
`posture` and nothing else" — the same blocker, one level up, on affix selection rather than
exclusion). `permitted_for_branch` uses the ONE real, unblocked signal this vocabulary carries
today — `offensive`/`defensive`/`utility` tags, matched against the node's own branch — so the
brief's `affixIds` enum is never empty while the finer six-axis cut waits on that registry. This is
named here as a wiring gap, not an architectural wall (`CLAUDE.md`'s own rule): the day the
registry lands, this function's body is the one place that gets narrower, its signature unchanged.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[6]
AFFIX_FAMILY_DIR = REPO_ROOT / "data" / "seed" / "items" / "affix-families"


class AffixVocabularyError(ValueError):
    """The affix corpus is structurally unusable — refused, never silently narrowed."""


@dataclass(frozen=True)
class AffixOption:
    """One `affixIds` choice a node's brief may offer. `one_line` is the brief's own no-numbers
    identity line — the affix's shipped `name`, never its `displayTemplate` (that field embeds a
    `{value}%`-shaped magnitude placeholder, which has no business in a schema-adjacent brief)."""

    affix_id: str
    name: str
    tags: "tuple[str, ...]"
    kind_id: str

    @property
    def one_line(self) -> str:
        return f"{self.affix_id} — {self.name}"


@dataclass(frozen=True)
class AffixVocabulary:
    options: "tuple[AffixOption, ...]"

    @property
    def count(self) -> int:
        return len(self.options)

    def ids(self) -> "tuple[str, ...]":
        return tuple(o.affix_id for o in self.options)

    def by_tag(self, tag: str) -> "tuple[AffixOption, ...]":
        return tuple(o for o in self.options if tag in o.tags)

    def tag_counts(self) -> "dict[str, int]":
        counts: "dict[str, int]" = {}
        for o in self.options:
            for t in o.tags:
                counts[t] = counts.get(t, 0) + 1
        return counts

    def get(self, affix_id: str) -> AffixOption:
        for o in self.options:
            if o.affix_id == affix_id:
                return o
        raise AffixVocabularyError(f"unknown affix id {affix_id!r} — not in the loaded corpus")

    def permitted_for_branch(self, branch: str) -> "tuple[AffixOption, ...]":
        """§4.2 step 6's affix-side cut, at the ONE granularity today's tag vocabulary supports:
        `branch`'s own tag (`offensive` or `defensive`) plus `utility` (fits either branch) —
        never the finer six-axis `QuotaCell` cut, which needs the not-yet-built atom-tag registry
        (this module's own docstring). Held, never widened, if nothing matches: an empty result
        means the corpus has no affix tagged for this branch at all, which is a vocabulary defect
        to surface, not paper over with the whole 125-family list."""
        if branch not in ("offensive", "defensive"):
            raise ValueError(f"permitted_for_branch: branch must be 'offensive' or 'defensive', "
                             f"got {branch!r}")
        return tuple(o for o in self.options if branch in o.tags or "utility" in o.tags)


def load_affix_families(directory: "Path | None" = None) -> "list[dict]":
    """Every shipped affix-family entry, read fresh. Mirrors
    `items/setgen/vocab.py:load_families` exactly — same directory, same `kind` filter — because
    this is the SAME corpus, not a tree-specific copy of it."""
    entries: "list[dict]" = []
    for path in sorted((directory or AFFIX_FAMILY_DIR).glob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        if doc.get("kind") != "affix-family":
            continue
        entries.extend(doc.get("entries", []))
    return entries


def build(directory: "Path | None" = None) -> AffixVocabulary:
    options = tuple(
        AffixOption(
            affix_id=entry["id"],
            name=str(entry["name"]),
            tags=tuple(entry.get("tags") or ()),
            kind_id=str(entry["kindId"]),
        )
        for entry in load_affix_families(directory)
    )
    if not options:
        raise AffixVocabularyError(
            f"no affix-family entries found under {(directory or AFFIX_FAMILY_DIR)} — "
            f"held, never widened to an empty-but-legal vocabulary")
    return AffixVocabulary(options=options)
