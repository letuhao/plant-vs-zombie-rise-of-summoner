"""debug_preflight: setup/readiness audit (todo T7).

Read-only: inspects the tree, env, and :5088; changes nothing (no timestamps
in output, so two idle runs are identical). Every check reports PASS/FAIL +
evidence + the fix command. Root and env inject for hermetic tests.
"""
import os
import socket
from pathlib import Path

import registry

_PORT = 5088
_GAME_VARS = ("FUSIONRPG_ML_GAMEDIR", "FUSIONRPG_GAME_DIR")


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


def audit(root=None, env=None):
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
    return {"checks": checks, "scope": "local-machine"}
