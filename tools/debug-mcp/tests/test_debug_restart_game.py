"""debug_restart_game: adapter over scripts/restart-game.ps1. Never reimplements the script's own
recovery/polling logic -- only invokes it and reports what happened.
"""
import subprocess

import pytest

from tools import debug_restart_game as restart_game


def _write_script(tmp_path):
    scripts = tmp_path / "scripts"
    scripts.mkdir()
    (scripts / "restart-game.ps1").write_text("# stub\n")
    return tmp_path


def test_success_reports_exit_zero(tmp_path):
    root = _write_script(tmp_path)

    def runner(args, timeout):
        assert "-TimeoutSec" in args
        assert "restart-game.ps1" in args[args.index("-File") + 1]
        return 0, "==> Game relaunched and injector reconnected after 3 check(s)\n", ""

    out = restart_game.restart(timeout_sec=90, root=root, runner=runner)
    assert out["ok"] is True
    assert out["exitCode"] == 0
    assert "relaunched" in out["stdout"]
    assert out["scope"] == "local-machine"


def test_nonzero_exit_reports_failure(tmp_path):
    root = _write_script(tmp_path)

    def runner(args, timeout):
        return 1, "", "==> TIMEOUT after 120s\n"

    out = restart_game.restart(root=root, runner=runner)
    assert out["ok"] is False
    assert out["exitCode"] == 1
    assert "TIMEOUT" in out["stderr"]


def test_missing_script_refuses_without_invoking_runner(tmp_path):
    called = []

    def runner(args, timeout):
        called.append(1)
        return 0, "", ""

    out = restart_game.restart(root=tmp_path, runner=runner)
    assert out["ok"] is False
    assert "missing" in out["error"]
    assert called == []


def test_subprocess_timeout_reports_clearly(tmp_path):
    root = _write_script(tmp_path)

    def runner(args, timeout):
        raise subprocess.TimeoutExpired(cmd=args, timeout=timeout)

    out = restart_game.restart(timeout_sec=10, root=root, runner=runner)
    assert out["ok"] is False
    assert "40s" in out["error"]  # timeout_sec (10) + 30s subprocess headroom


def test_invalid_timeout_raises():
    with pytest.raises(ValueError, match="timeout_sec"):
        restart_game.restart(timeout_sec=0)
    with pytest.raises(ValueError, match="timeout_sec"):
        restart_game.restart(timeout_sec="soon")
