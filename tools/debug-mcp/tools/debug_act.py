"""debug_act: run one lawn verb (place/shovel/hammer) with a named receipt.

Adapter over POST /api/debug/act. Verbs compose click primitives injector-side; the
receipt names the expected telemetry kind and the broken link on failure. Telemetry
read-back stays a follow-up poll (debug_events), never claimed from the response.
Only caller misuse raises.
"""
from tools import _control

_VERBS = ("place", "shovel", "hammer")


def act(verb, timeout=60, transport=None, sleep=None, **params):
    """Run one lawn verb. Returns the receipt envelope."""
    if verb not in _VERBS:
        raise ValueError(f"unknown verb: {verb} (expected one of {', '.join(_VERBS)})")
    tag = _control.fresh_tag("act")
    body = {"verb": verb, "tag": tag}
    for key in ("typeId", "col", "row"):
        if params.get(key) is not None:
            body[key] = params[key]
    ok, reply, fix = _control.trigger("/act", body, transport=transport,
                                      timeout=timeout)
    if not ok:
        return {"ok": False, "error": fix, "fix": fix,
                "verb": verb, "scope": _control.SCOPE}
    after_id = reply.get("afterId", 0) if isinstance(reply, dict) else 0
    ready = _control.poll("debug.act.done", tag, after_id,
                          transport=transport, timeout=timeout, sleep=sleep)
    if ready is None:
        return {"ok": False,
                "error": f"no debug.act.done for verb={verb} within timeout",
                "fix": "resolve refusals name their link in the injector log; "
                       "check BepInEx/LogOutput.txt or MelonLoader/Latest.log",
                "verb": verb, "scope": _control.SCOPE}
    return {
        "ok": True,
        "scope": _control.SCOPE,
        "verb": verb,
        "receipt": {k: v for k, v in ready.items() if k != "snapshot"},
        "snapshot": ready.get("snapshot"),
    }
