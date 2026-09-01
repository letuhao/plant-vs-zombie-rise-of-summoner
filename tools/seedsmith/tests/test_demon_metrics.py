"""Tests for seedsmith.metrics.demon_coverage / motif_sharing (spec-demon-metrics.md, wave D3).

Synthetic fixtures throughout (standing rule) — none of these metrics have run against the real
corpus yet, since the field-naming contract they read (`motifs`, `motifTautological`) is only
written once `motif-derive`'s output is wired onto corpus entries, which is integration work this
wave does not include (see D2's own notes on why nothing has been committed yet).
"""
from __future__ import annotations

from seedsmith.adapters.demons import DemonsAdapter
from seedsmith.corpus.model import Corpus, Entry
from seedsmith.metrics.demon_coverage import DemonUncoveredMetric
from seedsmith.metrics.model import Ctx, Severity
from seedsmith.metrics.motif_sharing import MotifSharingMetric


def demon(id_: str, **extra) -> Entry:
    return Entry(id=id_, kind="demon", partition="zombie/common", path="d.json",
                data={"id": id_, "name": id_, **extra})


def content(id_: str, kind: str, demon_id: str) -> Entry:
    return Entry(id=id_, kind=kind, partition="x", path="c.json",
                data={"id": id_, "demonId": demon_id})


def build_corpus(*entries: Entry) -> Corpus:
    c = Corpus()
    for e in entries:
        c.add(e)
    return c


# ---- Coverage/DemonUncovered ------------------------------------------------------------------


def test_every_demon_has_content_reports_nothing():
    corpus = build_corpus(
        demon("a"), content("a-aspect", "aspect", "a"),
        demon("b"), content("b-cmd", "commander-effect", "b"),
    )
    findings = DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert findings == []


def test_a5_one_demon_uncovered_its_families_all_covered_is_one_finding():
    # A5's exact case: family coverage would report GREEN here (every family has content via
    # sibling members), but per-demon coverage must still catch the uncovered one directly.
    corpus = build_corpus(
        demon("multi-family-a", family=["nut", "shell"]), content("x", "aspect", "multi-family-a"),
        demon("uncovered", family=["nut", "shell"]),  # same families, but no content of its own
    )
    findings = DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert len(findings) == 1
    assert findings[0].subject == "uncovered"
    assert findings[0].severity == Severity.GAP


def test_a_demon_with_commander_effect_but_no_aspect_is_covered():
    corpus = build_corpus(demon("d0"), content("c0", "commander-effect", "d0"))
    findings = DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert findings == []


def test_uncovered_finding_names_every_checked_kind_as_absent():
    corpus = build_corpus(demon("d0"))
    findings = DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert len(findings) == 1
    f = findings[0]
    assert f.evidence["presentKinds"] == []
    assert set(f.evidence["absentKinds"]) == {"aspect", "commander-effect", "environment"}


def test_no_demons_at_all_reports_nothing():
    corpus = build_corpus(content("x", "aspect", "ghost"))
    findings = DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert findings == []


def test_generic_across_a_non_demon_subject_kind():
    """§5's own requirement: this metric must work for a non-demon adapter supplying the same
    strata. Proven directly by pointing `subject_kind` at a different kind entirely."""
    metric = DemonUncoveredMetric()
    metric.subject_kind = "widget"
    corpus = build_corpus(
        Entry(id="w1", kind="widget", partition="p", path="w.json", data={"id": "w1"}),
        Entry(id="g1", kind="gadget", partition="p", path="g.json", data={"id": "g1", "widgetId": "w1"}),
        Entry(id="w2", kind="widget", partition="p", path="w2.json", data={"id": "w2"}),
    )
    from seedsmith.adapters._stub import StubAdapter
    findings = metric.run(Ctx(corpus=corpus, adapter=StubAdapter()))
    assert len(findings) == 1
    assert findings[0].subject == "w2"


# ---- Distribution/MotifSharing ------------------------------------------------------------------


def test_no_demon_carries_motif_data_reports_cannot_measure_not_success():
    corpus = build_corpus(demon("a"), demon("b"))
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert len(findings) == 1
    assert findings[0].evidence["measuredCount"] == 0
    assert findings[0].severity == Severity.NOTE


def test_a2_entirely_tautological_corpus_reports_cannot_be_measured_not_perfect_sharing():
    corpus = build_corpus(
        demon("a", motifs=["nut"], motifTautological=True),
        demon("b", motifs=["nut"], motifTautological=True),
    )
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert len(findings) == 1
    f = findings[0]
    assert "cannot be measured" in f.message
    assert f.evidence["excludedTautological"] == 2
    assert "demonsPerMotif" not in f.evidence, "must not report a number when nothing was measured"


def test_motifs_each_used_once_single_use_equals_vocabulary_size():
    corpus = build_corpus(
        demon("a", motifs=["alpha"], motifTautological=False),
        demon("b", motifs=["beta"], motifTautological=False),
    )
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    f = findings[0]
    assert set(f.evidence["singleUseMotifs"]) == {"alpha", "beta"}
    assert f.evidence["motifVocabularySize"] == 2


def test_motifs_shared_across_a_family_demons_per_motif_above_one():
    corpus = build_corpus(
        demon("a", motifs=["nut"], motifTautological=False),
        demon("b", motifs=["nut"], motifTautological=False),
        demon("c", motifs=["nut"], motifTautological=False),
    )
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    assert findings[0].evidence["demonsPerMotif"] > 1


def test_a_tautological_demon_is_excluded_from_both_numerator_and_denominator():
    corpus = build_corpus(
        demon("real-a", motifs=["nut"], motifTautological=False),
        demon("real-b", motifs=["nut"], motifTautological=False),
        demon("fake", motifs=["nut"], motifTautological=True),
    )
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    f = findings[0]
    assert f.evidence["excludedTautological"] == 1
    assert f.evidence["demonsPerMotif"] == 2.0, "the tautological demon must not inflate the count"


def test_schema_carries_no_pass_fail_field():
    corpus = build_corpus(demon("a", motifs=["nut"], motifTautological=False))
    findings = MotifSharingMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))
    for f in findings:
        assert "verdict" not in f.evidence
        assert "pass" not in f.evidence
        assert "ok" not in f.evidence


def test_both_metrics_ship_non_gating():
    assert DemonUncoveredMetric.gates is False
    assert MotifSharingMetric.gates is False


def test_finding_ordering_is_stable_across_runs():
    corpus = build_corpus(demon("z"), demon("a"), demon("m"))
    first = [f.subject for f in DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))]
    second = [f.subject for f in DemonUncoveredMetric().run(Ctx(corpus=corpus, adapter=DemonsAdapter()))]
    assert first == second == ["a", "m", "z"]
