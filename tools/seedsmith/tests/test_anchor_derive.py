"""Tests for seedsmith.adapters.demons.anchor.derive (spec-classify-pipelines.md §4).

T2.11's own real 20-species run (2026-09-02) crashed on `clamp_variant_count` the first time a
species' `rarity` vote landed on the documented "two repairs, then unresolved" outcome
(spec-classify-pipelines.md §4) — `derive.py` had never been exercised against that real, anticipated
case before this file existed.
"""
from __future__ import annotations

from seedsmith.adapters.demons.anchor.derive import (
    clamp_variant_count,
    derive_posture,
    derive_pure,
)


def test_truncates_to_the_bands_high_end():
    # sprout: [1, 1] — three offered variants, keep only the model's own first one.
    assert clamp_variant_count(["normal", "ancient", "mutated"], "sprout") == ["normal"]


def test_extends_to_the_bands_low_end_deterministically():
    # fused: [2, 3] — one offered variant, extend with the lowest-ordinal missing one twice run
    # over the same input must extend identically.
    once = clamp_variant_count(["ancient"], "fused")
    twice = clamp_variant_count(["ancient"], "fused")
    assert once == twice
    assert len(once) == 2
    assert "ancient" in once


def test_dedupes_before_clamping():
    assert clamp_variant_count(["normal", "normal"], "sprout") == ["normal"]


def test_unresolved_rarity_passes_variants_through_unclamped():
    # The real bug: rarity's own vote can legitimately land on "unresolved" (two failed repairs,
    # spec §4) — there is no band to clamp against, so this must not crash and must not guess a
    # band. The model's own (deduped) variants pass through as-is.
    result = clamp_variant_count(["normal", "ancient", "normal"], "unresolved")
    assert result == ["normal", "ancient"]


def test_an_unknown_rarity_also_passes_through_rather_than_crashing():
    # Any value absent from the tuning bands (not just the literal "unresolved" string) hits the
    # same no-band-to-clamp-against case — defensive against a future rarity added to the enum
    # before the tuning file catches up, not just the one known string.
    result = clamp_variant_count(["normal"], "not-a-real-rarity")
    assert result == ["normal"]


def test_derive_posture_matches_the_real_catalog_for_a_known_aptitude():
    assert derive_posture("Might") in ("Force", "Finesse", "Bastion")


def test_derive_pure_true_when_no_secondary():
    assert derive_pure("Might", "none") is True


def test_derive_pure_true_when_both_aptitudes_share_a_posture():
    # Might and Fortitude are both Force (schema.py's own APTITUDE_POSTURE).
    assert derive_posture("Might") == derive_posture("Fortitude") == "Force"
    assert derive_pure("Might", "Fortitude") is True


def test_derive_pure_false_when_aptitudes_differ_in_posture():
    # Might is Force, Ferocity is Bastion.
    assert derive_posture("Might") != derive_posture("Ferocity")
    assert derive_pure("Might", "Ferocity") is False


def test_derive_posture_unresolved_aptitude_propagates_rather_than_crashing():
    # The second real bug from the same T2.11 run: aptitudePrimary can itself land on
    # "unresolved" the same way rarity can — a posture cannot be derived from it.
    assert derive_posture("unresolved") == "unresolved"


def test_derive_pure_false_when_primary_aptitude_is_unresolved():
    assert derive_pure("unresolved", "none") is False
    assert derive_pure("unresolved", "Ferocity") is False


def test_derive_pure_false_when_secondary_aptitude_is_unresolved():
    assert derive_pure("Might", "unresolved") is False
