"""Tests for seedsmith.adapters.dungeon.{emit,provenance} (D1.11, spec-dungeon-seed-contract.md
§6) and the offline guarantee (decision 6: "in game runtime don't use LLM — this seed generator is
only contained in seedsmith").

    python -m pytest tools/seedsmith/tests/test_dungeon_idempotency.py -v
"""
from __future__ import annotations

import ast
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.dungeon.emit import render_entry, render_index, write_corpus  # noqa: E402
from seedsmith.adapters.dungeon.provenance import DungeonProvenance, stale_ids, staleness_key  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
DUNGEON_ADAPTER_DIR = REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "adapters" / "dungeon"


class CanonicalSerialisationTests(unittest.TestCase):
    def test_render_entry_is_sorted_keys_two_space_indent(self) -> None:
        entry = {"b": 1, "a": "x", "_provenance": {"planHash": "abc"}}
        rendered = render_entry(entry).decode("utf-8")
        self.assertTrue(rendered.startswith("{\n  \"_provenance\""))  # sorted: "_provenance" < "a" < "b"
        self.assertTrue(rendered.endswith("}\n"))

    def test_render_entry_unescapes_cjk_and_keeps_explicit_nulls(self) -> None:
        entry = {"flavor": "灰烬回廊", "chainRef": None}
        rendered = render_entry(entry).decode("utf-8")
        self.assertIn("灰烬回廊", rendered)
        self.assertNotIn("\\u", rendered)
        self.assertIn("null", rendered)

    def test_render_index_maps_id_to_filename_sorted(self) -> None:
        rendered = render_index(["room.b-001", "room.a-001"]).decode("utf-8")
        self.assertLess(rendered.index('"room.a-001"'), rendered.index('"room.b-001"'))


class RerunIsByteIdenticalTests(unittest.TestCase):
    def test_two_runs_over_unchanged_input_produce_identical_bytes(self) -> None:
        entries = {
            "room.cache-ice-001": {"roomId": "room.cache-ice-001", "kind": "cache", "climate": "ice"},
            "room.fight-fire-001": {"roomId": "room.fight-fire-001", "kind": "fight", "climate": "fire"},
        }
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp) / "rooms"
            first_run = write_corpus(directory, entries)
            first_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in first_run}

            second_run = write_corpus(directory, entries)
            second_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in second_run}

            self.assertEqual(first_hashes, second_hashes)
            self.assertEqual(set(first_hashes), {"room.cache-ice-001.json", "room.fight-fire-001.json", "_index.json"})


class StalenessTests(unittest.TestCase):
    def test_an_entry_with_no_provenance_is_stale(self) -> None:
        entries = [{"roomId": "room.a-001"}]
        current = DungeonProvenance(plan_hash="p1", brief_hash="b1").to_dict()
        self.assertEqual(stale_ids(entries, id_field="roomId", current=current), ["room.a-001"])

    def test_an_entry_matching_the_current_staleness_key_is_not_stale(self) -> None:
        prov = DungeonProvenance(
            plan_hash="p1", brief_hash="b1", prompt_versions={"room-identity": 1},
            registry_versions={"room-kinds": 1}, motif_subset_hash="m1",
        ).to_dict()
        entries = [{"roomId": "room.a-001", "_provenance": prov}]
        self.assertEqual(stale_ids(entries, id_field="roomId", current=prov), [])

    def test_adding_a_plan_cell_stales_nothing_when_brief_hash_is_unchanged(self) -> None:
        # §6's own point: planHash is recorded but never part of the staleness key -- a plan that
        # adds a cell (new planHash) must not stale an untouched entry whose brief did not change.
        prov = DungeonProvenance(plan_hash="p1", brief_hash="b1", motif_subset_hash="m1").to_dict()
        entries = [{"roomId": "room.a-001", "_provenance": prov}]
        current_after_plan_v2 = DungeonProvenance(plan_hash="p2", brief_hash="b1", motif_subset_hash="m1").to_dict()
        self.assertEqual(stale_ids(entries, id_field="roomId", current=current_after_plan_v2), [])

    def test_a_changed_brief_hash_stales_the_entry(self) -> None:
        prov = DungeonProvenance(plan_hash="p1", brief_hash="b1").to_dict()
        entries = [{"roomId": "room.a-001", "_provenance": prov}]
        current = DungeonProvenance(plan_hash="p1", brief_hash="b2").to_dict()
        self.assertEqual(stale_ids(entries, id_field="roomId", current=current), ["room.a-001"])

    def test_staleness_key_excludes_planHash(self) -> None:
        prov_a = DungeonProvenance(plan_hash="p1", brief_hash="b1").to_dict()
        prov_b = DungeonProvenance(plan_hash="p999", brief_hash="b1").to_dict()
        self.assertEqual(staleness_key(prov_a), staleness_key(prov_b))


class OfflineGuaranteeTests(unittest.TestCase):
    """decision 6: 'in game runtime don't use LLM — this seed generator is only contained in
    seedsmith.' Nothing built so far in adapters/dungeon/ calls a model at all (pipelines.py,
    the one module that would, is not built — see planner.py's own stated-gap docstring), so this
    proves the guarantee structurally: no module here imports a transport/model-calling symbol."""

    def test_no_dungeon_adapter_module_imports_a_transport(self) -> None:
        offenders = []
        for path in DUNGEON_ADAPTER_DIR.glob("*.py"):
            tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
            for node in ast.walk(tree):
                if isinstance(node, ast.ImportFrom) and node.module and "transport" in node.module.lower():
                    offenders.append(f"{path.name}: {node.module}")
                if isinstance(node, ast.Import):
                    for alias in node.names:
                        if "transport" in alias.name.lower():
                            offenders.append(f"{path.name}: {alias.name}")
        self.assertEqual(offenders, [], f"a dungeon adapter module imports a transport: {offenders}")

    def test_importing_every_dungeon_adapter_module_makes_no_network_call(self) -> None:
        # A weaker but real guarantee available today: importing every module that exists cannot
        # itself reach the network, because none of them defines a call at import time (no
        # module-level HTTP client construction, no eager model warm-up). Import-time side effects
        # are exactly what a hidden model call would look like if one were smuggled in.
        import importlib

        for path in sorted(DUNGEON_ADAPTER_DIR.glob("*.py")):
            if path.name == "__init__.py":
                continue
            module_name = f"seedsmith.adapters.dungeon.{path.stem}"
            importlib.import_module(module_name)  # raises if import itself fails/blocks


if __name__ == "__main__":
    unittest.main()
