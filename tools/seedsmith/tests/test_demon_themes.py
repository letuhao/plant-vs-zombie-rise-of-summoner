"""Tests for seedsmith.adapters.demons.themes and the items registries.py bridge
(spec-demon-themes.md, wave D4).
"""
from __future__ import annotations

from pathlib import Path

from seedsmith.adapters.demons.themes import (
    THEME_PREFIX,
    DemonThemeInput,
    build_theme_registry,
)
from seedsmith.adapters.items.registries import load_theme_keys, load_vocabularies
from seedsmith.corpus.model import Corpus

LIVE_ITEMS_ROOT = Path(__file__).resolve().parents[3] / "data" / "seed" / "items"


def T(species_id: str, basis: str = "text", rarity: str = "common", retired: bool = False) -> DemonThemeInput:
    return DemonThemeInput(
        species_id=species_id, display_name=species_id, rarity=rarity,
        motifs=("nut", "shell"), anti_motifs=("fire",), basis=basis, retired=retired,
    )


# ---- Publishing rules -------------------------------------------------------------------------


def test_a_demon_with_motifs_publishes_a_theme_carrying_everything():
    registry = build_theme_registry([T("d0", basis="text")])
    theme = registry["demon.d0"]
    assert theme.motifs == ("nut", "shell")
    assert theme.anti_motifs == ("fire",)
    assert theme.expression == {"item": "material and form — what it is made of, what shape it takes",
                                "action": "tempo and effect shape — how fast, how it lands"}
    assert theme.basis == "text"
    assert theme.retired is False


def test_a_blocked_demon_publishes_no_theme():
    registry = build_theme_registry([T("d0", basis="blocked")])
    assert "demon.d0" not in registry
    assert registry == {}


def test_a_name_basis_demon_publishes_a_theme_marked_as_such():
    registry = build_theme_registry([T("d0", basis="name")])
    assert registry["demon.d0"].basis == "name"


def test_every_published_theme_carries_expression_rules_structurally():
    # No code path in build_theme_registry can produce a PublishedTheme without EXPRESSION_RULES —
    # asserted directly rather than left implicit, so "a theme without expression rules fails
    # validation" stays true even if a future edit adds a second construction site.
    registry = build_theme_registry([T("d0"), T("d1", basis="name")])
    for theme in registry.values():
        assert theme.expression, f"{theme.theme_key} published with no expression rules"
        assert set(theme.expression) == {"item", "action"}


def test_theme_id_is_always_demon_prefixed():
    registry = build_theme_registry([T("d0")])
    assert next(iter(registry)).startswith(THEME_PREFIX)
    assert THEME_PREFIX == "demon."


# ---- Roster churn — retire, never delete (§2.4a) -----------------------------------------------


def test_a_demon_that_leaves_the_roster_is_retired_not_deleted():
    first = build_theme_registry([T("d0"), T("d1")])
    second = build_theme_registry([T("d1")], existing_registry=first)  # d0 no longer in the roster
    assert "demon.d0" in second
    assert second["demon.d0"].retired is True
    assert second["demon.d1"].retired is False


def test_a_retired_theme_is_still_resolvable_with_its_original_data():
    first = build_theme_registry([T("d0", rarity="legendary")])
    second = build_theme_registry([], existing_registry=first)
    retired = second["demon.d0"]
    assert retired.retired is True
    assert retired.rarity == "legendary"  # the rarity it was PUBLISHED against, unchanged
    assert retired.motifs == ("nut", "shell")


def test_republishing_never_recomputes_an_already_published_theme():
    first = build_theme_registry([T("d0", rarity="common")])
    # Same demon, DIFFERENT rarity this run — append-only means the theme does not silently update.
    second = build_theme_registry([T("d0", rarity="legendary")], existing_registry=first)
    assert second["demon.d0"].rarity == "common", "a published theme is a snapshot, never re-derived"


def test_rarity_recorded_is_what_the_theme_was_published_against():
    registry = build_theme_registry([T("d0", rarity="epic")])
    assert registry["demon.d0"].rarity == "epic"


# ---- Direction is one-way (§2.2) ---------------------------------------------------------------


def test_themes_module_never_imports_from_the_items_adapter():
    import seedsmith.adapters.demons.themes as mod
    source = Path(mod.__file__).read_text(encoding="utf-8")
    assert "adapters.items" not in source
    assert "from ..items" not in source and "from ...items" not in source


# ---- Items-side bridge: the ONE permitted file outside adapters/demons/ -----------------------


def test_load_theme_keys_returns_the_thirteen_registered_legacy_themes_prefixed():
    """⭐ Updated 2026-09-04 (item module 13, `set-charm-gen`): a THIRD append-only population,
    `build.*`, joined the union — 36 keys, 12 aptitudes x 3 archetypes, in
    `data/seed/items/_registry/build-themes.v1.json`. A `set` requires a `themeKey` and the build
    set families belong to no species, so without it a build set is unauthorable; ruled as a third
    namespace rather than a loosened `themeKey`, because making it *required* on `unique` is the
    intended direction (`spec-demon-themes.md` 7) and loosening it here would reverse that.

    The thirteen legacy themes are still asserted exactly — this test's original subject — and the
    new population is asserted alongside rather than the count being loosened to `>= 13`."""
    keys = load_theme_keys()
    legacy = {k for k in keys if k.startswith("theme.")}
    build = {k for k in keys if k.startswith("build.")}
    assert len(legacy) == 13
    assert len(build) == 36
    assert legacy | build == keys
    assert "theme.rot-bloom" in keys
    assert "theme.rusted-legion" in keys
    assert "build.might-offense" in keys


