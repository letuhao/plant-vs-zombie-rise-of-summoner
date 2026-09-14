"""debug_screenshot: trigger a live Unity frame capture and return the PNG.

Playwright-shaped (browser_screenshot analogue): one call captures the currently
rendered game frame and hands the image back to the model as base64, plus an
optional saved path. Adapter over POST /api/debug/screenshot (trigger) +
GET /api/debug/screenshot/info + GET /api/debug/screenshot/latest
(DebugEndpoints.cs) — composed from debug_call/debug_events, never reimplemented.
Only caller misuse raises; every environment failure returns a not-ready envelope
with a fix. Scope is game-injector-debug end to end: the image proves live-engine
state, never server correctness.
"""
import base64
import time
from pathlib import Path

import httpx

from tools.debug_call import call as route_call

BASE_URL = "http://127.0.0.1:5088"
_PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
_POLL_EVERY_SEC = 2
_FETCH_TIMEOUT = 30

_FIXES = (
    ("injector not connected",
     "launch the game with the FusionRpg injector loaded, then retry"),
    ("route not in debug allowlist",
     "the MCP process is reading a DebugEndpoints.cs without the screenshot routes "
     "-- restart it from a tree that has them (post-merge main)"),
)


def _not_ready(error, fix):
    return {"ok": False, "error": error, "fix": fix,
            "tag": None, "width": None, "height": None, "bytes": None,
            "takenAtUtc": None, "primitive": None, "pngBase64": None,
            "path": None, "scope": "game-injector-debug"}


def _default_fetch(url, timeout):
    response = httpx.get(url, timeout=timeout)
    response.raise_for_status()
    return response.content


def _match_fix(error):
    for marker, fix in _FIXES:
        if marker in error:
            return fix
    return error


def screenshot(tag="probe", timeout=30, save_to=None, transport=None,
               fetch=None, sleep=None):
    """Capture the live game frame. Returns image envelope (never raises on env).

    `tag` labels the capture (sanitized server-side too). `timeout` bounds the
    ready-poll. `save_to` optionally writes the PNG to disk as well.
    """
    if not isinstance(tag, str) or not tag.strip():
        raise ValueError("tag must be a non-empty string")
    if not isinstance(timeout, int) or timeout < 1:
        raise ValueError("timeout must be a positive int")
    do_sleep = sleep or time.sleep
    get_bytes = fetch or _default_fetch

    try:
        trigger = route_call("POST", "/screenshot", body={"tag": tag},
                             transport=transport, timeout=timeout + 30)
    except ValueError as ex:
        # Off-allowlist: the MCP process is reading a DebugEndpoints.cs generation
        # without the screenshot routes (registry.load_allowlist is mtime-cached per
        # tree) — an environment fact, not caller misuse.
        return _not_ready(str(ex), _match_fix(str(ex)))
    except (httpx.ConnectError, ConnectionError, TimeoutError,
            httpx.TimeoutException) as ex:
        return _not_ready(f"server unreachable: {ex}",
                          "Start-Process dist\\FusionRpg.Server\\FusionRpg.Server.exe")
    if not trigger["ok"]:
        body = trigger["body"] if isinstance(trigger["body"], dict) else {}
        error = body.get("error", "screenshot trigger refused")
        return _not_ready(error, _match_fix(error))

    deadline = time.monotonic() + timeout
    ready = None
    while time.monotonic() < deadline:
        try:
            # Newest-first tail: /events reads forward from afterId, so its kinds
            # filter only sees the oldest window on a long-lived server.
            page = route_call("GET", "/events/tail",
                              params={"kinds": "debug.screenshot.ready",
                                      "limit": 20},
                              transport=transport, timeout=_FETCH_TIMEOUT)
        except (httpx.ConnectError, ConnectionError, TimeoutError,
                httpx.TimeoutException):
            break
        body = page.get("body", {}) if isinstance(page, dict) else {}
        items = body.get("items", []) if isinstance(body, dict) else []
        for envelope in items:
            payload = envelope.get("payload", {}) \
                if isinstance(envelope, dict) else {}
            if payload.get("tag") == tag and payload.get("validPng"):
                ready = payload
                break
        if ready is not None:
            break
        do_sleep(_POLL_EVERY_SEC)
    if ready is None:
        return _not_ready(
            f"no debug.screenshot.ready for tag={tag} within {timeout}s",
            "the game may be closed, mid-load, or on a dev branch without the "
            "screenshot Drain case -- check BepInEx/LogOutput.txt or "
            "MelonLoader/Latest.log for a [screenshot] line")

    try:
        info = route_call("GET", "/screenshot/info", transport=transport,
                          timeout=_FETCH_TIMEOUT)
    except (httpx.ConnectError, ConnectionError, TimeoutError,
            httpx.TimeoutException) as ex:
        return _not_ready(f"screenshot info unreachable: {ex}",
                          "the server restarted mid-capture -- retry")
    info_body = info["body"] if info["ok"] and isinstance(info["body"], dict) \
        else {}
    taken_at = info_body.get("takenAtUtc", "")

    try:
        png = get_bytes(
            f"{BASE_URL}/api/debug/screenshot/latest?ts={taken_at}",
            _FETCH_TIMEOUT)
    except (httpx.ConnectError, ConnectionError, TimeoutError,
            httpx.TimeoutException, httpx.HTTPStatusError) as ex:
        return _not_ready(f"screenshot fetch failed: {ex}",
                          "the stored file may have been pruned -- retry the capture")
    if not png[:8] == _PNG_SIGNATURE:
        return _not_ready("stored bytes are not a PNG",
                          "the side store may be corrupt -- retry the capture")

    saved = None
    if save_to is not None:
        try:
            target = Path(save_to)
            target.write_bytes(png)
            saved = str(target)
        except OSError as ex:
            return _not_ready(f"could not write {save_to}: {ex}",
                              "pick a writable path")
    return {
        "ok": True,
        "tag": tag,
        "width": ready.get("width"),
        "height": ready.get("height"),
        "bytes": len(png),
        "takenAtUtc": taken_at,
        "primitive": ready.get("primitive"),
        "pngBase64": base64.b64encode(png).decode("ascii"),
        "path": saved,
        "scope": "game-injector-debug",
    }
