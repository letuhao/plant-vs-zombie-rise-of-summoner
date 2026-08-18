import { describe, expect, it, vi } from "vitest";
import {
  appendLogEvent,
  appendLogEvents,
  clearLogEvents,
  getLastHitEvent,
  getLawnMembershipRing,
  getLogEvents,
  subscribeLastHit,
  subscribeLog
} from "./log-store";
import type { EventEnvelope } from "./types";

function evt(kind: string, id?: number): EventEnvelope {
  return { id, t: "2026-01-01T00:00:00Z", game: "pvzrh-3.8.1", kind };
}

describe("log-store", () => {
  it("appends newest first and notifies subscribers", () => {
    const listener = vi.fn();
    const unsub = subscribeLog(listener);
    appendLogEvent(evt("board.start", 1));
    expect(getLogEvents()[0]?.kind).toBe("board.start");
    expect(listener).toHaveBeenCalledTimes(1);
    unsub();
    appendLogEvent(evt("board.end", 2));
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("prepends batches in reverse order", () => {
    appendLogEvents([evt("a", 1), evt("b", 2)]);
    expect(getLogEvents().map((e) => e.kind)).toEqual(["b", "a"]);
  });

  it("caps at 800 events", () => {
    for (let i = 0; i < 820; i++) appendLogEvent(evt(`k${i}`, i));
    expect(getLogEvents()).toHaveLength(800);
    expect(getLogEvents()[0]?.kind).toBe("k819");
  });

  it("clears events", () => {
    appendLogEvent(evt("x"));
    clearLogEvents();
    expect(getLogEvents()).toEqual([]);
  });

  it("ignores empty batch", () => {
    const listener = vi.fn();
    subscribeLog(listener);
    appendLogEvents([]);
    expect(listener).not.toHaveBeenCalled();
  });

  it("membership snapshot excludes hits and keeps the same array on hit-only append", () => {
    appendLogEvent(evt("plant.spawn", 1));
    const first = getLawnMembershipRing();
    expect(first.map((e) => e.kind)).toEqual(["plant.spawn"]);
    appendLogEvent({
      ...evt("combat.hit", 2),
      payload: { damage: 9, targetPtr: "P" }
    });
    expect(getLogEvents()[0]?.kind).toBe("combat.hit");
    const second = getLawnMembershipRing();
    expect(second).toBe(first);
    expect(second.map((e) => e.kind)).toEqual(["plant.spawn"]);
    expect(getLastHitEvent()?.kind).toBe("combat.hit");
  });

  it("lastHit slot notifies independently", () => {
    const hitFn = vi.fn();
    subscribeLastHit(hitFn);
    appendLogEvent(evt("plant.spawn", 1));
    expect(hitFn).not.toHaveBeenCalled();
    appendLogEvent({ ...evt("combat.hit", 2), payload: { damage: 1 } });
    expect(hitFn).toHaveBeenCalledTimes(1);
    expect(getLastHitEvent()?.kind).toBe("combat.hit");
  });
});
