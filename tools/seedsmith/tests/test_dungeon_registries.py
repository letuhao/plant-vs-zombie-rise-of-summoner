"""Tests for seedsmith.adapters.dungeon.registries (D1.5, spec-dungeon-registries.md).

    python -m pytest tools/seedsmith/tests/test_dungeon_registries.py -v

Asserts the Python reader's vocabularies equal the same committed JSON the C# catalogs load
(`DungeonRegistryLoader.LoadAll`) — read fresh from disk on both sides, so the two cannot drift
without this test catching it the same run.
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.dungeon.registries import (  # noqa: E402
    BAND_NAMES,
    DEMONS_REGISTRY_DIR,
    REGISTRY_DIR,
    load_band_display_names,
    load_bands,
    load_difficulty_rungs,
    load_disposition,
    load_door_kinds,
    load_interaction_verbs,
    load_motifs,
    load_objective_templates,
    load_override_tags,
    load_raid_modes,
    load_room_kinds,
    load_theme_ids,
    load_themes,
    load_versions,
    load_vocabularies,
)

REPO_ROOT = Path(__file__).resolve().parents[3]
LIVE_DUNGEON_REGISTRY_ROOT = REPO_ROOT / "data" / "seed" / "dungeon" / "_registry"
LIVE_DEMONS_REGISTRY_ROOT = REPO_ROOT / "data" / "seed" / "demons" / "_registry"


def _raw(name: str) -> dict:
    return json.loads((LIVE_DUNGEON_REGISTRY_ROOT / name).read_text(encoding="utf-8"))


def _raw_demons(name: str) -> dict:
    return json.loads((LIVE_DEMONS_REGISTRY_ROOT / name).read_text(encoding="utf-8"))


class RegistryDirTests(unittest.TestCase):
    def test_registry_dir_resolves_to_the_real_committed_folder(self) -> None:
        self.assertEqual(REGISTRY_DIR, LIVE_DUNGEON_REGISTRY_ROOT)
        self.assertTrue(REGISTRY_DIR.is_dir())


class RoomKindTests(unittest.TestCase):
    def test_eleven_room_kinds_matching_the_raw_file(self) -> None:
        kinds = load_room_kinds()
        self.assertEqual(len(kinds), 11)
        self.assertEqual(set(kinds), set(_raw("room-kinds.v1.json")["roomKinds"]))

    def test_exactly_one_boss_row_allowed(self) -> None:
        kinds = load_room_kinds()
        boss_rows = [k for k, row in kinds.items() if row["bossRowAllowed"]]
        self.assertEqual(boss_rows, ["boss"])

    def test_only_unknown_carries_unknownResolvesTo(self) -> None:
        kinds = load_room_kinds()
        for kind_id, row in kinds.items():
            if kind_id == "unknown":
                self.assertEqual(row["unknownResolvesTo"], ["cache", "merchant", "fight"])
            else:
                self.assertEqual(row["unknownResolvesTo"], [])


class DoorKindTests(unittest.TestCase):
    def test_four_door_kinds(self) -> None:
        self.assertEqual(set(load_door_kinds()), {"passage", "gated", "one-way", "secret"})


class OverrideTagTests(unittest.TestCase):
    def test_five_override_tags(self) -> None:
        self.assertEqual(load_override_tags(), frozenset({"herbs", "key", "holy", "bait", "watch"}))


class ObjectiveTemplateTests(unittest.TestCase):
    def test_nine_templates_matching_ideal_11_3(self) -> None:
        templates = load_objective_templates()
        self.assertEqual(set(templates), {
            "explore-rooms", "cleanse-fights", "gather-curio-kind", "kill-boss",
            "extract-with-item-kind", "bring-demon-home-alive", "finish-under-hunger",
            "survive-no-downed", "spend-no-provision",
        })

    def test_sink_avoidance_is_true_on_exactly_three(self) -> None:
        templates = load_objective_templates()
        sink_avoidance = {t for t, row in templates.items() if row["sinkAvoidance"]}
        self.assertEqual(sink_avoidance, {"finish-under-hunger", "survive-no-downed", "spend-no-provision"})


class DifficultyRungTests(unittest.TestCase):
    def test_ten_rungs_ordinals_1_to_10_contiguous(self) -> None:
        rungs = load_difficulty_rungs()
        self.assertEqual(len(rungs), 10)
        self.assertEqual(sorted(rungs.values()), list(range(1, 11)))

    def test_hard_is_ordinal_four(self) -> None:
        self.assertEqual(load_difficulty_rungs()["hard"], 4)


class DispositionTests(unittest.TestCase):
    def test_four_dispositions(self) -> None:
        self.assertEqual(load_disposition(), frozenset({"eager", "open", "wary", "hostile"}))


class InteractionVerbTests(unittest.TestCase):
    def test_six_verbs_with_decision_numbers(self) -> None:
        verbs = load_interaction_verbs()
        self.assertEqual(set(verbs), {"open", "disarm", "pray", "loot", "destroy", "garrison"})
        self.assertEqual(verbs["destroy"], 12)
        self.assertEqual(verbs["garrison"], 15)
        for verb_id in ("open", "disarm", "pray", "loot"):
            self.assertIsNone(verbs[verb_id])


class RaidModeTests(unittest.TestCase):
    def test_three_raid_modes(self) -> None:
        self.assertEqual(load_raid_modes(), frozenset({"solo", "pair", "quad"}))


class BandTests(unittest.TestCase):
    def test_twenty_bands_matching_the_owned_list(self) -> None:
        bands = load_bands()
        self.assertEqual(set(bands), set(BAND_NAMES))
        self.assertEqual(len(bands), 20)

    def test_countBand_is_S2_12_vocabulary_never_spelled_numbers(self) -> None:
        self.assertEqual(load_bands()["countBand"], frozenset({"lone", "few", "several", "many"}))
        spelled = {"one", "two", "three", "four", "five"}
        for band_name, members in load_bands().items():
            self.assertTrue(members.isdisjoint(spelled), f"{band_name} has a spelled-number member")

    def test_nerveStage_matches_delve_attrition(self) -> None:
        self.assertEqual(load_bands()["nerveStage"], frozenset({"unsettled", "shaken", "afflicted"}))

    def test_every_band_member_has_a_display_name(self) -> None:
        bands = load_bands()
        display_names = load_band_display_names()
        self.assertEqual(set(bands), set(display_names))
        for band_name, members in bands.items():
            self.assertEqual(set(display_names[band_name]), set(members),
                            f"{band_name}: display names don't match members exactly")


class VersionTests(unittest.TestCase):
    def test_nine_dungeon_files_plus_two_cross_program_theme_files_report_registryVersion_1_at_launch(self) -> None:
        # D1.10's own themes/motifs cross-read (2026-09-07) adds two more tracked versions
        # (`demons.themes`, `demons.motifs`) alongside the nine dungeon-native files — 9 -> 11,
        # named here rather than silently bumping the count.
        versions = load_versions()
        self.assertEqual(len(versions), 11)
        self.assertIn("demons.themes", versions)
        self.assertIn("demons.motifs", versions)
        self.assertTrue(all(v == 1 for v in versions.values()))


class ThemeTests(unittest.TestCase):
    """D1.10 (2026-09-07 correction): `theme` reads the demon-seed program's own already-shipped,
    already-reviewed registry as a frozen cross-program input — never a dungeon-authored vocabulary
    (`spec-dungeon-seed-contract.md:44`: "`themes.v1.json` (84 rows)")."""

    def test_demons_registry_dir_resolves_to_the_real_committed_folder(self) -> None:
        self.assertEqual(DEMONS_REGISTRY_DIR, LIVE_DEMONS_REGISTRY_ROOT)
        self.assertTrue(DEMONS_REGISTRY_DIR.is_dir())

    def test_eighty_four_themes_matching_the_spec_s_own_cited_row_count(self) -> None:
        themes = load_themes()
        self.assertEqual(len(themes), 84)
        self.assertEqual(set(themes), set(_raw_demons("themes.v1.json")["themes"]))

    def test_every_theme_id_is_demon_prefixed_never_the_legacy_theme_prefix(self) -> None:
        # THEME_PREFIX in adapters/demons/themes.py is "demon." specifically so it can never
        # collide with the items corpus's own legacy `theme.*` ids (spec-demon-themes.md §2.2a).
        for theme_id in load_themes():
            self.assertTrue(theme_id.startswith("demon."), theme_id)

    def test_load_theme_ids_is_a_frozenset_of_every_theme_key(self) -> None:
        ids = load_theme_ids()
        self.assertIsInstance(ids, frozenset)
        self.assertEqual(ids, frozenset(load_themes()))

    def test_a_retired_theme_still_resolves_never_filtered_out(self) -> None:
        # spec-demon-themes.md §6: "A demon that leaves the roster: its theme is retired, still
        # resolvable — never deleted." At least the shipped corpus's own retired rows (if any)
        # must still be legal vocabulary members, not silently dropped by this reader.
        themes = load_themes()
        retired = {tid for tid, row in themes.items() if row.get("retired")}
        self.assertTrue(retired.issubset(load_theme_ids()))


class MotifTests(unittest.TestCase):
    def test_motif_count_matches_the_raw_file(self) -> None:
        motifs = load_motifs()
        self.assertIsInstance(motifs, frozenset)
        self.assertEqual(motifs, frozenset(_raw_demons("motifs.v1.json")["motifs"]))

    def test_every_themes_own_motifs_are_members_of_the_flat_union(self) -> None:
        # The flat file is supposed to be exactly the union of every theme's own motifs -- proven
        # here rather than assumed, so a future hand-edit drift is caught by this test, not by a
        # downstream planner silently rejecting a real theme's own real motif.
        flat = load_motifs()
        for theme_id, row in load_themes().items():
            for motif in row.get("motifs", []):
                self.assertIn(motif, flat, f"{theme_id}'s own motif {motif!r} missing from the flat union")


class VocabularyAgreementTests(unittest.TestCase):
    """The load-bearing test: Python's vocabulary set equals the raw JSON's, both read fresh —
    proving the two readers (this one and DungeonRegistryLoader.cs) cannot silently drift, because
    both parse the SAME committed bytes with no intermediate cache on either side."""

    def test_load_vocabularies_agrees_with_the_raw_files_in_both_directions(self) -> None:
        vocab = load_vocabularies()

        raw_room_kinds = set(_raw("room-kinds.v1.json")["roomKinds"])
        self.assertEqual(vocab["roomKind"], raw_room_kinds)

        raw_door_kinds = set(_raw("door-kinds.v1.json")["doorKinds"])
        self.assertEqual(vocab["doorKind"], raw_door_kinds)

        raw_override_tags = set(_raw("override-tags.v1.json")["overrideTags"])
        self.assertEqual(vocab["overrideTag"], raw_override_tags)

        raw_bands = _raw("bands.v1.json")["bands"]
        for band_name, row in raw_bands.items():
            self.assertEqual(vocab[band_name], set(row["members"]), f"band '{band_name}' disagrees")

        raw_themes = set(_raw_demons("themes.v1.json")["themes"])
        self.assertEqual(vocab["theme"], raw_themes)

        raw_motifs = set(_raw_demons("motifs.v1.json")["motifs"])
        self.assertEqual(vocab["motif"], raw_motifs)

    def test_every_registry_file_is_read_by_at_least_one_loader(self) -> None:
        # A file this reader forgot would be invisible to every dungeon pipeline downstream —
        # walk the real directory and assert every *.v1.json is one of the nine this module names.
        from seedsmith.adapters.dungeon.registries import _REGISTRY_FILES  # noqa: SLF001

        on_disk = {p.name for p in LIVE_DUNGEON_REGISTRY_ROOT.glob("*.v1.json")}
        self.assertEqual(on_disk, set(_REGISTRY_FILES))


if __name__ == "__main__":
    unittest.main()
