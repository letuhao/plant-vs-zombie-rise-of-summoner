"""Tests for seedsmith.metrics.{constraint,exemplar,dedup} (tasks/seedsmith-todo.md, S7).

    python -m pytest tools/seedsmith/tests/test_constraint_exemplar_dedup.py -v
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items import ItemsAdapter  # noqa: E402
from seedsmith.corpus import Corpus  # noqa: E402
from seedsmith.metrics import Ctx, MetricRegistry, Severity, run_all  # noqa: E402
from seedsmith.metrics.constraint import KNOWN_RULES, Constraint, RuleBinding  # noqa: E402
from seedsmith.metrics.dedup import (  # noqa: E402
    SemanticDedup,
    jaccard_estimate,
    minhash_signature,
    shingles,
)
from seedsmith.metrics.exemplar import ExemplarConformance  # noqa: E402


def write(root: Path, rel_path: str, kind: str, entries: list, partition: str = "p") -> None:
    directory = root / Path(rel_path).parent
    directory.mkdir(parents=True, exist_ok=True)
    doc = {"kind": kind, "_meta": {"partition": partition}, "entries": entries}
    (root / rel_path).write_text(json.dumps(doc), encoding="utf-8")


class ConstraintManifestTests(unittest.TestCase):
    def test_the_real_manifest_has_no_currently_unbound_rule(self) -> None:
        # Every rule in KNOWN_RULES was individually verified (grep against the C# source, or
        # against seedsmith.metrics.linkage) to have at least one binding — this pins that
        # verification as a regression check, not a live claim about undiscovered lane-doc rules.
        registry = MetricRegistry()
        registry.register(Constraint())
        findings = run_all(registry, Ctx(corpus=None, adapter=None))
        self.assertEqual(findings, [])

    def test_an_unbound_rule_is_reported(self) -> None:
        rules = KNOWN_RULES + (
            RuleBinding("HypotheticalUnboundRule", "a rule nobody wrote a check for",
                       "some-lane-doc.md", frozenset()),
        )
        registry = MetricRegistry()
        registry.register(Constraint(rules=rules))
        findings = run_all(registry, Ctx(corpus=None, adapter=None))

        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].subject, "HypotheticalUnboundRule")
        self.assertEqual(findings[0].severity, Severity.GAP)

    def test_set_role_not_hybrid_core_is_bound_only_in_seedsmith_not_csharp(self) -> None:
        # The specific correction this task made to spec-metrics.md: verified live against
        # tools/ItemSeedValidator/Checks/*.cs (no "hybrid" match anywhere).
        rule = next(r for r in KNOWN_RULES if r.rule_id == "SetRoleNotHybridCore")
        self.assertEqual(rule.bound_in, frozenset({"seedsmith"}))


class ExemplarConformanceTests(unittest.TestCase):
    """Replays the three historical exemplar defects as fixtures."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp())
        self.adapter = ItemsAdapter()

    def test_missing_required_power_axis_on_a_unique_exemplar_is_caught(self) -> None:
        write(self.root, "_exemplars/unique.json", "unique", [{
            "id": "unique.exemplar-001", "nameKey": "u.ex1", "name": "Exemplar",
            "frame": "plant", "baseType": "item.plant-stem-a-001", "rarity": "grafted",
            "fixedAtoms": [], "counterPressure": {}, "tags": [],
            # powerAxis deliberately omitted — the exact historical defect
        }])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(ExemplarConformance())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=self.adapter))

        codes = {f.evidence["code"] for f in findings}
        self.assertIn("RequiredFieldMissing", codes)
        missing_finding = next(f for f in findings if f.evidence["code"] == "RequiredFieldMissing")
        self.assertIn("powerAxis", missing_finding.evidence["missing"])

    def test_set_exemplar_teaching_members_by_role_alone_is_caught(self) -> None:
        write(self.root, "_exemplars/set.json", "set", [{
            "id": "set.exemplar-001", "nameKey": "s.ex1", "name": "Exemplar Set",
            "themeKey": "ex", "members": [{"role": "core-guard"}, {"role": "head-guard"}],
            "thresholds": [{"pieces": 2}],
        }])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(ExemplarConformance())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=self.adapter))

        codes = {f.evidence["code"] for f in findings}
        self.assertIn("SetUncompletable", codes)

    def test_an_unknown_field_on_an_exemplar_is_caught(self) -> None:
        # A display-template-shaped defect (wrong/extra field) generalized: any exemplar
        # carrying a field its KindSpec does not allow teaches the wrong contract.
        write(self.root, "_exemplars/gem.json", "gem", [{
            "id": "gem.exemplar-001", "nameKey": "g.ex1", "name": "Exemplar Gem",
            "family": "atom.vitality", "powerBand": "low",
            "totallyMadeUpField": "should not be here",
        }])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(ExemplarConformance())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=self.adapter))

        codes = {f.evidence["code"] for f in findings}
        self.assertIn("UnknownField", codes)

    def test_a_valid_exemplar_produces_no_findings(self) -> None:
        write(self.root, "_exemplars/gem.json", "gem", [{
            "id": "gem.exemplar-001", "nameKey": "g.ex1", "name": "Exemplar Gem",
            "family": "atom.vitality", "powerBand": "low",
        }])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(ExemplarConformance())
        self.assertEqual(run_all(registry, Ctx(corpus=corpus, adapter=self.adapter)), [])

    def test_real_shipped_exemplars_all_currently_conform(self) -> None:
        live_root = Path(__file__).resolve().parents[3] / "data" / "seed" / "items"
        if not live_root.is_dir():
            self.skipTest("live item corpus not present in this checkout")
        corpus = Corpus.load(live_root)
        registry = MetricRegistry()
        registry.register(ExemplarConformance())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=self.adapter))
        self.assertEqual(findings, [], f"live exemplars should already conform: {findings}")


