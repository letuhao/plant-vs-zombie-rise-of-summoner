"""Tests for seedsmith.adapters.items.uniques.briefs (D4.29, spec-unique-pipeline.md §1).

    python -m pytest tools/seedsmith/tests/test_unique_briefs.py -v

Covers the per-cell schema (PLANNED consts, VALIDATED closed enums against real registries),
`acquisition_for_band`'s own hard rule (ssot-uniques.md §4.5: "ordinal >= 90 is never plain drop"),
and `role_for_cell`'s own real-corpus-verified collision avoidance (`UniqueRuleCheck.cs`'s
8-role-per-frame quota + its `(band, role, axis)` `UniqueAxisCollision` key).
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.uniques.briefs import (  # noqa: E402
    ALLOWED_ROLES,
    DESCRIPTIONS,
    acquisition_for_band,
    assemble_tags,
    build_brief,
    build_unique_schema,
    load_base_types_by_frame_and_role,
    role_for_cell,
)
from seedsmith.adapters.items.uniques.planner import AXES, Cell  # noqa: E402

PLANNED_IDS = {"id": "unique.plant-offense-firstseed-001", "nameKey": "unique.example",
              "iconKey": "icon.unique.example", "flavorKey": "flavor.unique.example"}

FAKE_BASE_TYPES = {
    (frame, role): frozenset({f"item.{frame}-example-{role}-001", f"item.{frame}-example-{role}-002"})
    for frame in ("plant", "humanoid") for role in ALLOWED_ROLES
}
FAKE_ATOM_FAMILIES = frozenset({"atom.might", "atom.vitality", "atom.fortitude"})
FAKE_TAG_AXES = {
    "mass-class": ("light", "medium", "heavy"),
    "material-nature": ("organic", "metal"),
    "combat-posture": ("offensive", "defensive", "utility"),
    "origin": ("rooted", "undead"),
    "durability-class": ("sturdy", "fragile"),
}


def _schema(cell: Cell, planned_ids=None, role=None) -> dict:
    return build_unique_schema(
        cell, planned_ids or PLANNED_IDS, role=role,
        base_types_by_frame_and_role=FAKE_BASE_TYPES, atom_families=FAKE_ATOM_FAMILIES,
        tag_axes=FAKE_TAG_AXES)


class AcquisitionForBandTests(unittest.TestCase):
    def test_firstseed_is_drop(self) -> None:
        # ssot-uniques.md §4.5: `drop` is legal only at the rung-80 floor.
        self.assertEqual(acquisition_for_band("firstseed"), "drop")

    def test_sunwoven_and_almanac_are_deterministic_never_drop(self) -> None:
        self.assertEqual(acquisition_for_band("sunwoven"), "deterministic")
        self.assertEqual(acquisition_for_band("almanac"), "deterministic")

    def test_unknown_band_raises(self) -> None:
        with self.assertRaises(ValueError):
            acquisition_for_band("heirloom")


class RoleForCellTests(unittest.TestCase):
    def test_every_role_assigned_is_one_of_the_eight_allowed(self) -> None:
        for frame in ("plant", "humanoid"):
            for axis in AXES:
                cell = Cell(frame, axis, "firstseed", f"{frame}-{axis}-firstseed")
                self.assertIn(role_for_cell(cell), ALLOWED_ROLES)

    def test_plant_and_humanoid_never_share_a_role_at_the_same_axis(self) -> None:
        # UniqueRuleCheck.cs's UniqueAxisCollision keys on (band, role, axis) -- NOT frame -- so
        # a same-axis plant/humanoid pair sharing a role would collide with EACH OTHER.
        for axis in AXES:
            plant_role = role_for_cell(Cell("plant", axis, "firstseed", f"plant-{axis}-firstseed"))
            humanoid_role = role_for_cell(Cell("humanoid", axis, "firstseed", f"humanoid-{axis}-firstseed"))
            self.assertNotEqual(plant_role, humanoid_role, f"axis {axis} assigns the same role to both frames")

    def test_role_assignment_is_deterministic(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        self.assertEqual(role_for_cell(cell), role_for_cell(cell))

    def test_role_does_not_depend_on_band(self) -> None:
        # Band is irrelevant to the role assignment itself -- only axis and frame matter; the
        # band-90 exclusion is a decision made by the batch script, not by this function.
        a = role_for_cell(Cell("plant", "offense", "firstseed", "plant-offense-firstseed"))
        b = role_for_cell(Cell("plant", "offense", "almanac", "plant-offense-almanac"))
        self.assertEqual(a, b)


class LoadBaseTypesByFrameAndRoleTests(unittest.TestCase):
    def test_the_real_registry_yields_every_allowed_role_for_both_frames(self) -> None:
        # Live-corpus smoke test (no fixture) -- proves the loader actually reads real disk
        # content restricted to the 8 allowed roles, not just that a fake dict round-trips.
        real = load_base_types_by_frame_and_role()
        for frame in ("plant", "humanoid"):
            for role in ALLOWED_ROLES:
                self.assertIn((frame, role), real, f"no real base types found for ({frame}, {role})")
                self.assertGreater(len(real[(frame, role)]), 0)

    def test_a_forbidden_role_never_appears_in_the_returned_keys(self) -> None:
        real = load_base_types_by_frame_and_role()
        roles_present = {role for (_, role) in real}
        self.assertEqual(roles_present, set(ALLOWED_ROLES))


class SchemaPlannedFieldsTests(unittest.TestCase):
    def test_planned_fields_are_pinned_const_never_a_free_choice(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        for field, expected in (("id", PLANNED_IDS["id"]), ("frame", "plant"),
                                ("powerAxis", "offense"), ("rarity", "firstseed"),
                                ("acquisition", "drop")):
            node = schema["properties"][field]
            self.assertEqual(node.get("const"), expected, f"{field} must be const {expected!r}")
            self.assertNotIn("enum", node, f"{field} is PLANNED -- it must never expose a free enum")

    def test_acquisition_tracks_the_bands_own_rule_in_the_schema_too(self) -> None:
        for band, expected in (("firstseed", "drop"), ("sunwoven", "deterministic"), ("almanac", "deterministic")):
            cell = Cell("plant", "offense", band, f"plant-offense-{band}")
            schema = _schema(cell)
            self.assertEqual(schema["properties"]["acquisition"]["const"], expected)


class SchemaValidatedFieldsTests(unittest.TestCase):
    def test_base_type_enum_is_restricted_to_the_cells_own_role_not_the_whole_frame(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        expected_role = role_for_cell(cell)
        self.assertEqual(set(schema["properties"]["baseType"]["enum"]), FAKE_BASE_TYPES[("plant", expected_role)])

    def test_an_explicit_role_override_replaces_the_grid_default(self) -> None:
        # Real finding 2026-09-06: naming.v1.json's idNamespaces.uniques.bandAssignment has no row
        # for ordinal 80/100 at all, so shipping under a REAL registered ordinal (70) needs a role
        # picked from THAT ordinal's own free (role, axis) slots -- never role_for_cell's grid
        # rotation, which assumes an empty space that does not exist for these rungs.
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        default_role = role_for_cell(cell)
        override_role = next(r for r in ALLOWED_ROLES if r != default_role)
        schema = _schema(cell, role=override_role)
        self.assertEqual(set(schema["properties"]["baseType"]["enum"]), FAKE_BASE_TYPES[("plant", override_role)])

    def test_atom_family_enum_matches_the_real_supplied_catalog(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        family_enum = set(schema["properties"]["fixedAtoms"]["items"]["properties"]["family"]["enum"])
        self.assertEqual(family_enum, FAKE_ATOM_FAMILIES)
        variance_family_enum = set(schema["properties"]["varianceSlot"]["properties"]["family"]["enum"])
        self.assertEqual(variance_family_enum, FAKE_ATOM_FAMILIES)

    def test_the_three_required_axes_are_single_valued_enums_not_an_array(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        for field, axis in (("massClass", "mass-class"), ("materialNature", "material-nature"),
                           ("combatPosture", "combat-posture")):
            node = schema["properties"][field]
            self.assertEqual(node["type"], "string", f"{field} must be a single string, not an array")
            self.assertEqual(set(node["enum"]), set(FAKE_TAG_AXES[axis]))
            self.assertIn(field, schema["required"])

    def test_origin_and_durability_class_are_optional_via_a_none_member(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        for field, axis in (("origin", "origin"), ("durabilityClass", "durability-class")):
            node = schema["properties"][field]
            self.assertIn("none", node["enum"])
            self.assertEqual(set(node["enum"]) - {"none"}, set(FAKE_TAG_AXES[axis]))

    def test_counter_pressure_kind_is_narrow_only_this_batch(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        self.assertEqual(schema["properties"]["counterPressure"]["properties"]["kind"]["enum"], ["narrow"])

    def test_fixed_atoms_array_bounds_match_the_real_tunable_ceiling(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        self.assertEqual(schema["properties"]["fixedAtoms"]["minItems"], 1)
        self.assertEqual(schema["properties"]["fixedAtoms"]["maxItems"], 3)

    def test_fixed_atoms_forbids_a_duplicate_family_and_power_band_pair(self) -> None:
        # Self-caught 2026-09-06: a real smoke call against the local model returned the SAME
        # {family, powerBand} pair twice as two "different" fixed atoms -- schema-legal (each
        # item was individually valid) but nonsensical content. `uniqueItems` makes the exact
        # duplicate unsampleable under constrained decoding rather than relying on a post-hoc check.
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        self.assertTrue(schema["properties"]["fixedAtoms"]["uniqueItems"])


class SchemaShapeTests(unittest.TestCase):
    def test_every_required_field_is_present_and_additional_properties_is_closed(self) -> None:
        cell = Cell("humanoid", "control", "almanac", "humanoid-control-almanac")
        schema = _schema(cell)
        self.assertFalse(schema["additionalProperties"])
        for field in schema["required"]:
            self.assertIn(field, schema["properties"], f"required field {field} has no schema node")

    def test_every_field_shown_to_the_model_has_a_negative_clause_description(self) -> None:
        for field, text in DESCRIPTIONS.items():
            self.assertTrue("NOT" in text or "not " in text.lower(), f"{field} description has no negative clause: {text!r}")


class AssembleTagsTests(unittest.TestCase):
    def test_assembles_the_three_required_axes_plus_signature(self) -> None:
        entry = {"massClass": "light", "materialNature": "organic", "combatPosture": "offensive",
                "origin": "none", "durabilityClass": "none"}
        tags = assemble_tags(entry)
        self.assertEqual(tags, ["light", "organic", "offensive", "signature"])

    def test_a_real_origin_and_durability_are_both_included(self) -> None:
        entry = {"massClass": "heavy", "materialNature": "metal", "combatPosture": "defensive",
                "origin": "mechanical", "durabilityClass": "sturdy"}
        tags = assemble_tags(entry)
        self.assertEqual(tags, ["heavy", "metal", "defensive", "mechanical", "sturdy", "signature"])

    def test_the_five_intermediate_fields_are_removed_from_the_final_entry(self) -> None:
        entry = {"massClass": "light", "materialNature": "organic", "combatPosture": "offensive",
                "origin": "none", "durabilityClass": "none", "name": "Example"}
        assemble_tags(entry)
        for field in ("massClass", "materialNature", "combatPosture", "origin", "durabilityClass"):
            self.assertNotIn(field, entry)
        self.assertIn("tags", entry)
        self.assertEqual(entry["name"], "Example")

    def test_the_assembled_result_never_violates_the_one_per_axis_rule(self) -> None:
        # The structural guarantee this whole redesign exists for: every legal combination of
        # single-enum inputs produces a tags[] with at most one member per exclusive axis.
        from seedsmith.adapters.items.uniques.pipelines import _tag_axis_violation
        entry = {"massClass": "heavy", "materialNature": "metal", "combatPosture": "utility",
                "origin": "undead", "durabilityClass": "fragile"}
        tags = assemble_tags(entry)
        self.assertIsNone(_tag_axis_violation(tags, FAKE_TAG_AXES))


class BuildBriefTests(unittest.TestCase):
    def test_brief_names_frame_axis_band_and_the_assigned_role(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        schema = _schema(cell)
        brief = build_brief(cell, schema)
        self.assertIsInstance(brief, str)
        self.assertIn(cell.frame, brief)
        self.assertIn(cell.axis, brief)
        self.assertIn(cell.band, brief)
        self.assertIn(role_for_cell(cell), brief)

    def test_an_explicit_role_override_replaces_the_grid_default_in_the_brief_too(self) -> None:
        cell = Cell("plant", "offense", "firstseed", "plant-offense-firstseed")
        default_role = role_for_cell(cell)
        override_role = next(r for r in ALLOWED_ROLES if r != default_role)
        schema = _schema(cell, role=override_role)
        brief = build_brief(cell, schema, role=override_role)
        self.assertIn(override_role, brief)
        self.assertNotIn(default_role, brief)


if __name__ == "__main__":
    unittest.main()
