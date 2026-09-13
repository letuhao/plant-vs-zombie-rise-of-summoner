"""debug_preflight: setup/readiness audit (todo T7).

Read-only: inspects the tree, env, and :5088; changes nothing (no timestamps
in output, so two idle runs are identical). Every check reports PASS/FAIL +
evidence + the fix command. Root and env inject for hermetic tests.
"""
import csv
import datetime as dt
import io
import os
import socket
import subprocess
import time
from pathlib import Path

import httpx
import registry

_PORT = 5088
_BASE_URL = "http://127.0.0.1:5088"
_GAME_VARS = ("FUSIONRPG_ML_GAMEDIR", "FUSIONRPG_GAME_DIR")
_SNAPSHOT_TIMEOUT_SECONDS = 5  # Readiness observation must never become a long-running probe.


def _fail(check, evidence, fix):
    return {"check": check, "verdict": "FAIL", "evidence": evidence, "fix": fix}


def _pass(check, evidence):
    return {"check": check, "verdict": "PASS", "evidence": evidence,
            "fix": ""}


def _game_dir(root, env):
    for var in _GAME_VARS:
        candidate = env.get(var)
        if candidate and Path(candidate).is_dir():
            exe = Path(candidate) / "PlantsVsZombiesRH.exe"
            if exe.is_file():
                return str(candidate), _pass("game-dir", f"{var}={candidate}")
            return None, _fail("game-dir", f"{var}={candidate} (no game exe)",
                               f"point {var} at the MelonLoader/BepInEx pack")
    return None, _fail(
        "game-dir", "no game dir (%s)" % ", ".join(_GAME_VARS),
        "$env:FUSIONRPG_GAME_DIR = \"<game folder>\" (MelonLoader pack default)")


def _interop_refs(root, env, game_dir):
    if game_dir is None:
        return _fail("interop-refs", "no game dir (see game-dir check)",
                     "fix game-dir first")
    game = Path(game_dir)
    for sub in ("BepInEx/core", "BepInEx/interop"):
        if not (game / sub).is_dir():
            artifacts = Path(root) / "artifacts" / "bepinex-refs"
            if artifacts.is_dir():
                return _pass("interop-refs", f"fallback {artifacts}")
            return _fail("interop-refs", f"missing {game / sub}",
                         ".\\scripts\\prepare-injector-refs.ps1")
    return _pass("interop-refs", f"{game}/BepInEx/{{core,interop}}")


def _port_state(probe=None):
    """(port_free, server_healthy). Probe injectable; default hits :5088."""
    if probe is None:
        def probe():
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(0.5)
            free = sock.connect_ex(("127.0.0.1", _PORT)) != 0
            sock.close()
            healthy = False
            if not free:
                try:
                    import httpx
                    healthy = httpx.get(f"http://127.0.0.1:{_PORT}/health",
                                        timeout=2).status_code == 200
                except Exception:
                    healthy = False
            return free, healthy
    free, healthy = probe()
    if healthy:
        return _pass("server-port", f":{ _PORT} healthy (server already up)")
    if free:
        return _pass("server-port", f":{_PORT} free (deploy may start a server)")
    return _fail("server-port", f":{_PORT} occupied by an unhealthy listener",
                 f"stop it or free :{_PORT}, then redeploy")


def _newest(root, *globs):
    newest = None
    for pattern in globs:
        for path in Path(root).glob(pattern):
            if path.is_file():
                mtime = path.stat().st_mtime
                if newest is None or mtime > newest[1]:
                    newest = (str(path), mtime)
    return newest


def _dll_freshness(root, env, game_dir):
    if game_dir is None:
        return _fail("dll-freshness", "no game dir (see game-dir check)",
                     "fix game-dir first")
    src = _newest(root, "src/FusionRpg.Core/**/*.cs",
                  "src/FusionRpg.Contracts/**/*.cs")
    if src is None:
        return _pass("dll-freshness", "no src files under root; nothing to compare")
    dll = Path(root) / "dist" / "FusionRpg.Server" / "FusionRpg.Core.dll"
    if not dll.is_file():
        return _fail("dll-freshness", "no published server tree",
                     "dotnet publish src/FusionRpg.Server -c Release -o dist/FusionRpg.Server")
    if dll.stat().st_mtime < src[1]:
        return _fail("dll-freshness", f"{dll.name} older than {src[0]}",
                     "republish; close the game first if it holds the DLL lock")
    return _pass("dll-freshness", f"{dll.name} newer than newest src")


