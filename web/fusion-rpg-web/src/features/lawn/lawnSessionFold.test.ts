import { describe, expect, it } from "vitest";
import { applyLawnSession, membershipFingerprint } from "./lawnSessionFold";
import { emptyLawnViewModel, findOccupant } from "./lawnViewModel";
import type { EventEnvelope } from "@/lib/bus/types";

function evt(kind: string, payload?: unknown, id?: number): EventEnvelope {
  return { id, t: "2026-01-01T00:00:00Z", game: "test", kind, payload };
}

describe("applyLawnSession", () => {
  it("keeps mowers when mower.place ages out of a hit-only window", () => {
    const start = applyLawnSession(emptyLawnViewModel(), [
      evt("mower.place", { ptr: "M1", type: 0, row: 2 }, 1),
      evt("plant.spawn", { ptr: "P", type: 1, row: 0, col: 0 }, 2)
    ], 0);
    expect(start.model.mowers.size).toBe(1);
    expect(start.lastEventId).toBe(2);

    const hits = applyLawnSession(start.model, [
      evt("combat.hit", { damage: 20, targetPtr: "P" }, 5),
      evt("combat.hit", { damage: 21, targetPtr: "P" }, 4),
      evt("combat.hit", { damage: 19, targetPtr: "P" }, 3)
    ], start.lastEventId);
    expect(hits.model).toBe(start.model);
    expect(hits.model.mowers.size).toBe(1);
    expect(hits.model.lastHit).toBeUndefined();
    expect(hits.model.revision).toBe(start.model.revision);
    expect(hits.lastEventId).toBe(5);
  });

  it("does not drop revision across hit-only window", () => {
    const a = applyLawnSession(emptyLawnViewModel(), [
      evt("zombie.spawn", { ptr: "Z", type: 0, row: 1, col: 6 }, 1)
    ], 0);
    const b = applyLawnSession(a.model, [
      evt("combat.hit", { damage: 1 }, 2)
    ], a.lastEventId);
    expect(b.model).toBe(a.model);
    expect(b.model.revision).toBe(a.model.revision);
    expect(findOccupant(b.model, "Z")).toBeDefined();
  });

  it("double-apply of the same ring does not bump revision twice", () => {
    const ring = [evt("plant.spawn", { ptr: "P", type: 1, row: 0, col: 0 }, 1)];
    const a = applyLawnSession(emptyLawnViewModel(), ring, 0);
    const b = applyLawnSession(a.model, ring, a.lastEventId);
    expect(b.model).toBe(a.model);
    expect(b.model.revision).toBe(a.model.revision);
    expect(b.lastEventId).toBe(a.lastEventId);
  });

  it("resets from empty when maxId falls below watermark", () => {
    const a = applyLawnSession(emptyLawnViewModel(), [
      evt("plant.spawn", { ptr: "P", type: 1, row: 0, col: 0 }, 5)
    ], 0);
    expect(findOccupant(a.model, "P")).toBeDefined();
    const b = applyLawnSession(a.model, [
      evt("zombie.spawn", { ptr: "Z", type: 0, row: 1, col: 3 }, 2)
    ], a.lastEventId);
    expect(findOccupant(b.model, "P")).toBeUndefined();
    expect(findOccupant(b.model, "Z")).toBeDefined();
  });

  it("fingerprint ignores lastHit", () => {
    const a = applyLawnSession(emptyLawnViewModel(), [
      evt("plant.spawn", { ptr: "P", type: 1, row: 0, col: 0 }, 1)
    ], 0);
    const withHit = {
      ...a.model,
      lastHit: { damage: 9, targetPtr: "P", source: "combat.hit" }
    };
    expect(membershipFingerprint(withHit)).toBe(membershipFingerprint(a.model));
  });
});
