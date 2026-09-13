"""debug_preflight: readiness audit (todo T7).

Every check reports PASS/FAIL + evidence + fix command. Root and env inject
so tests never depend on this machine. Read-only: two runs on an idle tree
are byte-identical (no timestamps in output).
"""
import json

from tools import debug_preflight as preflight


def _env(**overrides):
    env = {"PATH": "x"}
    env.update(overrides)
    return env


def test_empty_tree_fails_game_dir_naming_the_var(tmp_path):
    out = preflight.audit(root=str(tmp_path), env=_env())
    by_name = {c["check"]: c for c in out["checks"]}
    assert by_name["game-dir"]["verdict"] == "FAIL"
    assert "FUSIONRPG_GAME_DIR" in by_name["game-dir"]["fix"]
    # Dependents fail (never crash, never SKIP-silence) naming the same cause.
    assert by_name["interop-refs"]["verdict"] == "FAIL"
    assert by_name["dll-freshness"]["verdict"] == "FAIL"


def test_full_tree_passes(tmp_path):
    game = tmp_path / "game"
    (game / "BepInEx" / "core").mkdir(parents=True)
    (game / "BepInEx" / "interop").mkdir(parents=True)
    (game / "PlantsVsZombiesRH.exe").write_text("x")
    (tmp_path / "dist" / "FusionRpg.Server" / "data").mkdir(parents=True)
    (tmp_path / "dist" / "FusionRpg.Server" / "data" / "rpg-hot.sqlite").write_text("x")
    (tmp_path / "dist" / "FusionRpg.Server" / "FusionRpg.Core.dll").write_text("x")
    (tmp_path / "web" / "fusion-rpg-web" / "node_modules").mkdir(parents=True)
    env = _env(FUSIONRPG_GAME_DIR=str(game))
    out = preflight.audit(root=str(tmp_path), env=env)
    failing = [c["check"] for c in out["checks"]
               if c["verdict"] == "FAIL" and c["check"] != "server-port"]
    assert failing == [], failing
    assert all(set(c) >= {"check", "verdict", "evidence", "fix"}
               for c in out["checks"])


def test_audit_is_deterministic(tmp_path):
    first = preflight.audit(root=str(tmp_path), env=_env())
    second = preflight.audit(root=str(tmp_path), env=_env())
    assert json.dumps(first, sort_keys=True) == json.dumps(second, sort_keys=True)


def test_server_port_branches():
    up = preflight._port_state(probe=lambda: (False, True))
    assert up["verdict"] == "PASS"
    free = preflight._port_state(probe=lambda: (True, False))
    assert free["verdict"] == "PASS"
    stale = preflight._port_state(probe=lambda: (False, False))
    assert stale["verdict"] == "FAIL"
    assert "5088" in stale["fix"]


def test_server_registers_debug_preflight():
    import asyncio
    from server import mcp
    tools = asyncio.run(mcp.list_tools())
    assert "debug_preflight" in [t.name for t in tools]
