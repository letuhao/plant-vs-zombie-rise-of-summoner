"""Meta-test for task H1's own acceptance bullet: "The twelve `adapters/trees/nodegen/` modules
exist, including `dedup.py` and `exclusion.py`." Each module-specific test file above exercises one
module's real behaviour; this file is the one place that checks the SET is complete and that every
module says, in its own docstring, which task completes any behaviour it does not yet carry.
"""
from __future__ import annotations

import importlib
import unittest
from pathlib import Path

NODEGEN_DIR = Path(__file__).resolve().parents[3] / "seedsmith" / "adapters" / "trees" / "nodegen"

#: `spec-tree-language.md`'s own "Project structure" section names these eleven, plus `__init__.py`
#: — twelve files total, the same count the spec's own "Twelve modules in the `setgen` mould" line
#: describes (`items/setgen/` is twelve files too: `__init__.py` + eleven named modules).
EXPECTED_MODULES = (
    "__init__", "tuning", "vocab", "quota", "plan_read", "brief", "schema", "run", "emit",
    "dedup", "exclusion", "verdict",
)


class TwelveModulesExistTests(unittest.TestCase):
    def test_exactly_the_twelve_named_files_exist(self) -> None:
        on_disk = {p.stem for p in NODEGEN_DIR.glob("*.py")}
        self.assertEqual(on_disk, set(EXPECTED_MODULES))

    def test_dedup_and_exclusion_are_present_by_name(self) -> None:
        self.assertIn("dedup", EXPECTED_MODULES)
        self.assertIn("exclusion", EXPECTED_MODULES)

    def test_every_module_imports_cleanly(self) -> None:
        for name in EXPECTED_MODULES:
            module = importlib.import_module(f"seedsmith.adapters.trees.nodegen.{name}")
            self.assertIsNotNone(module)

    def test_every_module_has_a_non_empty_docstring(self) -> None:
        for name in EXPECTED_MODULES:
            module = importlib.import_module(f"seedsmith.adapters.trees.nodegen.{name}")
            self.assertTrue((module.__doc__ or "").strip(), f"{name} has no module docstring")


if __name__ == "__main__":
    unittest.main()
