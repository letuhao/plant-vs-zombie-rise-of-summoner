"""Task 10 (seedsmith-content-standard, content-completeness-demons) — the missing-field metric
and backfill wiring for demon species, proven against the REAL, committed 904-species corpus, not a
synthetic fixture (spec-content-completeness-demons.md §7).

    python -m pytest tools/seedsmith/tests/test_demons_completeness.py -q
"""
from __future__ import annotations

import unittest
from pathlib import Path

from seedsmith.adapters.demons.completeness import (
    DOMAIN,
    SPECIES_FLAVOR_FIELD,
    SPECIES_KIND,
    ensure_completeness_registered,
    load_species_corpus,
    missing_species_ids,
)
from seedsmith.metrics import Ctx, MetricRegistry, run_all
from seedsmith.metrics.content_completeness import (
    ContentFieldMissing,
    clear_registry,
    registered_specs,
)

REPO_ROOT = Path(__file__).resolve().parents[3]
LIVE_DEMONS_ROOT = REPO_ROOT / "data" / "seed" / "demons"


@unittest.skipUnless(LIVE_DEMONS_ROOT.is_dir(), "live demons corpus not present in this checkout")
class LoadSpeciesCorpusTests(unittest.TestCase):
    """§7 worked example 1: `load_species_corpus` bridges the bare-array shape `Corpus.load()`
    cannot parse (spec §2)."""

    def test_loads_all_904_real_species_entries(self) -> None:
        corpus = load_species_corpus(LIVE_DEMONS_ROOT)
        entries = corpus.by_kind(SPECIES_KIND)
        self.assertEqual(len(entries), 904)

    def test_a_real_known_species_id_resolves_with_its_real_fields(self) -> None:
        corpus = load_species_corpus(LIVE_DEMONS_ROOT)
        entry = corpus.by_id("BloverUmbrella")
        self.assertIsNotNone(entry)
        self.assertEqual(entry.kind, SPECIES_KIND)
        self.assertEqual(entry.get("elementPrimary"), "air")

    def test_no_real_species_entry_has_a_flavor_field_today(self) -> None:
        # The real, un-fabricated finding spec-content-completeness-demons.md §1 predicts: this
        # program has not generated species flavor text yet, so every one of the 904 is missing it.
        corpus = load_species_corpus(LIVE_DEMONS_ROOT)
        with_flavor = [e for e in corpus.by_kind(SPECIES_KIND) if e.get("flavor")]
        self.assertEqual(with_flavor, [])

    def test_species_entries_add_onto_an_existing_corpus_without_disturbing_it(self) -> None:
        # demons is MIXED-shape (spec §2): demon/commander-effect already parse via Corpus.load();
        # this must ADD species on top, never replace what the generic loader already found.
        from seedsmith.corpus import Corpus

        corpus = Corpus.load(LIVE_DEMONS_ROOT)
        demon_count_before = len(corpus.by_kind("demon"))
        self.assertGreater(demon_count_before, 0, "sanity: the real demon-kind corpus is non-empty")

        load_species_corpus(LIVE_DEMONS_ROOT, into=corpus)
        self.assertEqual(len(corpus.by_kind("demon")), demon_count_before)
        self.assertEqual(len(corpus.by_kind(SPECIES_KIND)), 904)


@unittest.skipUnless(LIVE_DEMONS_ROOT.is_dir(), "live demons corpus not present in this checkout")
class ContentFieldMissingAgainstRealCorpusTests(unittest.TestCase):
    """§7 worked example 2: a real, non-fabricated GAP finding against the full real corpus."""

    def setUp(self) -> None:
        clear_registry()

    def tearDown(self) -> None:
        clear_registry()

    def test_reports_904_of_904_missing_flavor(self) -> None:
        ensure_completeness_registered()
        corpus = load_species_corpus(LIVE_DEMONS_ROOT)
        registry = MetricRegistry()
        registry.register(ContentFieldMissing())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=None))

        self.assertEqual(len(findings), 1)
        finding = findings[0]
        self.assertEqual(finding.subject, f"{DOMAIN}:{SPECIES_KIND}")
        self.assertEqual(finding.evidence["missingCount"], 904)
        self.assertEqual(finding.evidence["totalCount"], 904)
        self.assertEqual(finding.evidence["field"], SPECIES_FLAVOR_FIELD)


class EnsureCompletenessRegisteredTests(unittest.TestCase):
    def setUp(self) -> None:
        clear_registry()

    def tearDown(self) -> None:
        clear_registry()

    def test_idempotent_across_repeated_calls(self) -> None:
        ensure_completeness_registered()
        ensure_completeness_registered()
        ensure_completeness_registered()
        matching = [s for s in registered_specs()
                   if s.domain == DOMAIN and s.field == SPECIES_FLAVOR_FIELD]
        self.assertEqual(len(matching), 1)


@unittest.skipUnless(LIVE_DEMONS_ROOT.is_dir(), "live demons corpus not present in this checkout")
class MissingSpeciesIdsBackfillWiringTests(unittest.TestCase):
    """§7 worked example 3: the shared engine's automatic path (`plan_missing`, via
    `missing_species_ids`) proven equivalent to `run/runner.py`'s own real bespoke filter
    (`already_done = {a["speciesId"] for a in existing_anchors}` /
    `ids = [i for i in ids if i not in already_done]`), at real corpus scale."""

    def setUp(self) -> None:
        corpus = load_species_corpus(LIVE_DEMONS_ROOT)
        self.real_anchors = [e.data for e in corpus.by_kind(SPECIES_KIND)]
        self.assertEqual(len(self.real_anchors), 904, "sanity: real corpus scale")

    def test_every_real_species_id_is_a_no_op_not_missing(self) -> None:
        real_ids = [a["speciesId"] for a in self.real_anchors]
        plan = missing_species_ids(self.real_anchors, real_ids)
        self.assertEqual(plan, [], "every already-anchored species must be a no-op, at full scale")

    def test_a_genuinely_new_id_not_yet_anchored_is_correctly_planned(self) -> None:
        real_ids = [a["speciesId"] for a in self.real_anchors]
        candidate_ids = real_ids + ["NotYetClassifiedSpecies"]
        plan = missing_species_ids(self.real_anchors, candidate_ids)
        self.assertEqual(plan, ["NotYetClassifiedSpecies"])

    def test_matches_runner_pys_own_bespoke_already_done_filter_exactly(self) -> None:
        # `run/runner.py`'s `start()`: `already_done = {a["speciesId"] for a in existing_anchors}`;
        # `ids = [i for i in ids if i not in already_done]`. Reproduced here verbatim as the
        # baseline this task's own shared-engine wiring must match, not re-derived differently.
        real_ids = [a["speciesId"] for a in self.real_anchors]
        candidate_ids = real_ids[:50] + ["FirstNewSpecies", "SecondNewSpecies"]

        already_done = {a["speciesId"] for a in self.real_anchors}
        bespoke_plan = [i for i in candidate_ids if i not in already_done]

        shared_plan = missing_species_ids(self.real_anchors, candidate_ids)

        self.assertEqual(sorted(shared_plan), sorted(bespoke_plan))
        self.assertEqual(shared_plan, ["FirstNewSpecies", "SecondNewSpecies"])


if __name__ == "__main__":
    unittest.main()
