"""Tests for the event half of seedsmith.adapters.dungeon.briefs (D1.10's real remaining scope,
2026-09-07).

    python -m pytest tools/seedsmith/tests/test_dungeon_event_briefs.py -v
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.dungeon.briefs import (  # noqa: E402
    EVENT_KIND_FIRST_SHIP,
    EVENT_OUTCOME_COUNT,
    EVENT_SYSTEM_PROMPT,
    build_event_brief,
    build_event_schema_for_cell,
)
from seedsmith.adapters.dungeon.planner import Cell  # noqa: E402

FAMILIES = frozenset({"atom.might", "atom.vitality"})
POWER_BANDS = frozenset({"trivial", "low", "medium", "high", "extreme"})
OVERRIDE_TAGS = frozenset({"herbs", "key", "holy", "bait", "watch"})


def _schema(kind: str = "curio", theme: str = "creature.wallnut", event_id: str = "event.curio-creature.wallnut-001") -> dict:
    cell = Cell("dungeon-event", (kind, theme), f"{kind}-{theme}")
    return build_event_schema_for_cell(
        cell, event_id,
        grantable_atom_families=FAMILIES, power_bands=POWER_BANDS, override_tags=OVERRIDE_TAGS,
    )


class EventKindFirstShipTests(unittest.TestCase):
    def test_story_is_excluded(self) -> None:
        self.assertNotIn("story", EVENT_KIND_FIRST_SHIP)

    def test_exactly_five_kinds(self) -> None:
        self.assertEqual(len(EVENT_KIND_FIRST_SHIP), 5)


class BuildEventSchemaForCellTests(unittest.TestCase):
    def test_eventId_kind_theme_are_pinned_to_the_real_cell(self) -> None:
        schema = _schema(kind="shrine", theme="creature.pot", event_id="event.shrine-creature.pot-003")
        self.assertEqual(schema["properties"]["eventId"]["const"], "event.shrine-creature.pot-003")
        self.assertEqual(schema["properties"]["kind"]["const"], "shrine")
        self.assertEqual(schema["properties"]["theme"]["const"], "creature.pot")

    def test_eligibility_is_pinned_to_json_null_never_a_string_sentinel(self) -> None:
        schema = _schema()
        elig = schema["properties"]["eligibility"]
        self.assertEqual(elig["type"], "null")
        self.assertIsNone(elig["const"])

    def test_chainRef_is_always_pinned_none(self) -> None:
        schema = _schema()
        self.assertEqual(schema["properties"]["chainRef"]["const"], "none")

    def test_outcomes_array_is_fixed_at_the_batch_outcome_count(self) -> None:
        schema = _schema()
        self.assertEqual(schema["properties"]["outcomes"]["minItems"], EVENT_OUTCOME_COUNT)
        self.assertEqual(schema["properties"]["outcomes"]["maxItems"], EVENT_OUTCOME_COUNT)

    def test_outcome_ordinal_never_offers_nothing(self) -> None:
        schema = _schema()
        ordinal_enum = schema["properties"]["outcomes"]["items"]["properties"]["ordinal"]["enum"]
        self.assertNotIn("nothing", ordinal_enum)
        self.assertEqual(set(ordinal_enum), {"good", "mixed", "bad"})

    def test_outcome_effects_family_is_exactly_the_supplied_grantable_set(self) -> None:
        schema = _schema()
        family_enum = schema["properties"]["outcomes"]["items"]["properties"]["effects"]["items"]["properties"]["family"]["enum"]
        self.assertEqual(set(family_enum), FAMILIES)

    def test_outcome_effects_powerBand_is_exactly_the_supplied_set(self) -> None:
        schema = _schema()
        pb_enum = schema["properties"]["outcomes"]["items"]["properties"]["effects"]["items"]["properties"]["powerBand"]["enum"]
        self.assertEqual(set(pb_enum), POWER_BANDS)

    def test_supplyOverride_offers_every_real_tag_plus_none(self) -> None:
        schema = _schema()
        self.assertEqual(set(schema["properties"]["supplyOverride"]["enum"]), OVERRIDE_TAGS | {"none"})

    def test_required_matches_the_kindspecs_own_twelve_fields(self) -> None:
        schema = _schema()
        self.assertEqual(set(schema["required"]), {
            "eventId", "kind", "theme", "name", "flavor", "reason", "climateAffinity",
            "repeatScope", "eligibility", "outcomes", "supplyOverride", "chainRef",
        })

    def test_additionalProperties_is_false_at_top_level_and_on_outcome_items(self) -> None:
        schema = _schema()
        self.assertFalse(schema["additionalProperties"])
        self.assertFalse(schema["properties"]["outcomes"]["items"]["additionalProperties"])
        self.assertFalse(schema["properties"]["outcomes"]["items"]["properties"]["effects"]["items"]["additionalProperties"])


class BuildEventBriefTests(unittest.TestCase):
    def test_brief_names_the_kind_and_the_allocated_motifs(self) -> None:
        cell = Cell("dungeon-event", ("trap", "creature.pot"), "trap-creature.pot")
        brief = build_event_brief(cell, {"motifs": ["屋顶", "植物"], "antiMotifs": ["铁头功"]})
        self.assertIn("trap", brief)
        self.assertIn("屋顶", brief)
        self.assertIn("植物", brief)
        self.assertIn("铁头功", brief)
        self.assertIn(str(EVENT_OUTCOME_COUNT), brief)

    def test_every_kind_produces_a_nonempty_brief_with_no_crash(self) -> None:
        for kind in EVENT_KIND_FIRST_SHIP:
            cell = Cell("dungeon-event", (kind, "creature.wallnut"), f"{kind}-creature.wallnut")
            brief = build_event_brief(cell, {"motifs": ["a"], "antiMotifs": []})
            self.assertTrue(brief.strip())

    def test_an_empty_motif_allocation_still_produces_a_readable_brief(self) -> None:
        cell = Cell("dungeon-event", ("curio", "creature.wallnut"), "curio-creature.wallnut")
        brief = build_event_brief(cell, {"motifs": [], "antiMotifs": []})
        self.assertIn("none allocated", brief)


class EventSystemPromptTests(unittest.TestCase):
    def test_prompt_is_nonempty_and_mentions_motifs(self) -> None:
        self.assertTrue(EVENT_SYSTEM_PROMPT.strip())
        self.assertIn("motif", EVENT_SYSTEM_PROMPT.lower())


if __name__ == "__main__":
    unittest.main()
