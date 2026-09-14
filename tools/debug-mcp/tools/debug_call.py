"""debug_call: invoke one allowlisted debug route, scope-stamped.

The universal adapter — plain route reads ride here instead of becoming
per-endpoint tools. Non-allowlisted routes are refused before any transport
runs. Scope comes from registry derivation (guard-debug-scope semantics).
"""
import re

import httpx

import registry

BASE_URL = "http://127.0.0.1:5088"
_API_PREFIX = "/api/debug"
_TIMEOUT = 60


def _match_template(route, template):
    """Match a concrete route against a {param}-templated allowlist entry."""
    if "{" not in template:
        return route == template
    pattern = "/".join("[^/]+" if seg.startswith("{") and seg.endswith("}")
                       else re.escape(seg) for seg in template.split("/"))
    return re.match(f"{pattern}$", route) is not None


def _lookup(routes, route):
    if route in routes:
        return route, routes[route]
    for template, scope in routes.items():
        if "{" in template and _match_template(route, template):
            return template, scope
    return None, None


def _default_transport(method, url, body, params, timeout):
    if method == "GET":
        response = httpx.get(url, params=params, timeout=30)
    else:
        response = httpx.post(url, json=body or {}, timeout=timeout)
    try:
        payload = response.json()
    except ValueError:
        payload = {"raw": response.text[:4000]}
    return {"status": response.status_code, "body": payload}


def call(method, route, body=None, params=None, transport=None, timeout=None):
    """Call one allowlisted route. Raises ValueError off-allowlist.

    Relative routes resolve under /api/debug (backward compatible); full
    /api/* paths match the allowlist directly (normal query paths). timeout
    applies to the default transport only (stub transports keep the 4-arg
    shape); default 60s.
    """
    raw = (route or "").strip()
    if not raw.startswith("/"):
        raw = "/" + raw
    # Relative candidate first (backward compatible output for debug routes),
    # then the full path (normal query paths outside /api/debug).
    rel = raw[len(_API_PREFIX):] if raw.startswith(_API_PREFIX + "/") else raw
    full = raw if raw.startswith("/api/") else _API_PREFIX + raw
    candidates = [rel] if rel == full else [rel, full]
    routes = registry.load_allowlist()
    matched, scope = None, None
    for candidate in candidates:
        matched, scope = _lookup(routes, candidate)
        if matched is not None:
            break
    if matched is None:
        raise ValueError(f"route not in debug allowlist: {route}")
    send = transport
    if send is None:
        limit = timeout if timeout and timeout > 0 else _TIMEOUT
        send = lambda m, u, b, p: _default_transport(m, u, b, p, limit)
    result = send(method.upper(), BASE_URL + full, body, params)
    return {
        "ok": result["status"] < 400,
        "status": result["status"],
        "body": result["body"],
        "scope": scope,
        "route": matched,
    }
