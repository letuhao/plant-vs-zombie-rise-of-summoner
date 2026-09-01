"""Tests for seedsmith.adapters.demons.motifs (spec-motif-derive.md, wave D2)."""
from __future__ import annotations

import dataclasses

from seedsmith.adapters.demons.motifs import (
    DemonMotifInput,
    DerivedMotifs,
    FamilyMembership,
    derive_motifs,
    own_motifs,
)


def FM(family_id: str, basis: str = "text") -> FamilyMembership:
    return FamilyMembership(family_id=family_id, basis=basis)


def D(species_id: str, name: str = "Demon", flavor_text: "str | None" = None,
      families: tuple = ()) -> DemonMotifInput:
    return DemonMotifInput(species_id=species_id, name=name, flavor_text=flavor_text, families=families)


# ---- Real defect, real model, 2026-08-31: whole Chinese clauses were not word tokens -----------


def test_chinese_flavor_text_produces_real_short_words_not_whole_clauses():
    """The exact bug from the first real run: a punctuation-free Chinese clause is not one word,
    it is an un-segmented sentence. Before switching to jieba, `own_motifs` returned the entire
    clause below as a single "motif" — unusable as shared vocabulary."""
    text = "以下能防止爆炸樱桃产生溅射，可以用于保护其他植物，坚果有坚硬的外壳，融合后保留该特点。"
    tokens, basis = own_motifs(flavor_text=text, name="X")
    assert basis == "text"
    for t in tokens:
        assert len(t) <= 4, f"token {t!r} is not a short word — segmentation regressed to whole-clause capture"
    # A real content word from that clause must survive; common function words must not.
    assert any(t in ("樱桃", "坚果", "外壳", "植物", "融合") for t in tokens)
    assert "以下" not in tokens and "可以" not in tokens and "该" not in tokens


# ---- own_motifs -------------------------------------------------------------------------------


def test_own_motifs_prefers_flavor_text_and_reports_basis_text():
    tokens, basis = own_motifs(flavor_text="A powerful nut defender wields ancient shells.", name="X")
    assert basis == "text"
    assert tokens


def test_own_motifs_falls_back_to_name_when_no_flavor_text():
    tokens, basis = own_motifs(flavor_text=None, name="Wallnut Guardian")
    assert basis == "name"
    assert tokens


def test_own_motifs_blocked_when_neither_text_nor_usable_name():
    tokens, basis = own_motifs(flavor_text=None, name="")
    assert tokens == []
    assert basis == "blocked"


# ---- Determinism --------------------------------------------------------------------------------


def test_same_corpus_derived_twice_is_byte_identical():
    demons = [
        D("a", flavor_text="Ancient nut defender.", families=(FM("nut"),)),
        D("b", flavor_text="Younger nut sentry.", families=(FM("nut"),)),
        D("c", flavor_text="Lone wanderer with no kin."),
    ]
    first = derive_motifs(demons)
    second = derive_motifs(list(reversed(demons)))
    assert first == second


# ---- Inheritance ----------------------------------------------------------------------------


def test_demon_in_two_families_inherits_from_both():
    demons = [
        D("host", families=(FM("nut"), FM("shell"))),
        D("nut-a", flavor_text="Nut power surges within.", families=(FM("nut"),)),
        D("shell-a", flavor_text="Shell armor gleams brightly.", families=(FM("shell"),)),
    ]
    result = derive_motifs(demons)
    host = result["host"]
    nut_pool_tokens = set(own_motifs(flavor_text="Nut power surges within.", name="x")[0])
    shell_pool_tokens = set(own_motifs(flavor_text="Shell armor gleams brightly.", name="x")[0])
    assert set(host.motifs) & nut_pool_tokens
    assert set(host.motifs) & shell_pool_tokens


def test_demon_in_no_family_with_text_derives_from_own_text_basis_text():
    demons = [D("solo", flavor_text="A powerful lone wanderer.")]
    result = derive_motifs(demons)
    assert result["solo"].motifs
    assert result["solo"].basis == "text"


