"""Tests for seedsmith.adapters.demons.anchor.{permute,vote} (spec-option-permutation.md,
demon-seed module 6)."""
from __future__ import annotations

from seedsmith.adapters.demons.anchor.permute import order_for
from seedsmith.adapters.demons.anchor.vote import (
    VOTED_FIELDS,
    VoteRecord,
    VoteResult,
    disagreement_rate,
    resolve_vote,
)

ELEMENTS = ["fire", "ice", "air", "earth", "light", "dark"]


def test_same_species_same_order_across_runs():
    a = order_for("peashooter", "elementPrimary", 0, ELEMENTS)
    b = order_for("peashooter", "elementPrimary", 0, ELEMENTS)
    assert a == b


def test_three_samples_have_three_distinct_orders():
    orders = [order_for("peashooter", "elementPrimary", i, ELEMENTS) for i in range(3)]
    assert orders[0] != orders[1]
    assert orders[1] != orders[2]
    assert orders[0] != orders[2]


def test_different_species_get_different_orders():
    a = order_for("peashooter", "elementPrimary", 0, ELEMENTS)
    b = order_for("sunflower", "elementPrimary", 0, ELEMENTS)
    assert a != b


def test_order_is_a_permutation_never_drops_or_duplicates():
    order = order_for("peashooter", "elementPrimary", 0, ELEMENTS)
    assert sorted(order) == sorted(ELEMENTS)
    assert len(order) == len(ELEMENTS)


def test_different_fields_on_same_species_get_different_orders():
    a = order_for("peashooter", "elementPrimary", 0, ELEMENTS)
    b = order_for("peashooter", "aptitudePrimary", 0, ELEMENTS)
    assert a != b


# --- vote resolution -------------------------------------------------------------------------


def test_unanimous_vote_is_high_confidence():
    r = resolve_vote(["fire", "fire", "fire"])
    assert r == VoteResult(value="fire", confidence="high", minority=None)


def test_two_one_split_records_the_minority():
    r = resolve_vote(["fire", "fire", "ice"])
    assert r.value == "fire"
    assert r.confidence == "split"
    assert r.minority == "ice"


def test_three_way_split_is_unresolved_not_first():
    r = resolve_vote(["fire", "ice", "air"])
    assert r.value is None
    assert r.confidence == "unresolved"
    assert r.minority is None


def test_resolve_vote_requires_exactly_three():
    import pytest
    with pytest.raises(ValueError):
        resolve_vote(["fire", "ice"])


def test_vote_set_is_exactly_the_six_named_fields():
    # attackTempo joined 2026-09-04 (demon-corpus-self-heal C1, owner-approved) — kit-shape was the
    # one pipeline never wired into voting, and the real corpus audit found exactly the collapse
    # that predicts (attackTempo entropy 0.00 across 833 real species).
    assert VOTED_FIELDS == frozenset({
        "elementPrimary", "aptitudePrimary", "rarity", "threatBand", "deployMode", "attackTempo",
    })
    assert len(VOTED_FIELDS) == 6


def test_disagreement_rate_is_reported_per_field_and_side():
    records = [
        VoteRecord("p1", "plant", "elementPrimary", resolve_vote(["fire", "fire", "fire"])),
        VoteRecord("p2", "plant", "elementPrimary", resolve_vote(["fire", "fire", "ice"])),
        VoteRecord("z1", "zombie", "elementPrimary", resolve_vote(["fire", "ice", "air"])),
        VoteRecord("z2", "zombie", "elementPrimary", resolve_vote(["fire", "fire", "fire"])),
    ]
    rates = disagreement_rate(records)
    assert rates["elementPrimary"]["plant"] == 0.5   # 1 of 2 disagreed
    assert rates["elementPrimary"]["zombie"] == 0.5  # 1 of 2 disagreed


def test_disagreement_rate_zero_when_everything_unanimous():
    records = [
        VoteRecord("p1", "plant", "rarity", resolve_vote(["chaff", "chaff", "chaff"])),
        VoteRecord("p2", "plant", "rarity", resolve_vote(["sprout", "sprout", "sprout"])),
    ]
    rates = disagreement_rate(records)
    assert rates["rarity"]["plant"] == 0.0
