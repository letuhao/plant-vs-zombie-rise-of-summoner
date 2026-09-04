"""Tests for seedsmith.adapters.actions.coverage_report (A-S5, spec-coverage-report.md).

    python -m pytest tools/seedsmith/tests/test_coverage_report.py -v

Spec §5's twelve named cases plus §6's acceptance criteria (1-9, 7b, 7c). Same fixture discipline
every prior module in this session established: the accepted corpus this module measures does not
exist for real (A-S4/A-S3 have never run), so every metric-level test below runs against synthetic,
in-memory `ActionCoverageCtx` fixtures rather than a real `_rounds/round-1/survivors.json` tree. The
one genuinely real thing every test reads directly — never re-typed — is `pairings.json` (for the
"unpaired payoff" case) and the live roster (catalog + family-assignments, for the roster tests),
matching A-S3's own established discipline for exactly this class of test.
"""
from __future__ import annotations

import copy
import json
import random
import socket
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.actions import generate_coverage_report as gen_mod  # noqa: E402
from seedsmith.adapters.actions.characteristic_pool.catalog import load_catalog  # noqa: E402
from seedsmith.adapters.actions.coverage_report import derive as cr  # noqa: E402
from seedsmith.adapters.actions.coverage_report.ctx import (  # noqa: E402
    ActionCoverageCtx, RosterCounts,
)
from seedsmith.adapters.actions.distribution_planner.derive import WeightsRow  # noqa: E402
from seedsmith.adapters.actions.vocab import load_family_ids  # noqa: E402
from seedsmith.metrics.action_coverage import (  # noqa: E402
    ALL_ACTION_COVERAGE_CLOSED_METRICS, ALL_ACTION_COVERAGE_OPEN_METRICS,
    EnablerPayoffCoverageMetric, PairingReachMetric,
)
from seedsmith.metrics.model import Ctx, Finding, Loop, Metric, Severity  # noqa: E402
from seedsmith.metrics.registry import MetricRegistry, run_all  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
FAMILY_IDS = load_family_ids()                                    # the real 98, read fresh
FAM_A, FAM_B = sorted(FAMILY_IDS)[:2]
PAIRINGS_PATH = REPO_ROOT / "data" / "seed" / "actions" / "pairings.json"


# ---------------------------------------------------------------------------------------------
# Fixture builders.
# ---------------------------------------------------------------------------------------------

def _row(id_, scope, scope_key, *, category="attack", rung_band=(1, 10), target_mode="single",
        area_shape=None, relation="enemy", atom_families=(FAM_A,), structure_axes=(),
        pairing_role="none", paired_payoff_family=None, name="Strike"):
    return {
        "id": id_, "scope": scope, "scopeKey": scope_key, "category": category,
        "rungBand": list(rung_band), "targetMode": target_mode, "areaShape": area_shape,
        "relation": relation, "atomFamilies": list(atom_families),
        "structureAxes": list(structure_axes), "pairingRole": pairing_role,
        "pairedPayoffFamily": paired_payoff_family, "name": name,
    }


def _flat_weights() -> WeightsRow:
    return WeightsRow(
        category_milli={"attack": 200, "defense": 200, "support": 200, "movement": 200, "status": 200},
        target_mode_milli={}, area_shape_milli={})


_UNSET = object()   # a caller-passed `pairing_table=None` (missing-input tests) must NOT be
                    # overwritten by the convenience default below — only a genuinely omitted
                    # argument gets one.


def _simple_ctx(accepted_rows, *, quota_by_scope_category=None, subject_category_counts=None,
                roster=None, pairing_table=_UNSET, review_rows=(), mode="smoke", round_no=1):
    """A minimal, hand-built `ActionCoverageCtx` — most tests don't need the real recompute path
    (that path has its own `RecomputeQuotaTests` below), just a fixed quota to test one metric's
    reaction to it."""
    return ActionCoverageCtx(
        accepted_rows=tuple(accepted_rows),
        quota_by_scope_category=quota_by_scope_category or {},
        subject_category_counts=subject_category_counts or {},
        family_ids=FAMILY_IDS,
        pairing_table={} if pairing_table is _UNSET else pairing_table,
        roster=roster or RosterCounts(2, 1, 2),
        review_rows=tuple(review_rows), round_no=round_no, mode=mode,
    )


