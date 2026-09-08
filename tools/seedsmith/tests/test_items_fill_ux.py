"""Tests for items fill UX + `.env` write defaults (seedsmith-cli-ux).

    python -m pytest tools/seedsmith/tests/test_items_fill_ux.py -q
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items import defaults as defaults_mod  # noqa: E402
from seedsmith.adapters.items import fill as fill_mod  # noqa: E402
from seedsmith.pipeline.llm_caller import (  # noqa: E402
    resolve_dotenv_path,
    resolve_live_transport,
)
from seedsmith.report import cli as cli_mod  # noqa: E402


class ResolveDotenvPathTests(unittest.TestCase):
    def test_explicit_path_wins_even_when_absent(self) -> None:
        missing = Path(tempfile.mkdtemp()) / "no.env"
        self.assertEqual(resolve_dotenv_path(missing), missing)

    def test_default_resolution_prefers_existing_cwd_or_package(self) -> None:
        packaged = Path(__file__).resolve().parents[1] / ".env"
        cwd_env = Path(".env")
        resolved = resolve_dotenv_path(None)
        if cwd_env.exists():
            self.assertEqual(resolved.resolve(), cwd_env.resolve())
        elif packaged.exists():
            self.assertEqual(resolved.resolve(), packaged.resolve())
        else:
            self.assertEqual(resolved, Path(".env"))


class ResolveLiveTransportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp())
        self.no_dotenv = self.tmp / "absent.env"

    def test_cli_endpoint_wins(self) -> None:
        cfg = resolve_live_transport(
            "http://cli-only:1/v1", "", dotenv_path=self.no_dotenv,
            toml_path=self.tmp / "no.toml")
        self.assertEqual(cfg.endpoint, "http://cli-only:1/v1")

    def test_empty_cli_uses_dotenv(self) -> None:
        dotenv = self.tmp / ".env"
        dotenv.write_text(
            "SEEDSMITH_LLM_ENDPOINT=http://from-env:9/v1\nSEEDSMITH_LLM_MODEL=env/model\n",
            encoding="utf-8")
        cfg = resolve_live_transport("", "", dotenv_path=dotenv, toml_path=self.tmp / "no.toml")
        self.assertEqual(cfg.endpoint, "http://from-env:9/v1")
        self.assertEqual(cfg.model, "env/model")

    def test_unrecorded_model_falls_through(self) -> None:
        dotenv = self.tmp / ".env"
        dotenv.write_text("SEEDSMITH_LLM_MODEL=env/model\n", encoding="utf-8")
        cfg = resolve_live_transport(
            "http://x", "unrecorded", dotenv_path=dotenv, toml_path=self.tmp / "no.toml")
        self.assertEqual(cfg.model, "env/model")


class ItemsDefaultsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp())

    def test_allow_production_from_env(self) -> None:
        dotenv = self.tmp / ".env"
        dotenv.write_text("SEEDSMITH_ALLOW_PRODUCTION_TREE=1\n", encoding="utf-8")
        self.assertTrue(defaults_mod.allow_production_tree(dotenv_path=dotenv))
        dotenv.write_text("SEEDSMITH_ALLOW_PRODUCTION_TREE=0\n", encoding="utf-8")
        self.assertFalse(defaults_mod.allow_production_tree(dotenv_path=dotenv))

    def test_default_out_dir_for_set(self) -> None:
        path = defaults_mod.default_out_dir("set", dotenv_path=self.tmp / "absent.env")
        self.assertTrue(path.replace("\\", "/").endswith("data/seed/items/sets"))

    def test_resolve_out_dir_requires_allow(self) -> None:
        self.assertEqual(
            defaults_mod.resolve_out_dir_arg("set", "", allow_production=False,
                                             dotenv_path=self.tmp / "x"),
            "")
        self.assertTrue(
            defaults_mod.resolve_out_dir_arg("set", "", allow_production=True,
                                             dotenv_path=self.tmp / "x"))


class ItemsWriteDefaultsCliTests(unittest.TestCase):
    def test_empty_out_dir_defaults_when_allow_production(self) -> None:
        with patch("seedsmith.adapters.items.defaults.allow_production_tree",
                   return_value=True), \
             patch("seedsmith.adapters.items.defaults.resolve_out_dir_arg",
                   return_value=str(defaults_mod.PRODUCTION_OUT_DIRS["set"])) as resolve:
            args = cli_mod.build_parser().parse_args([
                "items", "generate", "--kind", "set", "--write"])
            cli_mod._apply_items_write_defaults(args)
            self.assertTrue(args.allow_production_tree)
            self.assertTrue(args.out_dir)
            resolve.assert_called()


class ItemsFillTests(unittest.TestCase):
    def _smoke_limits(self) -> fill_mod.FillLimits:
        return fill_mod.FillLimits(limit=1, count=1, batch_size=1, max_partitions=1)

    def test_dry_run_lists_kinds_in_dependency_order(self) -> None:
        report = fill_mod.run_fill(
            dry_run=True, allow_production=True, limits=self._smoke_limits())
        self.assertTrue(report.dry_run)
        self.assertFalse(report.refused_reason)
        kinds_seen = [s.kind for s in report.steps if s.status == "planned"]
        self.assertTrue(kinds_seen)
        order = {k: i for i, k in enumerate(defaults_mod.FILL_KIND_ORDER)}
        indexes = [order[k] for k in kinds_seen]
        self.assertEqual(indexes, sorted(indexes))

    def test_fill_cli_dry_run_requires_limit_or_full(self) -> None:
        args = cli_mod.build_parser().parse_args(["items", "fill", "--dry-run"])
        code = cli_mod.cmd_items(args)
        self.assertEqual(code, cli_mod.EXIT_REFUSED)

    def test_fill_cli_dry_run_with_limit(self) -> None:
        args = cli_mod.build_parser().parse_args([
            "items", "fill", "--dry-run", "--limit", "1", "--max-partitions", "1"])
        code = cli_mod.cmd_items(args)
        self.assertEqual(code, cli_mod.EXIT_CLEAN)

    def test_fill_dispatch_order_and_stop_on_refuse(self) -> None:
        calls: list[list[str]] = []

        def dispatch(argv: list[str]) -> int:
            calls.append(argv)
            if len(calls) == 2:
                return cli_mod.EXIT_REFUSED
            return cli_mod.EXIT_CLEAN

        report = fill_mod.run_fill(
            kinds=("material", "gem"), dry_run=False, allow_production=True,
            limits=self._smoke_limits(), dispatch=dispatch, stop_on_error=True)
        self.assertGreaterEqual(len(calls), 1)
        self.assertEqual(calls[0][:3], ["--kind", "material", "--write"])
        refused = [s for s in report.steps if s.status == "refused"]
        self.assertEqual(len(refused), 1)

    def test_fill_stops_on_gap(self) -> None:
        calls: list[list[str]] = []

        def dispatch(argv: list[str]) -> int:
            calls.append(argv)
            return cli_mod.EXIT_GAP

        report = fill_mod.run_fill(
            kinds=("material", "gem"), dry_run=False, allow_production=True,
            limits=self._smoke_limits(), dispatch=dispatch, stop_on_error=True)
        self.assertEqual(len(calls), 1)
        self.assertEqual(report.steps[0].status, "gap")
        self.assertEqual(report.worst_exit_code, cli_mod.EXIT_GAP)

    def test_affix_jobs_only_legal_kinds(self) -> None:
        jobs = fill_mod.discover_affix_family_jobs()
        legal = {"stat.modify", "stat.derived"}
        self.assertTrue(jobs)
        for _group, kid in jobs:
            self.assertIn(kid, legal)

    def test_fill_catches_unexpected_exception(self) -> None:
        def dispatch(argv: list[str]) -> int:
            raise RuntimeError("boom")

        report = fill_mod.run_fill(
            kinds=("material",), dry_run=False, allow_production=True,
            limits=self._smoke_limits(), dispatch=dispatch, stop_on_error=True)
        self.assertEqual(report.steps[0].status, "error")
        self.assertEqual(report.worst_exit_code, cli_mod.EXIT_CANNOT_RUN)

    def test_fill_kinds_filter(self) -> None:
        steps, reason = fill_mod.plan_fill_steps(
            kinds=("material",), allow_production=True, limits=self._smoke_limits())
        self.assertEqual(reason, "")
        self.assertEqual([s.kind for s in steps], ["material"])

    def test_unbounded_grids_refused_without_limit(self) -> None:
        steps, reason = fill_mod.plan_fill_steps(
            kinds=("set",), allow_production=True,
            limits=fill_mod.FillLimits(limit=0, full=False))
        self.assertEqual(steps, [])
        self.assertIn("--limit", reason)

    def test_combination_strain_before_splice(self) -> None:
        steps, reason = fill_mod.plan_fill_steps(
            kinds=("combination",), allow_production=True, limits=self._smoke_limits())
        self.assertEqual(reason, "")
        shapes = [s.note for s in steps if s.argv]
        self.assertEqual(shapes, ["shape=strain", "shape=splice"])
        self.assertIn("--limit", steps[0].argv)
        self.assertIn("1", steps[0].argv)

    def test_recipe_emits_count(self) -> None:
        steps, _ = fill_mod.plan_fill_steps(
            kinds=("recipe",), allow_production=True, limits=self._smoke_limits())
        argv = steps[0].argv
        self.assertIn("--count", argv)
        self.assertEqual(argv[argv.index("--count") + 1], "1")
        self.assertIn("--backfill", argv)

    def test_max_partitions_caps_gem(self) -> None:
        steps, _ = fill_mod.plan_fill_steps(
            kinds=("gem",), allow_production=True,
            limits=fill_mod.FillLimits(max_partitions=1, batch_size=1))
        planned = [s for s in steps if s.argv]
        self.assertEqual(len(planned), 1)
        self.assertIn("--batch-size", planned[0].argv)

    def test_smoke_dry_run_step_count_is_small(self) -> None:
        steps, reason = fill_mod.plan_fill_steps(
            allow_production=True, limits=self._smoke_limits())
        self.assertEqual(reason, "")
        planned = [s for s in steps if s.argv]
        # 1 per phase-2 kind + 1 base-type + set + charm + recipe + 2 combo + 1 drop ≈ ≤ 15
        self.assertLessEqual(len(planned), 15)
        self.assertGreaterEqual(len(planned), 8)

    def test_fill_kind_order_is_valid_topo_of_map_edges(self) -> None:
        order = {k: i for i, k in enumerate(defaults_mod.FILL_KIND_ORDER)}
        for upstream, downstream in fill_mod.MAP_DEPENDENCY_EDGES:
            self.assertLess(
                order[upstream], order[downstream],
                f"{upstream} must precede {downstream}")

    def test_allow_production_not_required_for_material_only(self) -> None:
        with patch("seedsmith.adapters.items.defaults.allow_production_tree",
                   return_value=False):
            args = cli_mod.build_parser().parse_args([
                "items", "fill", "--dry-run", "--kinds", "material",
                "--limit", "1"])
            # dry-run never needs allow; material-only live also shouldn't require it
            code = cli_mod.cmd_items(args)
            self.assertEqual(code, cli_mod.EXIT_CLEAN)


if __name__ == "__main__":
    unittest.main()