def test_demon_in_no_family_no_text_is_blocked_with_no_motifs():
    demons = [D("solo", name="", flavor_text=None)]
    result = derive_motifs(demons)
    assert result["solo"].motifs == []
    assert result["solo"].basis == "blocked"
    assert result["solo"].tautological is False


def test_family_with_basis_name_propagates_basis_name_not_text():
    # This demon's OWN candidate for "nut" was extracted from its NAME pattern (basis="name"),
    # even though the family pool itself may contain text-derived tokens from other members —
    # basis tracks how THIS demon came to belong, not what the pool happens to contain.
    demons = [
        D("d0", name="Wallnut Sentinel", families=(FM("nut", basis="name"),)),
        D("d1", flavor_text="Rich nut lore text here.", families=(FM("nut", basis="text"),)),
    ]
    result = derive_motifs(demons)
    assert result["d0"].basis == "name"


# ---- Motif count and ordering -----------------------------------------------------------------


def test_motif_count_is_between_three_and_five_wherever_any_motif_exists():
    demons = [
        D("d0", flavor_text="Ancient towering nut colossus guards the perimeter fiercely today.",
          families=(FM("nut"),)),
    ]
    result = derive_motifs(demons)
    assert 1 <= len(result["d0"].motifs) <= 5  # small fixture; upper bound is the real invariant


def test_ordering_is_family_first_and_stable_across_runs():
    demons = [
        D("host", families=(FM("alpha"), FM("beta"))),
        D("a-member", flavor_text="Alpha token appears here.", families=(FM("alpha"),)),
        D("b-member", flavor_text="Beta token appears here.", families=(FM("beta"),)),
    ]
    first = derive_motifs(demons)["host"].motifs
    second = derive_motifs(list(reversed(demons)))["host"].motifs
    assert first == second


# ---- Anti-motifs --------------------------------------------------------------------------------


def test_anti_motifs_drawn_from_the_nearest_contrasting_family_and_nonempty():
    demons = [
        D("host", families=(FM("nut"),)),
        D("nut-a", flavor_text="Nut shell fortress strength.", families=(FM("nut"),)),
        D("fire-a", flavor_text="Blazing inferno flame chaos.", families=(FM("fire"),)),
    ]
    result = derive_motifs(demons)
    assert result["host"].anti_motifs, "at least one other family exists globally, so contrast is possible"
    assert not (set(result["host"].anti_motifs) & set(result["host"].motifs))


def test_anti_motifs_empty_when_no_other_family_exists_anywhere():
    demons = [
        D("host", families=(FM("nut"),)),
        D("nut-a", flavor_text="Nut shell fortress.", families=(FM("nut"),)),
    ]
    result = derive_motifs(demons)
    assert result["host"].anti_motifs == []


# ---- A2's tautology flag ------------------------------------------------------------------------


def test_a2_tautology_flagged_when_own_and_every_family_are_basis_name():
    demons = [D("d0", name="Nut Guardian", families=(FM("nut", basis="name"),))]
    result = derive_motifs(demons)
    assert result["d0"].tautological is True


def test_not_tautological_when_any_contributing_basis_is_text():
    demons = [
        D("d0", flavor_text="Real lore text about this demon appears here.",
          families=(FM("nut", basis="name"),)),
    ]
    result = derive_motifs(demons)
    assert result["d0"].tautological is False


def test_not_tautological_with_no_family_at_all():
    demons = [D("d0", name="Solo Wanderer")]
    result = derive_motifs(demons)
    assert result["d0"].tautological is False


# ---- No numeric field ----------------------------------------------------------------------------


def test_derived_motifs_carries_no_numeric_field():
    for f in dataclasses.fields(DerivedMotifs):
        assert f.type not in ("int", "float"), f"{f.name} is a numeric field — motifs are words, not magnitudes"