def _data_dir(root, env):
    data = Path(env.get("FUSIONRPG_DATA", "")) if env.get("FUSIONRPG_DATA") else \
        Path(root) / "dist" / "FusionRpg.Server" / "data"
    hot = data / "rpg-hot.sqlite"
    if hot.is_file():
        return _pass("data-dir", str(hot))
    return _fail("data-dir", f"missing {hot}",
                 "publish the server, then run the AtomImporter import step")


def _node_modules(root, env):
    node = Path(root) / "web" / "fusion-rpg-web" / "node_modules"
    if node.is_dir():
        return _pass("node-modules", str(node))
    return _fail("node-modules", "no node_modules (FE build would fail)",
                 "cd web/fusion-rpg-web; npm ci")


def _mcp_deps(root, env):
    try:
        import fastmcp
        version = getattr(fastmcp, "__version__", "present")
    except ImportError:
        return _fail("mcp-deps", "fastmcp not importable",
                     "python -m pip install -r tools/debug-mcp/requirements.lock")
    lock = Path(root) / "tools" / "debug-mcp" / "requirements.lock"
    pinned = None
    if lock.is_file():
        for line in lock.read_text().splitlines():
            if line.startswith("fastmcp=="):
                pinned = line.split("==", 1)[1].strip()
    if pinned is not None and version != pinned and version != "present":
        return _fail("mcp-deps", f"fastmcp {version} != locked {pinned}",
                     "python -m pip install -r tools/debug-mcp/requirements.lock")
    return _pass("mcp-deps", f"fastmcp {version}")


def _runtime_request(method, path, params=None):
    """Read one existing local route; callers may inject this in tests."""
    response = httpx.request(method, _BASE_URL + path, params=params, timeout=2)
    try:
        body = response.json()
    except ValueError:
        body = {"raw": response.text[:4000]}
    return {"status": response.status_code, "body": body}


def _game_process_running():
    """Local process fact only; it does not claim that the injector loaded."""
    try:
        completed = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq PlantsVsZombiesRH.exe", "/FO", "CSV", "/NH"],
            capture_output=True, text=True, timeout=2, check=False)
        rows = list(csv.reader(io.StringIO(completed.stdout)))
        return any(row and row[0].lower() == "plantsvszombiesrh.exe" for row in rows)
    except (OSError, subprocess.TimeoutExpired):
        return None


def _event_max_id(request):
    """Find the event watermark through the existing cursor route, without SQL."""
    first = request("GET", "/api/debug/events", {"afterId": 0, "limit": 1})
    if first["status"] >= 400 or not first["body"].get("items"):
        return 0

    def has_after(event_id):
        result = request("GET", "/api/debug/events", {"afterId": event_id, "limit": 1})
        return result["status"] < 400 and bool(result["body"].get("items"))

    low, high = 0, 1
    for _ in range(63):
        if not has_after(high):
            break
        low, high = high, high * 2
    else:
        return None
    while high - low > 1:
        middle = low + (high - low) // 2
        if has_after(middle):
            low = middle
        else:
            high = middle
    return high


def _heartbeat_age_ms(value, now=None):
    if not isinstance(value, str):
        return None
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        parsed = parsed if parsed.tzinfo else parsed.replace(tzinfo=dt.timezone.utc)
        current = now or dt.datetime.now(dt.timezone.utc)
        return max(0, round((current - parsed.astimezone(dt.timezone.utc)).total_seconds() * 1000))
    except ValueError:
        return None


def _board_state(snapshot):
    payload = snapshot.get("payload", {}) if isinstance(snapshot, dict) else {}
    match = payload.get("match", {}) if isinstance(payload, dict) else {}
    phase = match.get("phase", payload.get("matchPhase")) if isinstance(match, dict) else None
    normalized = str(phase or "unknown").lower()
    if normalized in ("idle", "", "unknown"):
        return "idle", phase
    if "load" in normalized or "enter" in normalized:
        return "loading", phase
    return "active", phase


