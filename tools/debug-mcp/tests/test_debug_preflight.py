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


def test_live_state_separates_injector_connection_from_idle_board():
    snapshots = []

    def request(method, path, params=None):
        if path == "/health":
            return {"status": 200, "body": {"ok": True, "injectorConnected": True,
                                              "lastHeartbeatUtc": "2026-09-14T00:00:00+00:00",
                                              "source": "injector", "simEnabled": False}}
        if path == "/api/debug/session":
            return {"status": 200, "body": {"sessionActive": False}}
        if path == "/api/debug/snapshot":
            snapshots.append(True)
            return {"status": 200, "body": {"ok": True}}
        if not snapshots and params["afterId"] < 9:
            return {"status": 200, "body": {"items": [{"id": 9}]}}
        if snapshots and params["afterId"] == 9:
            return {"status": 200, "body": {"items": [{"id": 10, "kind": "debug.snapshot",
                "matchKey": None, "payload": {"match": {"phase": "Idle"}}}]}}
        return {"status": 200, "body": {"items": []}}

    out = preflight.live_state(request=request, process_probe=lambda: True)
    assert snapshots == [True]
    assert out["injector"]["connected"] is True
    assert out["board"]["observed"] is True
    assert out["board"]["state"] == "idle"
    assert out["ready"] is False
    assert "injector liveness alone" in out["fix"]


def test_live_state_requires_a_fresh_snapshot_for_ready():
    def request(method, path, params=None):
        if path == "/health":
            return {"status": 200, "body": {"ok": True, "injectorConnected": True}}
        if path in ("/api/debug/session", "/api/debug/snapshot"):
            return {"status": 200, "body": {"ok": True}}
        return {"status": 200, "body": {"items": []}}

    now = iter((0, 6))
    out = preflight.live_state(request=request, process_probe=lambda: True,
                               monotonic=lambda: next(now), sleep=lambda _: None)
    assert out["injector"]["connected"] is True
    assert out["board"]["observed"] is False
    assert out["ready"] is False
    assert "did not emit debug.snapshot" in out["fix"]


def test_board_state_only_calls_non_idle_observation_live():
    assert preflight._board_state({"payload": {"match": {"phase": "Idle"}}})[0] == "idle"
    assert preflight._board_state({"payload": {"match": {"phase": "Loading"}}})[0] == "loading"
    assert preflight._board_state({"payload": {"match": {"phase": "InMatch"}}})[0] == "active"
