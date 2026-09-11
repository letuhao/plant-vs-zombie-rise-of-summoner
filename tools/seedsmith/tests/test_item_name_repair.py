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


class NameRepairTests(unittest.TestCase):
    def test_plan_and_apply_change_only_the_losing_identity_fields(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "charms").mkdir()
            set_path = root / "sets" / "a.json"
            charm_path = root / "charms" / "a.json"
            set_path.write_text(json.dumps(_document("set", "set.a-001", "Shared Name")), encoding="utf-8")
            charm_path.write_text(json.dumps(_document("charm", "charm.a-001", "Shared Name")), encoding="utf-8")

            repairs = name_repair.plan(root)
            self.assertEqual([(repair.entry_id, repair.keeper_id) for repair in repairs],
                             [("set.a-001", "charm.a-001")])
            answers = {"set.a-001": {"name": "Distinct Name", "flavor": "new"}}
            changed = name_repair.apply(repairs, answers, write=True, items_root=root)

            self.assertEqual(changed, (set_path,))
            repaired = json.loads(set_path.read_text(encoding="utf-8"))["entries"][0]
            self.assertEqual(repaired["name"], "Distinct Name")
            self.assertEqual(repaired["nameKey"], "set.distinct-name")
            self.assertEqual(repaired["flavor"], "new")
            self.assertEqual(name_repair.plan(root), ())

    def test_replacement_may_not_reuse_any_current_name(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sets").mkdir()
            (root / "charms").mkdir()
            (root / "sets" / "a.json").write_text(
                json.dumps(_document("set", "set.a-001", "Shared Name")), encoding="utf-8")
            (root / "charms" / "a.json").write_text(
                json.dumps(_document("charm", "charm.a-001", "Shared Name")), encoding="utf-8")
            repairs = name_repair.plan(root)
            with self.assertRaisesRegex(ValueError, "already exists"):
                name_repair.apply(repairs, {"set.a-001": {"name": "Shared Name"}},
                                  write=False, items_root=root)
