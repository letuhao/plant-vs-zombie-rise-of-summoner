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
    // world-numbers W37: a trailing ".0" is trimmed — 15.0% reads as 15%.
    expect(formatMagnitude(mag("perMilleRatio", 150, { op: "increased" }))).toBe("+15%");
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
    // world-numbers W37: a trailing ".0" is trimmed — 25.0% reads as 25%.
    expect(formatMagnitude(mag("perMilleRatio", 250, { op: "flat" }))).toBe("25%");
  });

  it("perMilleRatio absolute: ×1.40 from the raw 1400 — no delta-from-1000 arithmetic (world-numbers W37, the verified defect's fix)", () => {
    expect(formatMagnitude(mag("perMilleRatio", 1400, { op: "absolute" }))).toBe("×1.40");
    // The neutral baseline itself renders neutral.
    expect(formatMagnitude(mag("perMilleRatio", 1000, { op: "absolute" }))).toBe("×1.00");
  });

  it("perMilleRatio flat: StabilityMilli 240 renders 24%, not 24.0% — the acceptance's own named example", () => {
    expect(formatMagnitude(mag("perMilleRatio", 240, { op: "flat" }))).toBe("24%");
  });

  it("perMilleRatio: a non-trivial decimal is not trimmed away", () => {
    expect(formatMagnitude(mag("perMilleRatio", 245, { op: "flat" }))).toBe("24.5%");
  });

  it("perMilleRatio: a small non-zero value never renders as 0% — the smallest per-mille integer already clears one decimal", () => {
    expect(formatMagnitude(mag("perMilleRatio", 1, { op: "flat" }))).toBe("0.1%");
    expect(formatMagnitude(mag("perMilleRatio", 1, { op: "flat" }))).not.toBe("0%");
  });

  it("perMilleRatio: a genuine zero still renders 0%, not merely trimmed away to nothing", () => {
    expect(formatMagnitude(mag("perMilleRatio", 0, { op: "flat" }))).toBe("0%");
  });

  it("loamUnits: a whole, unsigned count — the class the four …Milli loam-cost fields actually are", () => {
    expect(formatMagnitude(mag("loamUnits", 200))).toBe("200");
    expect(formatMagnitude(mag("loamUnits", 0))).toBe("0");
  });

  it("movementRemaining (perMilleRatio, op flat) renders as a fraction of the march budget, never a bare count", () => {
    // 750‰ of one turn's march budget — never "750 movement".
    const rendered = formatMagnitude(mag("perMilleRatio", 750, { op: "flat" }));
    expect(rendered).toBe("75%");
    expect(rendered).not.toContain("movement");
    expect(rendered).not.toBe("750");
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
