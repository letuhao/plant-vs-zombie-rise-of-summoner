"""Tests for seedsmith.adapters.dungeon.planner (D1.9, spec-dungeon-seed-contract.md §4).

    python -m pytest tools/seedsmith/tests/test_dungeon_planner.py -v

Covers what D1.9 actually builds: cell enumeration from the adapter's real legality function, and
id minting from a high-water mark. The motif-brief allocator and Hopcroft-Karp feasibility check
are a stated, deliberate gap (planner.py's own module docstring) — not tested here because they
are not built here.
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.dungeon import DungeonAdapter  # noqa: E402
from seedsmith.adapters.dungeon.planner import Cell, IdMinter, enumerate_cells, plan_ids_for_cells  # noqa: E402


class EnumerateCellsTests(unittest.TestCase):
    def test_room_cells_match_the_53_legal_shape(self) -> None:
        adapter = DungeonAdapter()
        dims = {d.id: d for d in adapter.dimensions()}
        cells = enumerate_cells(
            "dungeon-room",
            [("roomKind", dims["roomKind"].values), ("climate", dims["climate"].values)],
            adapter.legal_combinations(),
        )
        self.assertEqual(len(cells), 53)
        # A climate-neutral kind appears exactly once (climate=none); a climate-bearing kind
        # appears seven times (six elements + none).
        boss_cells = [c for c in cells if c.dimension_values[0] == "boss"]
        self.assertEqual(len(boss_cells), 1)
        self.assertEqual(boss_cells[0].dimension_values[1], "none")

        fight_cells = [c for c in cells if c.dimension_values[0] == "fight"]
        self.assertEqual(len(fight_cells), 7)

    def test_single_dimension_cells(self) -> None:
        cells = enumerate_cells("dungeon-encounter", [("formation", ("pack", "party", "boss"))], lambda *_: True)
        self.assertEqual({c.cell_key for c in cells}, {"pack", "party", "boss"})

    def test_cell_keys_are_deterministic_and_sorted(self) -> None:
        adapter = DungeonAdapter()
        dims = {d.id: d for d in adapter.dimensions()}
        cells_1 = enumerate_cells("dungeon-room", [("roomKind", dims["roomKind"].values), ("climate", dims["climate"].values)], adapter.legal_combinations())
        cells_2 = enumerate_cells("dungeon-room", [("roomKind", dims["roomKind"].values), ("climate", dims["climate"].values)], adapter.legal_combinations())
        self.assertEqual([c.cell_key for c in cells_1], [c.cell_key for c in cells_2])


class IdMinterTests(unittest.TestCase):
    def test_ids_are_sequential_per_namespace_and_cell(self) -> None:
        minter = IdMinter()
        self.assertEqual(minter.next_id("room", "cache-ice"), "room.cache-ice-001")
        self.assertEqual(minter.next_id("room", "cache-ice"), "room.cache-ice-002")
        self.assertEqual(minter.next_id("room", "fight-fire"), "room.fight-fire-001")  # separate cell, own sequence

    def test_ids_continue_from_a_supplied_high_water_mark(self) -> None:
        # The exact incident this exists to prevent: a second run must not restart at 1 and
        # collide with ids a prior run already minted for this cell.
        minter = IdMinter(high_water_marks={("room", "cache-ice"): 7})
        self.assertEqual(minter.next_id("room", "cache-ice"), "room.cache-ice-008")

    def test_high_water_marks_round_trip(self) -> None:
        minter = IdMinter()
        minter.next_id("room", "cache-ice")
        minter.next_id("room", "cache-ice")
        marks = minter.high_water_marks()
        self.assertEqual(marks[("room", "cache-ice")], 2)

        resumed = IdMinter(high_water_marks=marks)
        self.assertEqual(resumed.next_id("room", "cache-ice"), "room.cache-ice-003")


class PlanIdsForCellsTests(unittest.TestCase):
    def test_plan_ids_for_cells_mints_the_requested_count_per_cell(self) -> None:
        cells = [Cell("dungeon-room", ("cache", "ice"), "cache-ice"), Cell("dungeon-room", ("fight", "fire"), "fight-fire")]
        minter = IdMinter()
        result = plan_ids_for_cells(cells, "room", {"cache-ice": 2, "fight-fire": 1}, minter)
        self.assertEqual(result["cache-ice"], ["room.cache-ice-001", "room.cache-ice-002"])
        self.assertEqual(result["fight-fire"], ["room.fight-fire-001"])

    def test_a_cell_with_zero_requested_count_mints_nothing(self) -> None:
        cells = [Cell("dungeon-room", ("shrine", "none"), "shrine-none")]
        result = plan_ids_for_cells(cells, "room", {}, IdMinter())
        self.assertEqual(result["shrine-none"], [])


if __name__ == "__main__":
    unittest.main()