class SemanticDedupTests(unittest.TestCase):
    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp())

    def test_exact_duplicate_name_across_kinds_is_caught(self) -> None:
        # The historical incident: gem.g1-015 and consumable.k1-007 both named "Mending Pulse" —
        # already fixed in the live corpus, replayed here as the fixture it should have been.
        write(self.root, "gems/a.json", "gem",
             [{"id": "gem.g1-015", "nameKey": "g.1", "name": "Mending Pulse"}])
        write(self.root, "consumables/a.json", "consumable",
             [{"id": "consumable.k1-007", "nameKey": "c.1", "name": "Mending Pulse"}])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(SemanticDedup())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=None))

        exact = [f for f in findings if f.evidence["code"] == "ExactDuplicateName"]
        self.assertEqual(len(exact), 1)
        self.assertEqual(exact[0].severity, Severity.GAP)
        self.assertEqual(set(exact[0].evidence["entryIds"]), {"gem.g1-015", "consumable.k1-007"})

    def test_distinct_names_produce_no_exact_duplicate_finding(self) -> None:
        write(self.root, "gems/a.json", "gem", [
            {"id": "gem.g1-001", "nameKey": "g.1", "name": "Ember Shard"},
            {"id": "gem.g1-002", "nameKey": "g.2", "name": "Frost Shard"},
        ])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(SemanticDedup())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=None))
        self.assertEqual([f for f in findings if f.evidence["code"] == "ExactDuplicateName"], [])

    def test_canonical_duplicate_ignores_word_order(self) -> None:
        write(self.root, "gems/a.json", "gem", [
            {"id": "gem.g1-001", "nameKey": "g.1", "name": "Ashen Fang"},
            {"id": "gem.g1-002", "nameKey": "g.2", "name": "Fang, Ashen"},
        ])
        corpus = Corpus.load(self.root)
        registry = MetricRegistry()
        registry.register(SemanticDedup())
        findings = run_all(registry, Ctx(corpus=corpus, adapter=None))
        canonical = [f for f in findings if f.evidence["code"] == "CanonicalDuplicate"]
        self.assertEqual(len(canonical), 1)

    def test_minhash_jaccard_is_deterministic_across_calls(self) -> None:
        sig_a = minhash_signature(shingles("Ashen Fang"))
        sig_b = minhash_signature(shingles("Ashen Fang"))
        self.assertEqual(sig_a, sig_b)  # would fail if the hash were process-randomized

    def test_near_identical_names_score_high_jaccard_unrelated_names_score_low(self) -> None:
        # spec-analytics.md §6.2's own worked example: "finds 'Sapvein' vs 'Sapveil'".
        close = jaccard_estimate(minhash_signature(shingles("Sapvein")),
                                 minhash_signature(shingles("Sapveil")))
        far = jaccard_estimate(minhash_signature(shingles("Sapvein")),
                               minhash_signature(shingles("Quantum Tax Filing")))
        self.assertGreater(close, far)
        self.assertGreaterEqual(close, 0.5)


if __name__ == "__main__":
    unittest.main()
