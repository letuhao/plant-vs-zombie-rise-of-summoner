"""Tests for seedsmith.adapters.demons.anchor.legacy_diff (spec-anchor-emit.md §6, T2.7)."""
from __future__ import annotations

from seedsmith.adapters.demons.anchor.legacy_diff import diff_legacy, format_report


def test_legacy_diff_reports_per_field_agreement():
    new_anchors = [
        {"speciesId": "a", "elementPrimary": "fire", "deployMode": "PlantAvatar",
         "acquisition": ["Summonable"], "variants": ["normal", "shiny"]},
        {"speciesId": "b", "elementPrimary": "ice", "deployMode": "HypnoAlly",
         "acquisition": ["Summonable"], "variants": ["normal"]},
    ]
    legacy = [
        {"id": "a", "elementPrimary": "fire", "deployMode": "PlantAvatar",
         "acquisition": ["Summonable"], "variants": ["shiny", "normal"]},  # same set, diff order
        {"id": "b", "elementPrimary": "earth", "deployMode": "HypnoAlly",
         "acquisition": ["CaptureOnly"], "variants": ["normal"]},
    ]
    report = diff_legacy(new_anchors, legacy)

    assert report["elementPrimary"].total == 2
    assert report["elementPrimary"].agree == 1   # a agrees, b disagrees
    assert report["deployMode"].agree == 2         # both agree
    assert report["acquisition"].agree == 1        # a agrees, b disagrees
    assert report["variants"].agree == 2           # order-insensitive — both agree


def test_species_absent_from_legacy_are_excluded_not_counted_as_disagreement():
    new_anchors = [{"speciesId": "brand-new", "elementPrimary": "fire", "deployMode": "PlantAvatar",
                    "acquisition": ["Summonable"], "variants": ["normal"]}]
    report = diff_legacy(new_anchors, legacy_entries=[])
    for fa in report.values():
        assert fa.total == 0
        assert fa.agree_rate == 0.0


def test_only_the_named_fields_are_compared():
    from seedsmith.adapters.demons.anchor.legacy_diff import COMPARED_FIELDS
    assert set(COMPARED_FIELDS) == {"elementPrimary", "deployMode", "acquisition", "variants"}
    assert "attackTempo" not in COMPARED_FIELDS  # no old counterpart to compare against


def test_format_report_reads_as_a_percentage():
    new_anchors = [{"speciesId": "a", "elementPrimary": "fire", "deployMode": "PlantAvatar",
                    "acquisition": ["Summonable"], "variants": ["normal"]}]
    legacy = [{"id": "a", "elementPrimary": "fire", "deployMode": "PlantAvatar",
              "acquisition": ["Summonable"], "variants": ["normal"]}]
    text = format_report(diff_legacy(new_anchors, legacy))
    assert "100.0%" in text
