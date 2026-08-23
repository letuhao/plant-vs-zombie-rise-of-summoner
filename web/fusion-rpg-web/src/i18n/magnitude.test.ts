import { describe, expect, it } from "vitest";
import type { Magnitude } from "@/contract/types";
import { formatMagnitude, formatSigmoidContext } from "./magnitude";

function mag(unit: Magnitude["unit"], value: number, extra?: Partial<Magnitude>): Magnitude {
  return { unit, value, ...extra };
}

describe("formatMagnitude — one golden case per unit class (spec-magnitude-and-units.md §3)", () => {
  it("gameUnits: signed integer, no unit suffix (composed by the DisplayLine, not here)", () => {
    expect(formatMagnitude(mag("gameUnits", 45))).toBe("+45");
    expect(formatMagnitude(mag("gameUnits", -12))).toBe("−12");
    expect(formatMagnitude(mag("gameUnits", 0))).toBe("0");
  });

  it("gameUnitsPerSecond: same signed-integer shape", () => {
    expect(formatMagnitude(mag("gameUnitsPerSecond", 3))).toBe("+3");
  });

  it("sigmoidPoints: the raw points, signed — not a percentage (that's formatSigmoidContext's job)", () => {
    expect(formatMagnitude(mag("sigmoidPoints", 30))).toBe("+30");
  });

  it("sigmoidMultiplierPoints: same shape", () => {
    expect(formatMagnitude(mag("sigmoidMultiplierPoints", 40))).toBe("+40");
  });

  it("statusPotencyPoints: same shape", () => {
    expect(formatMagnitude(mag("statusPotencyPoints", 8))).toBe("+8");
  });

  it("perMilleRatio increased: +15% (per-mille divided by 10, SC4 display posture)", () => {
    expect(formatMagnitude(mag("perMilleRatio", 150, { op: "increased" }))).toBe("+15.0%");
  });

  it("perMilleRatio more: ×1.15 — a multiplier, not a percentage (Guard #5: increased/more differ)", () => {
    expect(formatMagnitude(mag("perMilleRatio", 150, { op: "more" }))).toBe("×1.15");
  });

  it("perMilleRatio increased and more render differently for the same raw value (Guard #5)", () => {
    const increased = formatMagnitude(mag("perMilleRatio", 150, { op: "increased" }));
    const more = formatMagnitude(mag("perMilleRatio", 150, { op: "more" }));
    expect(increased).not.toBe(more);
  });

  it("perMilleRatio flat: a plain percentage (chance/share)", () => {
    expect(formatMagnitude(mag("perMilleRatio", 250, { op: "flat" }))).toBe("25.0%");
  });

  it("milliseconds under one second render as ms", () => {
    expect(formatMagnitude(mag("milliseconds", 250))).toBe("250 ms");
  });

  it("milliseconds at or over one second render as seconds", () => {
    expect(formatMagnitude(mag("milliseconds", 4000))).toBe("4.0 s");
  });

  it("count: a bare integer", () => {
    expect(formatMagnitude(mag("count", 2))).toBe("2");
  });

  it("flag: present/absent, never a number", () => {
    expect(formatMagnitude(mag("flag", 1))).toBe("present");
    expect(formatMagnitude(mag("flag", 0))).toBe("absent");
    expect(formatMagnitude(mag("flag", 1))).not.toMatch(/\d/);
  });
});

describe("formatMagnitude — CJK locale fixture", () => {
  it("does not break under a CJK locale code, and groups large numbers per that locale", () => {
    // English only ships this pass (web/spec.md §10); this proves the plumbing survives a real
    // locale code rather than assuming it — Intl.NumberFormat is locale-aware even though no
    // Japanese catalog exists yet.
    expect(() => formatMagnitude(mag("count", 12345), "ja-JP")).not.toThrow();
    expect(formatMagnitude(mag("count", 12345), "ja-JP")).toBe("12,345");
    expect(formatMagnitude(mag("gameUnits", 45), "ja-JP")).toBe("+45");
  });
});

describe("formatSigmoidContext — the corrected two-part-line shape", () => {
  it("matches spec-magnitude-and-units.md's own worked example: +150 crit rate -> ~+31.8 pp vs neutral", () => {
    // The delta is against a 0-point (neutral) baseline: sigmoid(0) = 0.5, sigmoid(1.5) ~= 0.8176.
    expect(formatSigmoidContext(150, { kind: "neutral" })).toBe("≈ +31.8 pp vs neutral");
  });

  it("names a specimen reference when given one, not just 'neutral'", () => {
    expect(formatSigmoidContext(150, { kind: "specimen", label: "Peashooter" })).toBe(
      "≈ +31.8 pp vs Peashooter"
    );
  });

  it("a negative delta renders a negative pp with the real minus sign", () => {
    expect(formatSigmoidContext(-150, { kind: "neutral" })).toBe("≈ −31.8 pp vs neutral");
  });

  it("never renders the rejected two-bare-percentages shape (no '%' or '->' in its output)", () => {
    const text = formatSigmoidContext(150, { kind: "neutral" });
    expect(text).not.toContain("%");
    expect(text).not.toContain("->");
    expect(text).not.toContain("→");
  });
});
