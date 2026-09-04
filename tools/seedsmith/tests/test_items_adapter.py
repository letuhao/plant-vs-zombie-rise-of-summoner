"""Tests for seedsmith.adapters.items (tasks/seedsmith-todo.md, S2).

    python -m pytest tools/seedsmith/tests/test_items_adapter.py -v

Two kinds of test here on purpose, mirroring the plan's own split: unit-level tests against the
adapter's structural claims (registry-shaped tests, no live corpus needed), and one integration
test against the REAL `data/seed/items` corpus — the live corpus is the deliberate subject here,
not a violation of "fixtures are synthetic": S2's whole acceptance criterion (spec-foundation §6,
CP-B) is "the tool independently rediscovers a defect in the real content," which cannot be
proven any other way.
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items import ItemsAdapter  # noqa: E402
from seedsmith.adapters.items.kinds import KINDS  # noqa: E402
from seedsmith.adapters.items.channels import PRIMARY_CHANNEL_IDS  # noqa: E402
from seedsmith.adapters.items.registries import (  # noqa: E402
    HYBRID_FRAME_CITATION,
    REGISTRY_DIR,
    partition_kind_map,
)
from seedsmith.corpus import Corpus  # noqa: E402
from seedsmith.metrics import Ctx, MetricRegistry, Severity, run_all  # noqa: E402
from seedsmith.metrics.coverage import EmptyPartitionMetric  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
LIVE_ITEMS_ROOT = REPO_ROOT / "data" / "seed" / "items"


class KindSpecTests(unittest.TestCase):
    def test_fifteen_kinds_including_the_undefined_attribute_kind(self) -> None:
        self.assertEqual(len(KINDS), 15)
        self.assertIn("attribute", {k.kind for k in KINDS})

    def test_base_type_required_fields_match_kind_catalog(self) -> None:
        base_type = next(k for k in KINDS if k.kind == "base-type")
        self.assertTrue({"id", "nameKey", "name", "frame", "role", "class", "band",
                        "iconKey", "tags"} <= base_type.required)


class RegistryVersionTests(unittest.TestCase):
    def test_versions_are_read_not_a_single_hardcoded_constant(self) -> None:
        versions = ItemsAdapter().registries().versions
        # Measured fresh 2026-08-23: NOT all equal — a single assumed constant would already be
        # wrong for naming/tags (v4) and classes (v3) against bands/core/themes (v1).
        self.assertEqual(versions["naming"], 4)
        self.assertEqual(versions["tags"], 4)
        self.assertEqual(versions["classes"], 3)
        self.assertEqual(versions["bands"], 1)
        self.assertGreater(len(set(versions.values())), 1)


class LegalCombinationsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.legal = ItemsAdapter().legal_combinations()

    def test_ward_array_excluded_from_hybrid_frame(self) -> None:
        self.assertFalse(self.legal("role", "ward-array", "frame", "hybrid"))

    def test_jewel_minor_b_is_legal_with_hybrid_frame(self) -> None:
        # D30 (registryVersion 2, 2026-09-04): D3 wins over the prior 13-role/895‰ shape this test
        # used to assert — jewel-minor-b is hybrid-eligible; head-guard and sense are not.
        self.assertTrue(self.legal("frame", "hybrid", "role", "jewel-minor-b"))

    def test_head_guard_excluded_from_hybrid_frame(self) -> None:
        self.assertFalse(self.legal("frame", "hybrid", "role", "head-guard"))

    def test_sense_excluded_from_hybrid_frame(self) -> None:
        self.assertFalse(self.legal("frame", "hybrid", "role", "sense"))

    def test_commander_standard_excluded_from_hybrid_frame(self) -> None:
        self.assertFalse(self.legal("role", "standard", "frame", "hybrid"))

    def test_ordinary_role_is_legal_with_hybrid_frame(self) -> None:
        self.assertTrue(self.legal("role", "footing", "frame", "hybrid"))

    def test_any_role_is_legal_with_non_hybrid_frames(self) -> None:
        self.assertTrue(self.legal("role", "ward-array", "frame", "humanoid"))
        self.assertTrue(self.legal("role", "ward-array", "frame", "plant"))

    def test_hybrid_frame_citation_still_present_in_the_live_registry(self) -> None:
        # Pins the one transcribed (not parsed) fact in this adapter against the actual
        # registry text, so an edit to core.v1.json's frame prose cannot silently invalidate
        # HYBRID_FRAME_EXCLUDED_ROLES without a test failing.
        core_text = (REGISTRY_DIR / "core.v1.json").read_text(encoding="utf-8")
        self.assertIn(HYBRID_FRAME_CITATION, core_text)


class ChannelTests(unittest.TestCase):
    def test_fourteen_primary_channels(self) -> None:
        channels = ItemsAdapter().channels()
        self.assertEqual(len(channels), 14)
        self.assertEqual({c.id for c in channels}, PRIMARY_CHANNEL_IDS)

    def test_channels_match_bands_registry_member_families(self) -> None:
        bands = json.loads((REGISTRY_DIR / "bands.v1.json").read_text(encoding="utf-8"))
        registry_ids = frozenset(
            bands["powerBand"]["channelFamilyGroups"]["primaryChannel"]["memberFamilies"])
        self.assertEqual(PRIMARY_CHANNEL_IDS, registry_ids)

    def test_reference_base_is_callable_and_deterministic(self) -> None:
        channel = ItemsAdapter().channels()[0]
        self.assertEqual(channel.reference_base(5), channel.reference_base(5))


@unittest.skipUnless(LIVE_ITEMS_ROOT.is_dir(), "live item corpus not present in this checkout")
class LiveCorpusIntegrationTests(unittest.TestCase):
    """The known-answer test (tasks/seedsmith-plan.md, CP-B): rediscover, in one command, the
    exact defect that took three authoring waves and a hand-written diff to notice — verified
    fresh against the corpus as it stands today, not against a number carried over from an
    earlier session (which had already drifted once)."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.corpus = Corpus.load(LIVE_ITEMS_ROOT)
        cls.adapter = ItemsAdapter()

    def test_loads_the_expected_entry_and_file_counts(self) -> None:
        self.assertEqual(len(self.corpus.entries), 1430)
        seen_files = {e.path for e in self.corpus.entries.values()}
        self.assertEqual(len(seen_files), 121)

    def test_exactly_nine_empty_partitions_and_no_others(self) -> None:
        ctx = Ctx(corpus=self.corpus, adapter=self.adapter)
        registry = MetricRegistry()
        registry.register(EmptyPartitionMetric())

        findings = run_all(registry, ctx)
        subjects = {f.subject for f in findings}

        self.assertEqual(len(findings), 9)
        self.assertEqual(subjects, {
            "attributes",
            "base-types/footing/plant/a", "base-types/footing/plant/b",
            "base-types/manipulator/humanoid/b", "base-types/mantle/humanoid/a",
            "display-templates/4", "display-templates/5", "display-templates/6",
            "gems/2",
        })
        self.assertTrue(all(f.severity is Severity.GAP for f in findings))

    def test_attributes_is_distinguishable_as_the_deferred_one(self) -> None:
        # "attributes" is qualitatively different from the other eight: it has no authored
        # shape at all (KindCatalog.cs's `ShapeDefined: false`), so it is flagged rather than
        # silently folded into the same bucket as an ordinary missing partition.
        attribute_kind = next(k for k in KINDS if k.kind == "attribute")
        self.assertEqual(attribute_kind.required, {"id", "nameKey", "name"})  # common-only

        ctx = Ctx(corpus=self.corpus, adapter=self.adapter)
        registry = MetricRegistry()
        registry.register(EmptyPartitionMetric())
        findings = {f.subject: f for f in run_all(registry, ctx)}

        self.assertIn("attributes", findings)
        self.assertEqual(partition_kind_map()["attributes"], "attribute")


if __name__ == "__main__":
    unittest.main()
