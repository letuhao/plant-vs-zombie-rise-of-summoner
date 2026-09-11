import { describe, expect, it, vi } from "vitest";
import {
  applyAutoAssignShares,
  fillAutoAssign,
  fillEven,
  fillFromPermille,
  fillPosture
} from "./autoAssign";

describe("aptitude autoAssign (AS-3.3)", () => {
  it("even splits with leftover", () => {
    const r = fillEven(100);
    expect(r.ok).toBe(true);
    expect(Object.values(r.shares).every((v) => v === 8)).toBe(true);
    expect(r.leftover).toBe(4);
  });

  it("posture lean spends only that posture", () => {
    const r = fillPosture(40, "force");
    expect(r.ok).toBe(true);
    expect(r.shares.Might).toBe(10);
    expect(r.shares.Agility).toBe(0);
    expect(r.leftover).toBe(0);
  });

  it("empty favour refuses (S7)", () => {
    const r = fillFromPermille(100, {});
    expect(r.ok).toBe(false);
    expect(r.reason).toBe("autoAssign.favour.empty");
  });

  it("species-favour scales permille", () => {
    const favour: Record<string, number> = {};
    const ids = [
      "Might",
      "Fortitude",
      "Vigor",
      "Onslaught",
      "Agility",
      "Composure",
      "Pierce",
      "Focus",
      "Bulwark",
      "Retribution",
      "Precision",
      "Ferocity"
    ];
    for (const id of ids) favour[id] = 83;
    favour.Might = 83 + (1000 - 83 * 12);
    const r = fillAutoAssign("species-favour", 1000, { favourPermille: favour });
    expect(r.ok).toBe(true);
    expect(r.leftover + Object.values(r.shares).reduce((a, b) => a + b, 0)).toBe(1000);
  });

  it("Mode C refuses species-favour", () => {
    const r = fillAutoAssign("species-favour", 100, {
      favourAllowed: false,
      favourPermille: { Might: 1000 }
    });
    expect(r.ok).toBe(false);
    expect(r.reason).toBe("autoAssign.favour.modeC");
  });

  it("Mode C even still works", () => {
    expect(fillAutoAssign("even", 12, { favourAllowed: false }).ok).toBe(true);
  });

  it("applyAutoAssignShares writes via setValue only", () => {
    const setValue = vi.fn();
    applyAutoAssignShares(setValue, { Might: 7, Fortitude: 3 });
    expect(setValue).toHaveBeenCalledWith("Might", 7);
    expect(setValue).toHaveBeenCalledWith("Fortitude", 3);
    expect(setValue).toHaveBeenCalledWith("Agility", 0);
    expect(setValue.mock.calls).toHaveLength(12);
  });
});
