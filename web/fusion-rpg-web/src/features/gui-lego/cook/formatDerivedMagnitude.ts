import type { Magnitude, UnitClass } from "@/contract/types";
import { formatMagnitude } from "@/i18n/magnitude";
import type { MagnitudeDisplay } from "../types";

/** Cook catalog UnitClass (Pascal / wire) → contract UnitClass. */
const COOK_TO_UNIT: Record<string, UnitClass | "unitInterval"> = {
  GameUnits: "gameUnits",
  PerMilleRatio: "perMilleRatio",
  SigmoidPoints: "sigmoidPoints",
  SigmoidMultiplierPoints: "sigmoidMultiplierPoints",
  StatusPotencyPoints: "statusPotencyPoints",
  UnitInterval: "unitInterval",
  Flag: "flag",
  gameUnits: "gameUnits",
  perMilleRatio: "perMilleRatio",
  sigmoidPoints: "sigmoidPoints",
  unitInterval: "unitInterval",
  flag: "flag"
};

/**
 * GG-46 adapter for Derived fold — always goes through formatMagnitude when a
 * UnitClass exists; UnitInterval stays a fixed two-decimal display (no Magnitude unit).
 */
export function formatDerivedMagnitude(
  value: number,
  unitClass: string,
  locale = "en"
): MagnitudeDisplay {
  const mapped = COOK_TO_UNIT[unitClass] ?? "gameUnits";
  if (mapped === "unitInterval") {
    return {
      valueRaw: value,
      valueText: value.toFixed(2),
      unitLabel: null,
      formatterId: "UnitInterval"
    };
  }
  const m: Magnitude = {
    unit: mapped,
    value,
    ...(mapped === "perMilleRatio" ? { op: "flat" as const } : {})
  };
  return {
    valueRaw: value,
    valueText: formatMagnitude(m, locale),
    unitLabel: null,
    formatterId: mapped
  };
}