def _closed_registry() -> MetricRegistry:
    registry = MetricRegistry()
    for metric_cls in ALL_ACTION_COVERAGE_CLOSED_METRICS:
        registry.register(metric_cls())
    return registry


def _open_registry() -> MetricRegistry:
    registry = MetricRegistry()
    for metric_cls in ALL_ACTION_COVERAGE_OPEN_METRICS:
        registry.register(metric_cls())
    return registry


# ---------------------------------------------------------------------------------------------
# Determinism.
# ---------------------------------------------------------------------------------------------

class DeterminismTests(unittest.TestCase):
    def test_two_runs_over_the_same_real_inputs_are_byte_identical_by_hash(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            actions_root = Path(tmp)
            (actions_root / "_reports").mkdir(parents=True)
            first = gen_mod.regenerate(actions_root=actions_root, write=False)
            second = gen_mod.regenerate(actions_root=actions_root, write=False)
        self.assertEqual(first["docHash"], second["docHash"])
        self.assertEqual(first["verdict"], second["verdict"])


# ---------------------------------------------------------------------------------------------
# Planted violation — open metric gates.
# ---------------------------------------------------------------------------------------------

class OpenMetricGatesTests(unittest.TestCase):
    def test_constructing_an_open_metric_with_gates_true_raises_at_registration(self) -> None:
        class _BadOpenMetric(Metric):
            id = "test.bad-open-gates"
            family = "Test"
            loop = Loop.OPEN
            gates = True
            needs = frozenset({"action_coverage"})

            def run(self, ctx: Ctx) -> "list[Finding]":
                return []

        registry = MetricRegistry()
        with self.assertRaises(ValueError) as ctx:
            registry.register(_BadOpenMetric())
        self.assertIn("OPEN-loop metric may never gate", str(ctx.exception))

    def test_both_real_open_metrics_ship_gates_false_and_register_cleanly(self) -> None:
        for metric_cls in ALL_ACTION_COVERAGE_OPEN_METRICS:
            self.assertFalse(metric_cls.gates)
            self.assertIs(metric_cls.loop, Loop.OPEN)
        registry = _open_registry()
        self.assertEqual(len(registry.all()), len(ALL_ACTION_COVERAGE_OPEN_METRICS))

    def test_every_closed_metric_starts_gates_false(self) -> None:
        for metric_cls in ALL_ACTION_COVERAGE_CLOSED_METRICS:
            self.assertFalse(metric_cls.gates)
            self.assertIs(metric_cls.loop, Loop.CLOSED)


# ---------------------------------------------------------------------------------------------
# Planted violation — unevaluated pass (a genuinely missing input -> NOT_MEASURED, never a pass).
# ---------------------------------------------------------------------------------------------

class UnevaluatedPassTests(unittest.TestCase):
    def test_missing_pairings_json_yields_not_measured_and_a_non_pass_verdict(self) -> None:
        cov = _simple_ctx([], pairing_table=None)
        ctx = Ctx(corpus=None, adapter=None, action_coverage=cov)
        findings = run_all(_closed_registry(), ctx)

        pairing_reach = [f for f in findings if f.metric == PairingReachMetric.id]
        enabler_payoff = [f for f in findings if f.metric == EnablerPayoffCoverageMetric.id]
        self.assertTrue(all(f.severity is Severity.NOT_MEASURED for f in pairing_reach))
        self.assertTrue(all(f.severity is Severity.NOT_MEASURED for f in enabler_payoff))
        self.assertTrue(len(pairing_reach) >= 1 and len(enabler_payoff) >= 1)

        closed_ids = [m.id for m in ALL_ACTION_COVERAGE_CLOSED_METRICS]
        verdict = cr.compute_verdict(findings, closed_ids, cov.mode)
        self.assertIn(PairingReachMetric.id, verdict.not_measured_metrics)
        self.assertNotEqual(verdict.verdict, "pass")
        self.assertNotEqual(verdict.verdict, "smoke-clean")


# ---------------------------------------------------------------------------------------------
# Planted violation — thin cell hidden.
# ---------------------------------------------------------------------------------------------

class ThinCellHiddenTests(unittest.TestCase):
    def test_an_empty_planned_cell_produces_a_thin_and_occupancy_finding_naming_the_cell(self) -> None:
        quota = {("species", "attack"): 7}
        cov = _simple_ctx([], quota_by_scope_category=quota)
        ctx = Ctx(corpus=None, adapter=None, action_coverage=cov)
        findings = run_all(_closed_registry(), ctx)

        occ = [f for f in findings if f.metric == "action.corpus.cellOccupancy"]
        thin = [f for f in findings if f.metric == "action.corpus.thinCell"]
        self.assertEqual(len(occ), 1)
        self.assertIn("cell.species.attack.1-10", occ[0].subject)
        self.assertEqual(len(thin), 1)
        self.assertEqual(thin[0].evidence["shortfall"], 7)

        closed_ids = [m.id for m in ALL_ACTION_COVERAGE_CLOSED_METRICS]
        verdict = cr.compute_verdict(findings, closed_ids, cov.mode)
        self.assertNotEqual(verdict.verdict, "pass")
        self.assertNotEqual(verdict.verdict, "smoke-clean")
        self.assertIn("action.corpus.thinCell", verdict.gap_metrics)

    def test_a_partially_filled_cell_is_thin_but_not_missing(self) -> None:
        quota = {("species", "attack"): 7}
        rows = [_row(f"action.species.x.{i:03d}", "species", "x") for i in range(3)]
        cov = _simple_ctx(rows, quota_by_scope_category=quota)
        occ = cr.cell_occupancy_findings("m", cov)
        thin = cr.thin_cell_findings("m", cov)
        self.assertEqual(occ, [])
        self.assertEqual(len(thin), 1)
        self.assertEqual(thin[0].evidence["shortfall"], 4)

    def test_a_zero_quota_cell_is_never_thin_or_missing(self) -> None:
        cov = _simple_ctx([], quota_by_scope_category={})
        self.assertEqual(cr.cell_occupancy_findings("m", cov), [])
        self.assertEqual(cr.thin_cell_findings("m", cov), [])

    def test_every_planned_cell_carries_a_count_and_quota_even_at_zero(self) -> None:
        """Acceptance #4."""
        cov = _simple_ctx([], quota_by_scope_category={})
        groups = cr.build_cell_groups(cov.accepted_rows, cov.quota_by_scope_category)
        entries = cr.cell_entries(groups)
        # 3 scopes x 5 categories x 3 pairingRoles
        self.assertEqual(len(entries), 45)
        for e in entries:
            self.assertIn("count", e)
            self.assertIn("quota", e)
            self.assertEqual(e["quota"], 0)
            self.assertEqual(e["count"], 0)
            self.assertFalse(e["thin"])


# ---------------------------------------------------------------------------------------------
# Planted violation — unpaired payoff. Reads pairings.json's own first key/enabler at test time.
# ---------------------------------------------------------------------------------------------

class UnpairedPayoffTests(unittest.TestCase):
    def test_a_payoff_family_with_no_enabler_in_the_same_anchor_fails(self) -> None:
        pairing_table = json.loads(PAIRINGS_PATH.read_text(encoding="utf-8"))
        payoff_key = sorted(pairing_table)[0]
        enabler = sorted(pairing_table[payoff_key])[0]

        # payoff present, its enabler absent from the SAME anchor
        rows = [_row("action.species.x.001", "species", "x", atom_families=[payoff_key])]
        cov = _simple_ctx(rows, pairing_table={k: tuple(v) for k, v in pairing_table.items()})
        findings = cr.enabler_payoff_coverage_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.GAP)
        self.assertIn(payoff_key, findings[0].message)

        # now with the enabler present in the same anchor -> clean
        rows_ok = rows + [_row("action.species.x.002", "species", "x", atom_families=[enabler])]
        cov_ok = _simple_ctx(rows_ok, pairing_table={k: tuple(v) for k, v in pairing_table.items()})
        self.assertEqual(cr.enabler_payoff_coverage_findings("m", cov_ok), [])

    def test_pairing_reach_is_honestly_zero_against_the_real_pairings_file(self) -> None:
        """Acceptance #7c: the real pairings.json's 5 (well, 2-key, several-enabler) ids sit
        entirely outside the 98-family namespace today — pairingReach must say so in those
        words, never show a misleadingly empty section."""
        pairing_table = json.loads(PAIRINGS_PATH.read_text(encoding="utf-8"))
        cov = _simple_ctx([], pairing_table={k: tuple(v) for k, v in pairing_table.items()})
        findings = cr.pairing_reach_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertIn("zero reach while pairings.json still carries its", findings[0].message)
        self.assertEqual(findings[0].evidence["reachablePayoffKeys"], [])