def live_state(request=None, process_probe=None, sleep=None, monotonic=None):
    """Compose health + session + fresh snapshot into an honest live verdict.

    Health proves server/injector liveness. A snapshot emitted after a saved
    watermark proves a current injector observation. Neither fact is silently
    upgraded into the other.
    """
    request = request or _runtime_request
    process_probe = process_probe or _game_process_running
    sleep = sleep or time.sleep
    monotonic = monotonic or time.monotonic
    game_running = process_probe()
    try:
        health_result = request("GET", "/health")
    except (httpx.HTTPError, OSError) as exc:
        return {
            "ready": False,
            "server": {"reachable": False, "error": str(exc)},
            "gameProcess": {"running": game_running},
            "injector": {"connected": False},
            "board": {"observed": False, "state": "unknown"},
            "session": None,
            "scope": ["local-machine", "rpg-server-debug", "game-injector-debug"],
            "fix": "Start-Process dist\\FusionRpg.Server\\FusionRpg.Server.exe",
        }

    health = health_result.get("body", {})
    healthy = health_result.get("status", 500) < 400 and bool(health.get("ok"))
    connected = healthy and bool(health.get("injectorConnected"))
    result = {
        "ready": False,
        "server": {"reachable": health_result.get("status", 500) < 400, "healthy": healthy},
        "gameProcess": {"running": game_running},
        "injector": {"connected": connected, "heartbeatAgeMs": _heartbeat_age_ms(health.get("lastHeartbeatUtc")),
                     "source": health.get("source"), "simEnabled": health.get("simEnabled")},
        "board": {"observed": False, "state": "unknown", "phase": None},
        "session": None,
        "scope": ["local-machine", "rpg-server-debug", "game-injector-debug"],
        "fix": None,
    }
    if not healthy:
        result["fix"] = "Inspect http://127.0.0.1:5088/health, then start the published server if it is down"
        return result
    if not connected:
        result["fix"] = ".\\scripts\\deploy-play.ps1 -NoServer -NoRebuildUi"
        return result

    session = request("GET", "/api/debug/session")
    result["session"] = session.get("body") if session.get("status", 500) < 400 else None
    before = _event_max_id(request)
    if before is None:
        result["fix"] = "Event watermark could not be bounded; inspect /api/debug/events before a live probe"
        return result
    snapshot_request = request("GET", "/api/debug/snapshot")
    if snapshot_request.get("status", 500) >= 400:
        result["fix"] = "Snapshot command was refused; verify injector connection and debug route availability"
        return result

    deadline = monotonic() + _SNAPSHOT_TIMEOUT_SECONDS
    snapshot = None
    while monotonic() < deadline:
        events = request("GET", "/api/debug/events", {"afterId": before, "limit": 500})
        for event in events.get("body", {}).get("items", []):
            if event.get("kind") == "debug.snapshot":
                snapshot = event
        if snapshot is not None:
            break
        sleep(0.25)
    if snapshot is None:
        result["fix"] = "Injector is connected but did not emit debug.snapshot within 5s; inspect the game loading state or injector logs"
        return result

    state, phase = _board_state(snapshot)
    result["board"] = {"observed": True, "state": state, "phase": phase,
                       "eventId": snapshot.get("id"), "matchKey": snapshot.get("matchKey")}
    result["ready"] = state == "active"
    if state == "idle":
        result["fix"] = "Open a lawn or use debug_lawn_setup only when you own the board; injector liveness alone is not a live probe"
    elif state == "loading":
        result["fix"] = "Wait for the game to finish loading, then run debug_preflight again"
    return result


def audit(root=None, env=None, include_live=False, request=None, process_probe=None):
    """Run every check. Pure read: safe to run twice, output identical."""
    env = dict(os.environ) if env is None else dict(env)
    if root is None:
        try:
            root = str(registry.find_repo_root())
        except FileNotFoundError:
            root = os.getcwd()
    game_dir, game_check = _game_dir(root, env)
    checks = [
        game_check,
        _interop_refs(root, env, game_dir),
        _port_state(),
        _dll_freshness(root, env, game_dir),
        _data_dir(root, env),
        _node_modules(root, env),
        _mcp_deps(root, env),
    ]
    result = {"checks": checks, "scope": "local-machine"}
    if include_live:
        result["live"] = live_state(request=request, process_probe=process_probe)
    return result
