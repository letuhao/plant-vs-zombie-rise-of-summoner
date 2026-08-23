"""Parity harness: seedsmith's Linkage+Registration metrics vs `tools/seed_graph`'s original
seven check functions, both run against the SAME live item corpus (tasks/seedsmith-todo.md, S3).

Two independent implementations of the same property disagreeing is the cheapest possible
detector for the "checker was wrong" defect class (spec-foundation §7.4) — the whole reason S10's
CI cutover runs both side by side for a week before `seed_graph` is deleted. This harness is what
that week's CI step calls.

Usable two ways, per the acceptance criterion ("a test, not a one-off script"):
    python -m pytest tools/seedsmith/tests/parity_seed_graph.py -v      # pytest, live corpus
    python tools/seedsmith/tests/parity_seed_graph.py [--json OUT.json]  # CLI, prints a diff
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

_SEEDSMITH_ROOT = Path(__file__).resolve().parent.parent
_SEED_GRAPH_ROOT = _SEEDSMITH_ROOT.parent / "seed_graph"
sys.path.insert(0, str(_SEEDSMITH_ROOT))
sys.path.insert(0, str(_SEED_GRAPH_ROOT))

from seedsmith.corpus import Corpus as SeedsmithCorpus  # noqa: E402
from seedsmith.metrics import Ctx, MetricRegistry, run_all  # noqa: E402
from seedsmith.metrics.linkage import ALL_LINKAGE_METRICS  # noqa: E402
from seedgraph import Acquisition as SeedGraphAcquisition  # noqa: E402
from seedgraph import Corpus as SeedGraphCorpus  # noqa: E402
from seedgraph.checks import run_all as seed_graph_run_all  # noqa: E402

REPO_ROOT = _SEEDSMITH_ROOT.parent.parent
LIVE_ITEMS_ROOT = REPO_ROOT / "data" / "seed" / "items"


def seedsmith_findings(root: Path) -> "set[tuple[str, str, str]]":
    corpus = SeedsmithCorpus.load(root)
    registry = MetricRegistry()
    for metric_cls in ALL_LINKAGE_METRICS:
        registry.register(metric_cls())
    ctx = Ctx(corpus=corpus, adapter=None)
    findings = run_all(registry, ctx)
    return {(f.evidence["code"], f.subject, f.severity.value.upper()) for f in findings}


def seed_graph_findings(root: Path) -> "set[tuple[str, str, str]]":
    corpus = SeedGraphCorpus.load(root)
    acquisition = SeedGraphAcquisition.build(corpus)
    findings = seed_graph_run_all(corpus, acquisition)
    return {(f.code, f.subject, f.severity) for f in findings}


def compare(root: Path) -> "tuple[set, set]":
    """Returns (seedsmith_only, seed_graph_only) — both empty means byte-identical finding sets
    (up to the (code, subject, severity) triple, which is what a finding IS for this purpose;
    message wording is prose and not part of the contract)."""
    ss = seedsmith_findings(root)
    sg = seed_graph_findings(root)
    return ss - sg, sg - ss


@unittest.skipUnless(LIVE_ITEMS_ROOT.is_dir(), "live item corpus not present in this checkout")
class ParityTests(unittest.TestCase):
    def test_finding_sets_are_identical_on_the_live_corpus(self) -> None:
        seedsmith_only, seed_graph_only = compare(LIVE_ITEMS_ROOT)
        self.assertEqual(seedsmith_only, set(),
                         f"seedsmith found these but seed_graph did not: {seedsmith_only}")
        self.assertEqual(seed_graph_only, set(),
                         f"seed_graph found these but seedsmith did not: {seed_graph_only}")


def main() -> int:
    seedsmith_only, seed_graph_only = compare(LIVE_ITEMS_ROOT)
    if not seedsmith_only and not seed_graph_only:
        print("PARITY OK — finding sets byte-identical")
        return 0
    print("PARITY MISMATCH")
    for triple in sorted(seedsmith_only):
        print(f"  seedsmith only: {triple}")
    for triple in sorted(seed_graph_only):
        print(f"  seed_graph only: {triple}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
