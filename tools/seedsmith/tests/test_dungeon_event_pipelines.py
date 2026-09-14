"""Tests for the event half of seedsmith.adapters.dungeon.pipelines (D1.10's real remaining scope,
2026-09-07).

    python -m pytest tools/seedsmith/tests/test_dungeon_event_pipelines.py -v

`FakeCall` proves the one-call-per-slot / motif-quality-retry logic without any network dependency,
matching `test_dungeon_quest_pipelines.py`'s own established shape.
"""
from __future__ import annotations

import collections
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.dungeon.pipelines import (  # noqa: E402
    MAX_QUALITY_RETRY,
    run_event_draws,
)
from seedsmith.adapters.dungeon.planner import Cell  # noqa: E402

FAMILIES = frozenset({"atom.might"})
POWER_BANDS = frozenset({"medium"})
OVERRIDE_TAGS = frozenset({"herbs"})

THEMES = {
    "creature.a": {"motifs": ["motifA1", "motifA2"], "antiMotifs": ["banA"]},
    "creature.b": {"motifs": ["motifB1", "motifB2", "motifB3", "motifB4"], "antiMotifs": []},
}


def _outcome(ordinal: str = "good") -> dict:
    return {"ordinal": ordinal, "consequence": "none", "dropBand": "staple", "effects": []}


def _event(event_id: str, kind: str, theme: str, *, flavor: str = "A scene.", name: str = "Title") -> dict:
    return {
        "eventId": event_id, "kind": kind, "theme": theme, "name": name,
        "flavor": flavor, "reason": "because", "climateAffinity": "none",
        "repeatScope": "per-delve", "eligibility": None,
        "outcomes": [_outcome("good"), _outcome("bad")],
        "supplyOverride": "none", "chainRef": "none",
    }


class FakeCall:
    def __init__(self, responses: "list[dict]") -> None:
        self._responses = collections.deque(responses)
        self.calls: "list[tuple[str, str]]" = []

    def __call__(self, system: str, user: str, *, config=None, schema=None) -> str:
        self.calls.append((system, user))
        if not self._responses:
            raise AssertionError("FakeCall exhausted -- more calls made than responses supplied")
        return json.dumps(self._responses.popleft())


def _kwargs():
    return dict(themes=THEMES, grantable_atom_families=FAMILIES, power_bands=POWER_BANDS, override_tags=OVERRIDE_TAGS)


