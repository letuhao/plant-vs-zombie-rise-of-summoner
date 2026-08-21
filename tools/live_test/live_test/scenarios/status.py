"""Status apply/clear catalog (small subset)."""

from __future__ import annotations

from live_test.lawn import ensure_lawn
from live_test.report import Report
from live_test.scenarios import RunContext, scenario

# Small LIVE subset — expand later toward prove-status-full.ps1
STATUS_SUBSET = ("stun", "freeze", "slow")


def _any_ptr(ctx: RunContext) -> str | None:
    tip = ctx.client.max_event_id()
    ctx.client.post_debug("/board-stats", {})
    ev = ctx.client.wait_kind(tip, "debug.board-stats", timeout_sec=10)
    p = ctx.client.payload(ev) or {}
    for key in ("zombies", "plants", "livingZombies", "livingPlants"):
        arr = p.get(key)
        if isinstance(arr, list) and arr and isinstance(arr[0], dict):
            return str(arr[0].get("ptr") or arr[0].get("targetPtr") or "") or None
    return ctx.target_ptr


@scenario("status.apply")
def status_apply(ctx: RunContext, report: Report) -> None:
    ensure_lawn(ctx.client, enter_level=ctx.enter_level)
    tip = ctx.client.max_event_id()
    ctx.client.post_debug("/scenario/lab-overlay", {})
    ctx.client.wait_kind(tip, "debug.run-steps.done", timeout_sec=60)
    ptr = _any_ptr(ctx)
    tip = ctx.client.max_event_id()
    body = {"statusId": "stun", "duration": 2.0}
    if ptr:
        body["targetPtr"] = ptr
    ctx.client.post_debug("/apply-status", body)
    ev = ctx.client.wait_kind(tip, "debug.apply-status", timeout_sec=10)
    if ev is None:
        ev = ctx.client.wait_kind(tip, "debug.status.apply", timeout_sec=5)
    report.require(ev is not None, "apply-status event")


@scenario("status.clear")
def status_clear(ctx: RunContext, report: Report) -> None:
    ensure_lawn(ctx.client, enter_level=ctx.enter_level)
    tip = ctx.client.max_event_id()
    ctx.client.post_debug("/clear-status", {})
    ev = ctx.client.wait_kind(tip, "debug.clear-status", timeout_sec=10)
    report.require(ev is not None, "clear-status event")


@scenario("status.catalog")
def status_catalog(ctx: RunContext, report: Report) -> None:
    ensure_lawn(ctx.client, enter_level=ctx.enter_level)
    tip = ctx.client.max_event_id()
    ctx.client.post_debug("/scenario/lab-overlay", {})
    ctx.client.wait_kind(tip, "debug.run-steps.done", timeout_sec=60)
    ptr = _any_ptr(ctx)
    for sid in STATUS_SUBSET:
        tip = ctx.client.max_event_id()
        body: dict = {"statusId": sid, "duration": 1.0}
        if ptr:
            body["targetPtr"] = ptr
        ctx.client.post_debug("/apply-status", body)
        ev = ctx.client.wait_kind(tip, "debug.apply-status", timeout_sec=8)
        report.check(ev is not None, f"apply {sid}")
        tip = ctx.client.max_event_id()
        ctx.client.post_debug("/clear-status", {"statusId": sid, "targetPtr": ptr} if ptr else {"statusId": sid})
        ctx.client.wait_kind(tip, "debug.clear-status", timeout_sec=8)
