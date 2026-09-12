from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.setgen import name_repair  # noqa: E402


def _document(kind: str, entry_id: str, name: str) -> dict:
    return {"schemaVersion": 1, "kind": kind, "entries": [
        {"id": entry_id, "name": name, "nameKey": f"{kind}.old", "flavor": "old"}]}


def _groups(*, key: str, members: list[tuple[str, str]]) -> list[dict]:
    """A collision group in the validator's own --collision-groups shape."""
    return [{"key": key, "members": [{"id": i, "name": n, "kind": "set"} for i, n in members]}]


class NameRepairTests(unittest.TestCase):
    def test_plan_and_apply_change_only_the_losing_identity_fields(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            set_path = root / "sets" / "a.json"
            set_path.write_text(json.dumps(_document("set", "set.b-002", "Shared Name")),
                                encoding="utf-8")

            repairs = name_repair.plan(
                root, groups=_groups(key="shared name", members=[("set.a-001", "Shared Name"),
                                                                 ("set.b-002", "Shared Name")]))
            self.assertEqual([(r.entry_id, r.keeper_id) for r in repairs],
                             [("set.b-002", "set.a-001")])
            answers = {"set.b-002": {"name": "Distinct Name", "flavor": "new"}}
            changed = name_repair.apply(repairs, answers, write=True, items_root=root)

            self.assertEqual(changed, (set_path,))
            repaired = json.loads(set_path.read_text(encoding="utf-8"))["entries"][0]
            self.assertEqual(repaired["name"], "Distinct Name")
            self.assertEqual(repaired["nameKey"], "set.distinct-name")
            self.assertEqual(repaired["flavor"], "new")
            self.assertEqual(repaired["id"], "set.b-002")  # identity preserved (seed-contract 7.2)

    def test_the_keeper_is_the_lexically_first_id_in_the_group(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            for eid in ("set.m-003", "set.a-001", "set.z-009"):
                (root / "sets" / f"{eid}.json").write_text(
                    json.dumps(_document("set", eid, "Same Idea")), encoding="utf-8")
            repairs = name_repair.plan(
                root, groups=_groups(key="same idea", members=[
                    ("set.z-009", "Same Idea"), ("set.a-001", "Same Idea"),
                    ("set.m-003", "Same Idea")]))
            self.assertEqual({r.entry_id for r in repairs}, {"set.m-003", "set.z-009"})
            self.assertTrue(all(r.keeper_id == "set.a-001" for r in repairs))

    def test_replacement_may_not_reuse_any_current_name(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "sets" / "a.json").write_text(
                json.dumps(_document("set", "set.b-002", "Shared Name")), encoding="utf-8")
            repairs = name_repair.plan(
                root, groups=_groups(key="shared name",
                                     members=[("set.a-001", "Shared Name"),
                                              ("set.b-002", "Shared Name")]))
            with self.assertRaisesRegex(ValueError, "already exists"):
                name_repair.apply(repairs, {"set.b-002": {"name": "Shared Name"}},
                                  write=False, items_root=root)

    def test_an_unsluggable_replacement_is_refused_before_write(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "sets" / "a.json").write_text(
                json.dumps(_document("set", "set.b-002", "Shared Name")), encoding="utf-8")
            repairs = name_repair.plan(
                root, groups=_groups(key="shared name",
                                     members=[("set.a-001", "Shared Name"),
                                              ("set.b-002", "Shared Name")]))
            with self.assertRaisesRegex(ValueError, "no ASCII slug"):
                name_repair.apply(repairs, {"set.b-002": {"name": "毁灭豌豆之友"}},
                                  write=False, items_root=root)

    def test_answers_must_cover_exactly_the_planned_rows(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "sets" / "a.json").write_text(
                json.dumps(_document("set", "set.b-002", "Shared Name")), encoding="utf-8")
            repairs = name_repair.plan(
                root, groups=_groups(key="shared name",
                                     members=[("set.a-001", "Shared Name"),
                                              ("set.b-002", "Shared Name")]))
            with self.assertRaisesRegex(ValueError, "must name exactly"):
                name_repair.apply(repairs, {}, write=False, items_root=root)

    def test_a_single_member_group_is_not_a_collision(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "sets" / "a.json").write_text(
                json.dumps(_document("set", "set.a-001", "Lonely Name")), encoding="utf-8")
            repairs = name_repair.plan(
                root, groups=_groups(key="lonely name", members=[("set.a-001", "Lonely Name")]))
            self.assertEqual(repairs, ())


class RealCorpusPlanTests(unittest.TestCase):
    """The real plan, computed from the validator's own --collision-groups output."""

    def test_the_real_plan_is_non_empty_and_covers_every_colliding_kind(self) -> None:
        repairs = name_repair.plan()
        self.assertTrue(repairs, "the real corpus has collisions, so the plan must not be empty")
        kinds = {r.kind for r in repairs}
        # At least the two kinds the original tool never reached because it exact-matched only
        # sets+charms and found zero.
        self.assertTrue(kinds & {"base-type", "drop-table", "gem", "combination", "set", "charm"})

    def test_no_plan_row_is_its_own_keeper(self) -> None:
        for repair in name_repair.plan():
            self.assertNotEqual(repair.entry_id, repair.keeper_id,
                                "a row cannot keep a name it is being renamed away from")


if __name__ == "__main__":
    unittest.main()
