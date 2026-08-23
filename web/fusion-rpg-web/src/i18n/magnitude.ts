import type { Magnitude } from "@/contract/types";

/**
 * Renders one `Magnitude`'s numeric portion. **No overload accepts a bare
 * `number`** — that omission is the GG-46 guard (spec-magnitude-and-units.md
 * §7): an unlabelled magnitude cannot be passed, so it cannot be rendered.
 * The surrounding sentence (the noun — "hp", "crit rate", "shield hp/s") is
 * composed by the `DisplayLine`'s own `key`/`args` template, not here — a
 * single `Magnitude` doesn't carry that label, only its class does.
 *
 * `locale` is accepted for the eventual per-locale number grouping
 * (`Intl.NumberFormat`) a second locale will need; unused while English is
 * the only shipped locale (web/spec.md §10).
 */
export function formatMagnitude(m: Magnitude, locale = "en"): string {
  switch (m.unit) {
    case "gameUnits":
    case "gameUnitsPerSecond":
    case "sigmoidPoints":
    case "sigmoidMultiplierPoints":
    case "statusPotencyPoints":
      return signedInt(m.value, locale);
    case "perMilleRatio":
      return formatPerMille(m.value, m.op);
    case "milliseconds":
      return formatMilliseconds(m.value);
    case "count":
      return new Intl.NumberFormat(locale).format(m.value);
    case "flag":
      // Never a number — present/absent per spec-magnitude-and-units.md's unit ledger.
      return m.value !== 0 ? "present" : "absent";
    default: {
      const exhaustive: never = m.unit;
      throw new Error(`formatMagnitude: unhandled UnitClass ${String(exhaustive)}`);
    }
  }
}

function signedInt(value: number, locale: string): string {
  const formatted = new Intl.NumberFormat(locale).format(Math.abs(value));
  if (value > 0) return `+${formatted}`;
  if (value < 0) return `−${formatted}`; // real minus sign, not a hyphen
  return formatted;
}

/**
 * `PerMilleRatio` carries `op` (spec-magnitude-and-units.md §7's `Magnitude.op`).
 * Guard #5 (§8): `increased` and `more` must render differently — rendering
 * both the same way is the exact defect that guard exists to catch.
 * The renderer divides the raw per-mille integer by 10 per SC4's display
 * posture (§9.1), independent of the engine-side latent defect the same
 * section documents (the FE renders correctly regardless of whether the
 * engine currently composes `increased` right).
 */
function formatPerMille(value: number, op: Magnitude["op"]): string {
  const pct = value / 10;
  switch (op) {
    case "more":
      return `×${(1 + value / 1000).toFixed(2)}`;
    case "increased":
      return pct >= 0 ? `+${pct.toFixed(1)}%` : `−${Math.abs(pct).toFixed(1)}%`;
    case "flat":
    default:
      return pct >= 0 ? `${pct.toFixed(1)}%` : `−${Math.abs(pct).toFixed(1)}%`;
  }
}

/** "4.0 s · 250 ms under one second" — spec-magnitude-and-units.md's unit ledger. */
function formatMilliseconds(value: number): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? "−" : "";
  if (abs < 1000) return `${sign}${abs} ms`;
  return `${sign}${(abs / 1000).toFixed(1)} s`;
}

const SIGMOID_STEEPNESS = 1.0;

/** `1/(1+e^(-x*steepness))` — ResistanceEvaluator.cs:111-112, the one legal sigmoid. */
function sigmoid(x: number, steepness = SIGMOID_STEEPNESS): number {
  return 1 / (1 + Math.exp(-x * steepness));
}

export type SigmoidReference = { kind: "neutral" } | { kind: "specimen"; label: string };

/** The three scales `CombatProbabilityPolicy` declares (CombatPolicies.cs:8-13). */
export const CombatProbabilityScale = {
  Accuracy: 100.0,
  CritRate: 100.0,
  CritDamage: 100.0
} as const;

/**
 * The `ContextRead.text` a `SigmoidPoints`/`SigmoidMultiplierPoints` line
 * carries — `≈ +31.8 pp vs neutral` (spec-magnitude-and-units.md §5, D.1's
 * corrected worked example). **Not** the two-bare-percentages pairing
 * ("7.6% → 26.9%") an earlier plate draft used — that shape was found and
 * rejected in the same document (§14 D.1 row) for showing two numbers with
 * no stated reference; a signed percentage-point delta against a named
 * reference is what replaced it, and this is that replacement, not the
 * pattern it replaced.
 */
export function formatSigmoidContext(
  deltaPoints: number,
  reference: SigmoidReference,
  scale = CombatProbabilityScale.CritRate
): string {
  const deltaPp = (sigmoid(deltaPoints / scale) - sigmoid(0)) * 100;
  const sign = deltaPp >= 0 ? "+" : "−";
  const referenceLabel = reference.kind === "neutral" ? "neutral" : reference.label;
  return `≈ ${sign}${Math.abs(deltaPp).toFixed(1)} pp vs ${referenceLabel}`;
}