# ---------------------------------------------------------------------------------------------
# Planted violation — a family outside the namespace.
# ---------------------------------------------------------------------------------------------

class FamilyOutsideNamespaceTests(unittest.TestCase):
    def test_a_fixture_atom_family_outside_the_98_is_refused(self) -> None:
        rows = [_row("action.general.0001", "general", None,
                     atom_families=["atom.fx-cold-on-hit"])]
        cov = _simple_ctx(rows)
        findings = cr.atom_family_namespace_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.GAP)
        self.assertIn("atom.fx-cold-on-hit", findings[0].message)
        self.assertIn("action.general.0001", findings[0].subject)

    def test_a_real_98_family_id_is_not_refused(self) -> None:
        rows = [_row("action.general.0001", "general", None, atom_families=[FAM_A, FAM_B])]
        cov = _simple_ctx(rows)
        self.assertEqual(cr.atom_family_namespace_findings("m", cov), [])


# ---------------------------------------------------------------------------------------------
# Planted violation — a status where a family belongs.
# ---------------------------------------------------------------------------------------------

class StatusWhereFamilyBelongsTests(unittest.TestCase):
    def test_a_bare_status_id_in_paired_payoff_family_is_refused_naming_the_field(self) -> None:
        rows = [_row("action.general.0001", "general", None, pairing_role="enabler",
                     paired_payoff_family="rot")]
        cov = _simple_ctx(rows)
        findings = cr.atom_family_namespace_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertIn("STATUS id", findings[0].message)
        self.assertIn("pairedPayoffFamily", findings[0].subject)