class RunEventDrawsTests(unittest.TestCase):
    def test_one_call_per_slot_no_vote(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([_event("event.curio-creature.a-001", "curio", "creature.a", flavor="Uses motifA1 here.")])
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake, **_kwargs())

        self.assertEqual(len(fake.calls), 1)
        self.assertEqual(len(results), 1)
        self.assertIsNotNone(results[0].entry)
        self.assertEqual(results[0].entry["eventId"], "event.curio-creature.a-001")

    def test_a_target_two_cell_makes_two_independent_calls_with_different_motif_briefs(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.b"), "curio-creature.b")
        ids = ["event.curio-creature.b-001", "event.curio-creature.b-002"]
        fake = FakeCall([
            _event(ids[0], "curio", "creature.b", name="Title One", flavor="Uses motifB1 here."),
            _event(ids[1], "curio", "creature.b", name="Title Two", flavor="Uses motifB2 here."),
        ])
        results = run_event_draws([cell], {"curio-creature.b": ids}, call=fake, **_kwargs())

        self.assertEqual(len(fake.calls), 2)
        self.assertEqual(len(results), 2)
        self.assertNotEqual(fake.calls[0][1], fake.calls[1][1])  # different briefs, different motif slices
        self.assertTrue(all(r.entry is not None for r in results))

    def test_an_empty_target_cell_produces_zero_results(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([])
        results = run_event_draws([cell], {"curio-creature.a": []}, call=fake, **_kwargs())
        self.assertEqual(results, [])
        self.assertEqual(len(fake.calls), 0)

    def test_a_motif_coverage_rejection_triggers_a_quality_retry_that_then_succeeds(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([
            _event("event.curio-creature.a-001", "curio", "creature.a", flavor="No motif word appears here at all."),
            _event("event.curio-creature.a-001", "curio", "creature.a", flavor="Now it uses motifA1 properly."),
        ])
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake, **_kwargs())

        self.assertEqual(len(fake.calls), 2)  # first rejected, second accepted
        self.assertIsNotNone(results[0].entry)
        self.assertIn("motifA1", results[0].entry["flavor"])
        # the retry's own brief names the defect back to the model
        self.assertIn("rejected", fake.calls[1][1].lower())

    def test_an_anti_motif_violation_also_triggers_a_quality_retry(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([
            _event("event.curio-creature.a-001", "curio", "creature.a", flavor="Uses motifA1 but also banA, forbidden."),
            _event("event.curio-creature.a-001", "curio", "creature.a", flavor="Uses motifA1 cleanly this time."),
        ])
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake, **_kwargs())
        self.assertEqual(len(fake.calls), 2)
        self.assertIsNotNone(results[0].entry)

    def test_a_name_collision_within_the_same_run_triggers_a_quality_retry(self) -> None:
        cell_a = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        cell_b = Cell("dungeon-event", ("trap", "creature.a"), "trap-creature.a")
        fake = FakeCall([
            _event("event.curio-creature.a-001", "curio", "creature.a", name="The Crimson Stage", flavor="Uses motifA1."),
            _event("event.trap-creature.a-001", "trap", "creature.a", name="The Crimson Stage", flavor="Uses motifA1 too."),
            _event("event.trap-creature.a-001", "trap", "creature.a", name="A Genuinely Different Title", flavor="Uses motifA1 as well."),
        ])
        results = run_event_draws(
            [cell_a, cell_b],
            {"curio-creature.a": ["event.curio-creature.a-001"], "trap-creature.a": ["event.trap-creature.a-001"]},
            call=fake, **_kwargs())

        self.assertEqual(len(fake.calls), 3)  # cell_a: 1 call; cell_b: rejected once, then accepted
        self.assertEqual(results[0].entry["name"], "The Crimson Stage")
        self.assertEqual(results[1].entry["name"], "A Genuinely Different Title")
        self.assertIn("already used", fake.calls[2][1])

    def test_existing_names_seeds_the_collision_set_across_batches(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([
            _event("event.curio-creature.a-001", "curio", "creature.a", name="An Old Title", flavor="Uses motifA1."),
            _event("event.curio-creature.a-001", "curio", "creature.a", name="A Fresh Title", flavor="Uses motifA1 too."),
        ])
        results = run_event_draws(
            [cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake,
            existing_names=["An Old Title"], **_kwargs())
        self.assertEqual(len(fake.calls), 2)
        self.assertEqual(results[0].entry["name"], "A Fresh Title")

    def test_exhausting_every_retry_leaves_the_slot_unresolved_never_a_silent_bad_answer(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        bad = _event("event.curio-creature.a-001", "curio", "creature.a", flavor="Never mentions a motif at all.")
        fake = FakeCall([bad] * (MAX_QUALITY_RETRY + 1))
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake, **_kwargs())

        self.assertEqual(len(fake.calls), MAX_QUALITY_RETRY + 1)
        self.assertIsNone(results[0].entry)
        self.assertTrue(results[0].reason.startswith("quality_retry"))

    def test_a_call_that_never_parses_reports_unresolved_not_a_crash(self) -> None:
        class NeverParses:
            def __call__(self, *args, **kwargs) -> str:
                return "not json"
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]},
                                   call=NeverParses(), **_kwargs())
        self.assertIsNone(results[0].entry)
        self.assertEqual(results[0].reason, "insufficient_valid_samples")

    def test_never_calls_the_real_transport_when_a_stub_is_supplied(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.a"), "curio-creature.a")
        fake = FakeCall([_event("event.curio-creature.a-001", "curio", "creature.a", flavor="Uses motifA1.")])
        results = run_event_draws([cell], {"curio-creature.a": ["event.curio-creature.a-001"]}, call=fake, **_kwargs())
        self.assertIsNotNone(results[0].entry)


if __name__ == "__main__":
    unittest.main()