def test_theme_key_vocabulary_is_the_union_and_prefixes_cannot_collide():
    demon_keys = frozenset({"demon.wall-nut-zombie", "demon.tall-nut-zombie"})
    vocab = load_vocabularies(demon_theme_keys=demon_keys)["themeKey"]
    assert "theme.rot-bloom" in vocab
    assert "demon.wall-nut-zombie" in vocab
    assert not (frozenset(k for k in vocab if k.startswith("theme.")) &
               frozenset(k for k in vocab if k.startswith("demon.")))


def test_a_key_in_neither_population_is_illegal():
    from seedsmith.adapters.base import RegistrySet
    vocab = load_vocabularies()
    registries = RegistrySet(vocabularies=vocab)
    assert registries.is_legal("themeKey", "theme.rot-bloom")
    assert not registries.is_legal("themeKey", "not-a-real-theme")
    assert not registries.is_legal("themeKey", "demon.unpublished")  # legal only once unioned in


def test_a_demon_key_becomes_legal_once_unioned_in():
    from seedsmith.adapters.base import RegistrySet
    vocab = load_vocabularies(demon_theme_keys=frozenset({"demon.wall-nut-zombie"}))
    registries = RegistrySet(vocabularies=vocab)
    assert registries.is_legal("themeKey", "demon.wall-nut-zombie")
    assert registries.is_legal("themeKey", "theme.rot-bloom")  # legacy still works alongside it


# ---- The decisive test: real live corpus, not a fixture ----------------------------------------


def test_all_existing_live_themed_entries_still_validate():
    """spec-demon-themes.md's own words: 'the row that decides whether this ships.' Measured
    2026-08-31 against the real corpus: 30 sets + 8 uniques = 38 themed entries, all `theme.*`."""
    corpus = Corpus.load(LIVE_ITEMS_ROOT)
    themed = [e for e in corpus.entries.values() if e.get("themeKey")]
    assert len(themed) == 38, "the corpus moved — re-measure before trusting this test's count"

    vocab = load_vocabularies()  # no demon keys unioned in — proves legacy alone still suffices
    from seedsmith.adapters.base import RegistrySet
    registries = RegistrySet(vocabularies=vocab)
    broken = [e.id for e in themed if not registries.is_legal("themeKey", e.get("themeKey"))]
    assert broken == [], f"turning themeKey into a closed vocabulary broke: {broken}"


def test_live_theme_keys_used_are_a_subset_of_the_five_known_ones():
    corpus = Corpus.load(LIVE_ITEMS_ROOT)
    used = {e.get("themeKey") for e in corpus.entries.values() if e.get("themeKey")}
    assert used == {"theme.frostbitten-vanguard", "theme.rusted-legion", "theme.sunwoven-almanac",
                    "theme.thorned-chassis", "theme.verdant-graft"}


# ---- Cross-artifact consistency (added 2026-09-01 after a real, undetected staleness) -----------


def test_published_themes_carry_the_current_motifs():
    """⛔ Real defect this pins. `themes.v1.json` EMBEDS each demon's motifs. When G1
    (`motif-prose-filter`) changed every demon's motifs, all 84 themes silently went stale — still
    carrying the pre-filter stat vocabulary (`一类` = "armour-class one", `三线`, `伤害`) while
    `motif-assignments.json` had moved on. **No metric compared the two**, so nothing noticed.

    Consistency between a derived artifact and the artifact it was derived FROM is exactly the kind
    of thing that rots without a test."""
    import json

    root = LIVE_ITEMS_ROOT.parent / "demons"
    themes = json.loads((root / "_registry" / "themes.v1.json").read_text(encoding="utf-8"))["themes"]
    motifs = json.loads(
        (root / "_generated" / "motif-assignments.json").read_text(encoding="utf-8"))

    stale = []
    for key, rec in themes.items():
        sid = rec["speciesId"]
        if sid in motifs and rec["motifs"] != motifs[sid]["motifs"]:
            stale.append(sid)
    assert stale == [], (
        f"{len(stale)} theme(s) carry motifs that no longer match motif-assignments.json — "
        f"re-run `python -m seedsmith.adapters.demons.generate_themes --rebuild`: {stale[:5]}")


def test_every_theme_carries_both_expression_rules_and_a_rarity_snapshot():
    import json

    root = LIVE_ITEMS_ROOT.parent / "demons"
    themes = json.loads((root / "_registry" / "themes.v1.json").read_text(encoding="utf-8"))["themes"]
    assert themes, "the theme registry is empty"
    for key, rec in themes.items():
        assert key.startswith("demon."), f"{key} is not demon-prefixed"
        assert set(rec["expression"]) == {"item", "action"}, key
        assert rec["rarity"], f"{key} has no rarity snapshot"
        assert rec["basis"] in ("text", "name"), key