# ---------------------------------------------------------------------------------------------
# Planted violation — reaction accepted.
# ---------------------------------------------------------------------------------------------

class ReactionAcceptedTests(unittest.TestCase):
    def test_a_row_spending_reaction_fails_structure_enforceability(self) -> None:
        rows = [_row("action.general.0001", "general", None, structure_axes=["reaction"])]
        cov = _simple_ctx(rows)
        findings = cr.structure_enforceability_findings("m", cov)
        gaps = [f for f in findings if f.severity is Severity.GAP]
        self.assertEqual(len(gaps), 1)
        self.assertEqual(gaps[0].subject, "reaction")
        self.assertEqual(gaps[0].evidence["count"], 1)

    def test_restriction_is_reported_as_a_note_not_a_gap(self) -> None:
        rows = [_row("action.general.0001", "general", None, structure_axes=["restriction"])]
        cov = _simple_ctx(rows)
        findings = cr.structure_enforceability_findings("m", cov)
        self.assertTrue(all(f.severity is not Severity.GAP for f in findings))
        restriction = [f for f in findings if f.subject == "restriction"][0]
        self.assertEqual(restriction.evidence["count"], 1)

    def test_zero_reaction_is_a_clean_note(self) -> None:
        cov = _simple_ctx([])
        findings = cr.structure_enforceability_findings("m", cov)
        self.assertTrue(all(f.severity is not Severity.GAP for f in findings))


# ---------------------------------------------------------------------------------------------
# Planted violation — roster inflation.
# ---------------------------------------------------------------------------------------------

class RosterInflationTests(unittest.TestCase):
    def test_a_904_species_count_fails_roster_reconciliation(self) -> None:
        bad_roster = RosterCounts(species_count=904, family_count=200, family_assigned_count=800)
        findings = cr.roster_reconciliation_findings("m", bad_roster, accepted_corpus_size=0)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.GAP)
        self.assertIn("roster-inflated", findings[0].message)

    def test_the_real_shipped_roster_re_derives_below_the_band_never_quoting_it_raw(self) -> None:
        """Acceptance #6 — re-verify the roster numbers directly rather than trusting any prompt."""
        catalog = load_catalog()
        family_assignments = json.loads(
            (REPO_ROOT / "data" / "seed" / "demons" / "_generated" / "family-assignments.json")
            .read_text(encoding="utf-8"))
        members = gen_mod._family_members(family_assignments)
        roster = RosterCounts(species_count=len(catalog), family_count=len(members),
                              family_assigned_count=sum(len(v) for v in members.values()))
        self.assertEqual(roster.species_count, 84)
        self.assertEqual(roster.family_count, 19)
        self.assertEqual(roster.family_assigned_count, 53)

        findings = cr.roster_reconciliation_findings("m", roster, accepted_corpus_size=0)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.NOTE)
        evidence = findings[0].evidence
        self.assertEqual(evidence["signatureTierEstimate"], 84 * 3)
        self.assertTrue(evidence["belowBand"])
        self.assertEqual(evidence["researchBandRoster"], 904)
        # the message must show the re-derivation arithmetic, never just repeat "1,500-3,500"
        self.assertIn("84 x 3 = 252", findings[0].message)
        self.assertIn("below", findings[0].message)


