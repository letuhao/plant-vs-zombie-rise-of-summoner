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
    load_aptitude_fallback,
    load_rarity_power_fallback,
    resolve_secondary_element_from_fusion_lineage,
    resolve_unresolved_aptitude,
    resolve_unresolved_rarity,
    resolve_unresolved_threat_band,
)
from seedsmith.adapters.demons.power.bands import ThreatTuning


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


# ---- resolve_unresolved_threat_band (2026-09-04, demon-corpus-self-heal F1) ---------------------

def test_a_resolved_threat_band_passes_through_unchanged():
    tuning = ThreatTuning.load()
    value, was_deterministic = resolve_unresolved_threat_band("tyrant", tuning=tuning)
    assert value == "tyrant"
    assert was_deterministic is False


def test_unresolved_threat_band_resolves_to_the_real_sanctioned_default():
    tuning = ThreatTuning.load()
    value, was_deterministic = resolve_unresolved_threat_band("unresolved", tuning=tuning)
    # The exact value the real, committed demon-threat.v1.json names — never invented here.
    assert value == tuning.threshold_for_rung(tuning.inferred_default_rung).id
    assert was_deterministic is True


# ---- resolve_unresolved_rarity (2026-09-07, demon-corpus-self-heal Phase H, owner-directed) -----
#
# Rarity is this game's OWN mechanism, not an almanac/PvZ property — when the identity pipeline's
# vote never converges, the owner's direction is "stronger species are rarer, fall back to a
# deterministic engine" rather than leave it unresolved forever. The fallback reuses threatBand's
# own already-validated power banding (demon-threat.v1.json) via a rank-preserving correspondence
# (demon-rarity-power-fallback.v1.json), never a second independent curve.

def test_a_resolved_rarity_passes_through_unchanged():
    mapping = load_rarity_power_fallback()
    value, was_deterministic = resolve_unresolved_rarity("fused", "tyrant", mapping=mapping)
    assert value == "fused"
    assert was_deterministic is False


def test_unresolved_rarity_resolves_from_a_resolved_threat_band():
    mapping = load_rarity_power_fallback()
    value, was_deterministic = resolve_unresolved_rarity("unresolved", "calamity", mapping=mapping)
    # calamity is threat rung 10, the top — the committed mapping names its rarity explicitly.
    assert value == mapping["calamity"]
    assert value == "almanac"
    assert was_deterministic is True


def test_unresolved_rarity_stays_unresolved_when_threat_band_has_no_signal_either():
    mapping = load_rarity_power_fallback()
    value, was_deterministic = resolve_unresolved_rarity("unresolved", "unresolved", mapping=mapping)
    assert value == "unresolved"
    assert was_deterministic is False


def test_rarity_power_fallback_is_a_rank_preserving_bijection_over_both_closed_ladders():
    from seedsmith.adapters.demons.anchor.schema import RARITY, THREAT_BAND

    mapping = load_rarity_power_fallback()
    assert set(mapping.keys()) == set(THREAT_BAND)
    assert set(mapping.values()) == set(RARITY)
    # Rank-preserving: the Nth-weakest threat band maps to the Nth-least-rare rarity.
    assert [mapping[t] for t in THREAT_BAND] == list(RARITY)


# ---- resolve_unresolved_aptitude (2026-09-07, demon-corpus-self-heal Phase I, owner-directed) ---
#
# No real signal exists for aptitude (measured: F≈1.34 over the species with a computable score,
# and 10 of the 11 real unresolved species have no computable score at all) — this is a flat,
# undisguised invented default, not a derivation, on the owner's own explicit direction.

def test_a_resolved_aptitude_passes_through_unchanged():
    value, was_deterministic = resolve_unresolved_aptitude("Bulwark", default="Onslaught")
    assert value == "Bulwark"
    assert was_deterministic is False


def test_unresolved_aptitude_resolves_to_the_flat_default():
    value, was_deterministic = resolve_unresolved_aptitude("unresolved", default="Onslaught")
    assert value == "Onslaught"
    assert was_deterministic is True


def test_aptitude_fallback_names_a_real_aptitude():
    from seedsmith.adapters.demons.anchor.schema import APTITUDES

    default = load_aptitude_fallback()
    assert default in APTITUDES


# ---- resolve_secondary_element_from_fusion_lineage --------------------------------------------

def test_a_real_secondary_passes_through_unchanged():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "fire", "earth", input_a_element="earth", input_b_element="fire")
    assert value == "fire"
    assert was_fixed is False


def test_input_b_supplies_a_clean_secondary_when_input_a_matches_the_output():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "none", "air", input_a_element="air", input_b_element="earth")
    assert value == "earth"
    assert was_fixed is True


def test_input_a_supplies_a_clean_secondary_when_it_is_the_one_that_differs():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "none", "air", input_a_element="fire", input_b_element="air")
    assert value == "fire"
    assert was_fixed is True


def test_both_parents_matching_the_output_is_real_signal_not_a_gap():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "none", "light", input_a_element="light", input_b_element="light")
    assert value == "none"
    assert was_fixed is False


def test_both_parents_differing_and_disagreeing_is_left_unresolved():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "none", "light", input_a_element="earth", input_b_element="dark")
    assert value == "none"
    assert was_fixed is False


def test_missing_lineage_data_is_left_unresolved_rather_than_guessed():
    value, was_fixed = resolve_secondary_element_from_fusion_lineage(
        "none", "fire", input_a_element=None, input_b_element=None)
    assert value == "none"
    assert was_fixed is False
