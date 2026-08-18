import { describe, expect, it } from "vitest";
import {
  canEnterSpawnTargeting,
  idleInteraction,
  reduceInteraction
} from "./interactionMode";

describe("interactionMode", () => {
  it("clears to Idle", () => {
    let s = reduceInteraction(
      idleInteraction(),
      { type: "selectTile", row: 0, col: 0 },
      "InMatch"
    );
    s = reduceInteraction(s, { type: "clear" }, "InMatch");
    expect(s).toEqual({ mode: "Idle" });
  });

  it("selects tile and occupant", () => {
    let s = idleInteraction();
    s = reduceInteraction(s, { type: "selectTile", row: 1, col: 2 }, "InMatch");
    expect(s.mode).toBe("TileSelected");
    s = reduceInteraction(
      s,
      { type: "selectOccupant", ptr: "P1", row: 1, col: 2 },
      "InMatch"
    );
    expect(s.mode).toBe("OccupantSelected");
    expect(s.ptr).toBe("P1");
  });

  it("phase-gates SpawnTargeting (RT-06)", () => {
    expect(canEnterSpawnTargeting("Idle")).toBe(false);
    expect(canEnterSpawnTargeting("Ending")).toBe(false);
    expect(canEnterSpawnTargeting("Starting")).toBe(true);
    expect(canEnterSpawnTargeting("Paused")).toBe(true);
    expect(canEnterSpawnTargeting("InMatch")).toBe(true);

    let s = idleInteraction();
    s = reduceInteraction(s, { type: "enterSpawnTargeting" }, "Idle");
    expect(s.mode).toBe("Idle");

    s = reduceInteraction(s, { type: "enterSpawnTargeting" }, "Ending");
    expect(s.mode).toBe("Idle");

    s = reduceInteraction(s, { type: "enterSpawnTargeting" }, "InMatch");
    expect(s.mode).toBe("SpawnTargeting");

    s = reduceInteraction(s, { type: "phaseChanged", phase: "Ending" }, "Ending");
    expect(s.mode).toBe("Idle");
  });

  it("enterSpawnTargeting from OccupantSelected clears ptr keeps cell", () => {
    let s = reduceInteraction(
      idleInteraction(),
      { type: "selectOccupant", ptr: "AB", row: 2, col: 3 },
      "InMatch"
    );
    s = reduceInteraction(s, { type: "enterSpawnTargeting" }, "InMatch");
    expect(s).toEqual({
      mode: "SpawnTargeting",
      row: 2,
      col: 3,
      ptr: undefined
    });
  });

  it("SpawnTargeting selectTile keeps mode and updates cell", () => {
    let s = reduceInteraction(
      { mode: "TileSelected", row: 2, col: 3 },
      { type: "enterSpawnTargeting" },
      "InMatch"
    );
    s = reduceInteraction(s, { type: "selectTile", row: 0, col: 1 }, "InMatch");
    expect(s).toEqual({ mode: "SpawnTargeting", row: 0, col: 1, ptr: undefined });
  });

  it("selectOccupant from SpawnTargeting exits to OccupantSelected", () => {
    let s = reduceInteraction(
      idleInteraction(),
      { type: "enterSpawnTargeting" },
      "InMatch"
    );
    s = reduceInteraction(s, { type: "selectTile", row: 1, col: 1 }, "InMatch");
    s = reduceInteraction(
      s,
      { type: "selectOccupant", ptr: "Z9", row: 1, col: 1 },
      "InMatch"
    );
    expect(s.mode).toBe("OccupantSelected");
    expect(s.ptr).toBe("Z9");
  });

  it("phaseChanged keeps TileSelected / OccupantSelected", () => {
    let s = reduceInteraction(
      idleInteraction(),
      { type: "selectOccupant", ptr: "X" },
      "InMatch"
    );
    s = reduceInteraction(s, { type: "phaseChanged", phase: "Paused" }, "Paused");
    expect(s.mode).toBe("OccupantSelected");
    expect(s.ptr).toBe("X");
  });

  it("clear from each mode", () => {
    for (const start of [
      reduceInteraction(idleInteraction(), { type: "selectTile", row: 0, col: 0 }, "InMatch"),
      reduceInteraction(
        idleInteraction(),
        { type: "selectOccupant", ptr: "P" },
        "InMatch"
      ),
      reduceInteraction(idleInteraction(), { type: "enterSpawnTargeting" }, "InMatch")
    ]) {
      expect(reduceInteraction(start, { type: "clear" }, "InMatch").mode).toBe("Idle");
    }
  });
});