# ---------------------------------------------------------------------------------------------
# Next-round targets — pure function + shuffle invariance.
# ---------------------------------------------------------------------------------------------

class NextRoundTargetsTests(unittest.TestCase):
    def _fixture(self):
        subject_counts = {
            ("species", "alpha"): {"attack": 5, "defense": 0, "support": 0, "movement": 0, "status": 0},
            ("species", "beta"): {"attack": 3, "defense": 0, "support": 0, "movement": 0, "status": 0},
        }
        quota = cr.aggregate_scope_category_quota(subject_counts)
        rows = [_row(f"action.species.alpha.{i:03d}", "species", "alpha") for i in range(2)]
        groups = cr.build_cell_groups(rows, quota)
        return groups, subject_counts, rows

    def test_same_inputs_produce_the_same_targets(self) -> None:
        groups, subject_counts, rows = self._fixture()
        first = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                      accepted_rows=rows, round_no=1)
        second = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                       accepted_rows=rows, round_no=1)
        self.assertEqual(first, second)
        self.assertTrue(len(first) >= 1)

    def test_shuffling_accepted_row_order_changes_nothing(self) -> None:
        groups, subject_counts, rows = self._fixture()
        baseline = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                         accepted_rows=rows, round_no=1)
        shuffled = list(rows)
        random.Random(42).shuffle(shuffled)
        result = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                       accepted_rows=shuffled, round_no=1)
        self.assertEqual(baseline, result)

    def test_targets_are_ordered_by_shortfall_desc_then_subject_key(self) -> None:
        groups, subject_counts, rows = self._fixture()
        targets = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                        accepted_rows=rows, round_no=1)
        attack_targets = [t for t in targets if t["category"] == "attack"]
        wants = [t["want"] for t in attack_targets]
        self.assertEqual(wants, sorted(wants, reverse=True))
        # beta (shortfall 3) should sort ahead of alpha (shortfall 3) only if wants tie -> then
        # subjectKey ascending; alpha's shortfall is 5-2=3, beta's is 3-0=3 -- a real tie, so
        # alpha (< beta) must come first.
        self.assertEqual([t["scopeKey"] for t in attack_targets], ["alpha", "beta"])

    def test_target_ids_follow_the_action_coverage_id_grammar(self) -> None:
        groups, subject_counts, rows = self._fixture()
        targets = cr.next_round_targets(groups=groups, subject_counts=subject_counts,
                                        accepted_rows=rows, round_no=1)
        for t in targets:
            self.assertTrue(t["id"].startswith("target.round-2."))
            self.assertEqual(t["kindOfEntry"], "next-target")


# ---------------------------------------------------------------------------------------------
# Small batch honesty.
# ---------------------------------------------------------------------------------------------

class SmallBatchHonestyTests(unittest.TestCase):
    def test_twelve_accepted_rows_never_produce_a_corpus_level_pass(self) -> None:
        weights = _flat_weights()
        subject_counts = {}
        for i in range(12):
            subject_counts[("species", f"sp{i}")] = {"attack": 1, "defense": 0, "support": 0,
                                                      "movement": 0, "status": 0}
        quota = cr.aggregate_scope_category_quota(subject_counts)
        rows = [_row(f"action.species.sp{i}.001", "species", f"sp{i}") for i in range(12)]
        cov = _simple_ctx(rows, quota_by_scope_category=quota,
                          subject_category_counts=subject_counts, mode="smoke")
        ctx = Ctx(corpus=None, adapter=None, action_coverage=cov)
        findings = run_all(_closed_registry(), ctx)
        closed_ids = [m.id for m in ALL_ACTION_COVERAGE_CLOSED_METRICS]
        verdict = cr.compute_verdict(findings, closed_ids, cov.mode)

        self.assertEqual(len(cov.accepted_rows), 12)
        self.assertNotEqual(verdict.verdict, "pass")
        self.assertGreater(len(findings), 0)


