"""debug_restart_game: close and relaunch the game process, poll for a fresh injector connection.

Adapter over scripts/restart-game.ps1 -- deliberately NOT reimplemented in Python. The script is
the single source of truth for this recovery path (real ground-truth polling: /health +
injectorConnected + a heartbeat newer than the pre-restart snapshot, output-flushed so progress is
visible even when piped -- both fixed live 2026-09-14 after real incidents). Duplicating that logic
here would fork it the same way a second debug surface would (AGENTS.md: adapter-wrap existing
services, never re-implement them).

DISRUPTIVE: this closes the running PlantsVsZombiesRH.exe process and starts a new one. It is the
easiest, most reliable recovery for a stuck/defeated board (a fresh process cannot carry over a
stale Board/InitBoard reference the way in-place `debug_ui_nav` navigation might -- see
lawn-run-state-machine.md), but it does end whatever the operator was looking at. Prefer this over
chasing `debug_ui_nav` in place when a provably clean board matters more than speed.
"""
import subprocess
from pathlib import Path

import registry


def _default_runner(args, timeout):
    completed = subprocess.run(args, capture_output=True, text=True, timeout=timeout, check=False)
    return completed.returncode, completed.stdout, completed.stderr


def restart(timeout_sec=120, root=None, runner=None):
    """Run scripts/restart-game.ps1. Returns an envelope; only caller misuse raises.

    `timeout_sec` is passed through to the script's own bounded poll; the subprocess itself is
    given `timeout_sec + 30` headroom for process-spawn overhead before this call gives up on it.
    """
    if not isinstance(timeout_sec, int) or timeout_sec < 1:
        raise ValueError("timeout_sec must be a positive int")
    root = Path(root) if root else registry.find_repo_root()
    script = root / "scripts" / "restart-game.ps1"
    if not script.is_file():
        return {"ok": False, "error": f"missing {script}",
                "fix": "this script should already exist -- check the worktree/branch",
                "scope": "local-machine"}

    args = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
            "-TimeoutSec", str(timeout_sec)]
    run = runner or _default_runner
    subprocess_timeout = timeout_sec + 30
    try:
        code, out, err = run(args, subprocess_timeout)
    except subprocess.TimeoutExpired:
        return {"ok": False,
                "error": f"restart-game.ps1 did not finish within {subprocess_timeout}s",
                "fix": "check the game/server manually -- the script or game may still be starting",
                "scope": "local-machine"}
    return {
        "ok": code == 0,
        "exitCode": code,
        "stdout": out[-4000:] if out else "",
        "stderr": err[-2000:] if err else "",
        "scope": "local-machine",
    }
