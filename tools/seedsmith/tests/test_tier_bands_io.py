"""Tests for seedsmith.numerics.tier_bands_io (tasks/seedsmith-todo.md, S5).

    python -m pytest tools/seedsmith/tests/test_tier_bands_io.py -v
"""
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.numerics import OpWeight, TierBands  # noqa: E402
from seedsmith.numerics import tier_bands_io  # noqa: E402


class LiveTierBandsFileTests(unittest.TestCase):
    def test_loads_the_real_v1_file(self) -> None:
        tuning = TierBands.load("latest")
        self.assertEqual(tuning.version, 1)
        self.assertEqual(tuning.base_share_permille, 35)
        self.assertEqual(len(tuning.channel_weight_permille), 14)
        self.assertEqual(tuning.op_weight_permille[OpWeight.MORE], 550)

    def test_all_fourteen_primary_channels_are_present(self) -> None:
        from seedsmith.adapters.items.channels import PRIMARY_CHANNEL_IDS
        tuning = TierBands.load("latest")
        self.assertEqual(set(tuning.channel_weight_permille), PRIMARY_CHANNEL_IDS)


class RoundTripTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp())

    def test_save_then_load_round_trips(self) -> None:
        original = TierBands(version=2, base_share_permille=36,
                             channel_weight_permille={"vitality": 900, "might": 1100})
        tier_bands_io.save(original, tuning_dir=self.tmp)
        loaded = tier_bands_io.load(2, tuning_dir=self.tmp)

        self.assertEqual(loaded.version, 2)
        self.assertEqual(loaded.base_share_permille, 36)
        self.assertEqual(loaded.channel_weight_permille, {"vitality": 900, "might": 1100})

    def test_latest_picks_the_highest_version(self) -> None:
        for v in (1, 2, 3):
            tier_bands_io.save(
                TierBands(version=v, base_share_permille=35, channel_weight_permille={"x": 1000}),
                tuning_dir=self.tmp)
        self.assertEqual(tier_bands_io.load("latest", tuning_dir=self.tmp).version, 3)

    def test_publishing_over_an_existing_version_raises(self) -> None:
        tuning = TierBands(version=1, base_share_permille=35, channel_weight_permille={"x": 1000})
        tier_bands_io.save(tuning, tuning_dir=self.tmp)
        with self.assertRaises(FileExistsError):
            tier_bands_io.save(tuning, tuning_dir=self.tmp)


if __name__ == "__main__":
    unittest.main()