# ---------------------------------------------------------------------------------------------
# Offline guarantee.
# ---------------------------------------------------------------------------------------------

class OfflineGuaranteeTests(unittest.TestCase):
    def test_no_source_file_references_the_llm_transport(self) -> None:
        forbidden = ("llm_caller", "langchain", "langgraph", "requests", "urllib.request", "httpx")
        paths = list((REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "adapters" / "actions"
                     / "coverage_report").glob("*.py"))
        paths.append(REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "adapters" / "actions"
                    / "generate_coverage_report.py")
        paths.append(REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "metrics"
                    / "action_coverage.py")
        for path in paths:
            text = path.read_text(encoding="utf-8")
            for token in forbidden:
                self.assertNotIn(token, text, f"{path.name} references {token!r}")

    def test_regenerate_opens_no_non_loopback_socket(self) -> None:
        real_connect = socket.socket.connect
        attempts: list = []

        def guarded(self, address):
            host = address[0] if isinstance(address, tuple) else str(address)
            if isinstance(host, str) and not (
                host.startswith("127.") or host in ("localhost", "::1", "0.0.0.0")
            ):
                attempts.append(address)
                raise AssertionError(f"non-loopback connection attempted: {address}")
            return real_connect(self, address)

        socket.socket.connect = guarded
        try:
            gen_mod.regenerate(write=False)
        finally:
            socket.socket.connect = real_connect
        self.assertEqual(attempts, [])


# ---------------------------------------------------------------------------------------------
# Quota recomputation (spec §3 step 2) and quotaDrift.
# ---------------------------------------------------------------------------------------------

class RecomputeQuotaTests(unittest.TestCase):
    def test_recompute_uses_the_same_largest_remainder_helper(self) -> None:
        weights = _flat_weights()
        subject_counts = cr.recompute_subject_category_counts(
            species_ids=["alpha", "beta"], family_members={},
            weights_by_key={("species", "alpha"): weights, ("species", "beta"): weights},
            general_count=0, per_family_count=0, per_species_count=5)
        self.assertEqual(sum(subject_counts[("species", "alpha")].values()), 5)
        self.assertEqual(sum(subject_counts[("species", "beta")].values()), 5)

    def test_quota_drift_flags_overshoot_beyond_tolerance(self) -> None:
        quota = {("species", "attack"): 5}
        rows = [_row(f"action.species.x.{i:03d}", "species", "x") for i in range(10)]
        cov = _simple_ctx(rows, quota_by_scope_category=quota)
        findings = cr.quota_drift_findings("m", cov)
        gaps = [f for f in findings if f.severity is Severity.GAP]
        self.assertEqual(len(gaps), 1)
        self.assertEqual(gaps[0].evidence["driftUnits"], 5)

    def test_quota_drift_within_tolerance_is_a_note(self) -> None:
        quota = {("species", "attack"): 5}
        rows = [_row(f"action.species.x.{i:03d}", "species", "x") for i in range(6)]
        cov = _simple_ctx(rows, quota_by_scope_category=quota)
        findings = cr.quota_drift_findings("m", cov)
        self.assertTrue(all(f.severity is Severity.NOTE for f in findings))


# ---------------------------------------------------------------------------------------------
# Species collision.
# ---------------------------------------------------------------------------------------------

class SpeciesCollisionTests(unittest.TestCase):
    def test_two_species_with_identical_signature_sets_collide(self) -> None:
        rows = [
            _row("action.species.a.001", "species", "a", category="attack"),
            _row("action.species.b.001", "species", "b", category="attack"),
        ]
        cov = _simple_ctx(rows)
        findings = cr.species_collision_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.GAP)

    def test_two_species_with_different_categories_do_not_collide(self) -> None:
        rows = [
            _row("action.species.a.001", "species", "a", category="attack"),
            _row("action.species.b.001", "species", "b", category="defense"),
        ]
        cov = _simple_ctx(rows)
        self.assertEqual(cr.species_collision_findings("m", cov), [])


# ---------------------------------------------------------------------------------------------
# Singleton share (measure-only).
# ---------------------------------------------------------------------------------------------

class SingletonShareTests(unittest.TestCase):
    def test_all_singleton_cells_report_100_percent_share(self) -> None:
        rows = [
            _row("action.species.a.001", "species", "a", category="attack"),
            _row("action.species.a.002", "species", "a", category="defense"),
        ]
        cov = _simple_ctx(rows)
        findings = cr.singleton_share_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].evidence["singletonShare"], 1.0)

    def test_no_accepted_rows_is_not_measured(self) -> None:
        cov = _simple_ctx([])
        findings = cr.singleton_share_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.NOT_MEASURED)


# ---------------------------------------------------------------------------------------------
# Open metrics — review queue only, never a defect.
# ---------------------------------------------------------------------------------------------

class OpenMetricsReviewQueueTests(unittest.TestCase):
    def test_flavour_quality_flags_a_generic_name_as_a_note(self) -> None:
        rows = [_row("action.species.a.001", "species", "a", name="Attack")]
        cov = _simple_ctx(rows)
        findings = cr.flavour_quality_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.NOTE)

    def test_semantic_neighbour_passes_through_review_rows(self) -> None:
        review_rows = [{"candidateA": "action.species.a.001", "candidateB": "action.species.a.002",
                       "similarityMilli": 900}]
        cov = _simple_ctx([], review_rows=review_rows)
        findings = cr.semantic_neighbour_findings("m", cov)
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].severity, Severity.NOTE)

    def test_open_metrics_never_appear_in_the_closed_verdict_computation(self) -> None:
        rows = [_row("action.species.a.001", "species", "a", name="Attack")]
        cov = _simple_ctx(rows)
        ctx = Ctx(corpus=None, adapter=None, action_coverage=cov)
        open_findings = run_all(_open_registry(), ctx)
        closed_ids = [m.id for m in ALL_ACTION_COVERAGE_CLOSED_METRICS]
        for f in open_findings:
            self.assertNotIn(f.metric, closed_ids)


# ---------------------------------------------------------------------------------------------
# Real, honest report over round-1.json's plan with zero accepted rows — the module's own
# legitimate "small-batch honesty" case, run for real against the live tree (spec §1).
# ---------------------------------------------------------------------------------------------

class RealNonzeroAcceptedReportTests(unittest.TestCase):
    """Renamed from `RealZeroAcceptedReportTests` (2026-09-04, expanded real smoke batch): this
    checkout's accepted corpus is no longer genuinely empty -- A-S6 (`innate_picker`) has for the
    first time promoted real A-P1/A-P2/A-P3 survivors into `committed-round-1.json` /
    `committed-round-2.json` (24 real accepted rows total), so a real `regenerate()` call now
    measures real, non-empty content rather than degrading to the "small batch honesty" zero case
    this class used to pin. That empty-corpus behavior is still real and still covered --
    `SyntheticContentTests`/`OfflineGuaranteeTests` elsewhere in this file exercise it directly --
    this class only ever asserted it against the REAL checkout, which has permanently moved on."""

    def test_a_real_run_reports_the_real_accepted_corpus_and_an_explicit_non_pass_verdict(self) -> None:
        summary = gen_mod.regenerate(write=False)
        self.assertEqual(summary["acceptedCorpusSize"], 24)
        self.assertNotEqual(summary["verdict"], "pass")
        # 3 scopes x 5 categories = 15 (scope, category, rungBand) groups; 45 is the exploded
        # per-pairingRole cell ROW count in the written report's `entries` (cell_entries below).
        self.assertEqual(summary["cellCount"], 15)
        # pairings.json IS present in this checkout, so both pairing metrics run; with a real,
        # non-empty accepted corpus every metric below now has occupied mechanical cells to
        # measure, so nothing degrades to NOT_MEASURED any more (contrast the old empty-corpus
        # pin: `["action.corpus.singletonShare"]`).
        self.assertEqual(summary["notMeasuredMetrics"], [])
        # The real gaps this expanded batch's own thin corpus actually has -- named explicitly
        # (acceptance #3), never silently absorbed into a green verdict.
        self.assertEqual(sorted(summary["gapMetrics"]),
                         ["action.corpus.cellOccupancy", "action.corpus.thinCell"])


if __name__ == "__main__":
    unittest.main()
