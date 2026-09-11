/**
 * FE mirror of Core AptitudeAutoAssign — draft-only fills (AS-3.3).
 * Leftover after fill is legal. Use BigInt-safe integer math (budget * pm / 1000).
 */

export type AutoAssignRule =
  | "even"
  | "posture-force"
  | "posture-finesse"
  | "posture-bastion"
  | "active-preset"
  | "species-favour";

export type AutoAssignResult = {
  ok: boolean;
  reason: string;
  shares: Record<string, number>;
  leftover: number;
};

const POSTURE_OF: Record<string, "force" | "finesse" | "bastion"> = {
  Might: "force",
  Fortitude: "force",
  Vigor: "force",
  Onslaught: "force",
  Agility: "finesse",
  Composure: "finesse",
  Pierce: "finesse",
  Focus: "finesse",
  Bulwark: "bastion",
  Retribution: "bastion",
  Precision: "bastion",
  Ferocity: "bastion"
};

export const APTITUDE_IDS = Object.keys(POSTURE_OF);

function fail(reason: string): AutoAssignResult {
  return { ok: false, reason, shares: {}, leftover: 0 };
}

/** Scale permille map to budget (same shape as Core materialize without D13 clamps). */
export function fillFromPermille(
  budget: number,
  sharesPermille: Record<string, number>
): AutoAssignResult {
  if (budget < 0) return fail("presets.budget.negative");
  const keys = Object.keys(sharesPermille);
  if (keys.length === 0) return fail("autoAssign.favour.empty");

  let sumPm = 0;
  const shares: Record<string, number> = {};
  let spent = 0;
  for (const id of APTITUDE_IDS) {
    if (!(id in sharesPermille)) return fail("autoAssign.favour.incomplete");
    const pm = Math.trunc(sharesPermille[id]!);
    sumPm += pm;
    const share = Math.trunc((budget * pm) / 1000);
    shares[id] = share;
    spent += share;
  }
  if (sumPm !== 1000) return fail("presets.targetPermille.sum");
  return { ok: true, reason: "", shares, leftover: budget - spent };
}

export function fillEven(budget: number): AutoAssignResult {
  if (budget < 0) return fail("presets.budget.negative");
  const n = APTITUDE_IDS.length;
  const each = Math.trunc(budget / n);
  const shares: Record<string, number> = {};
  let spent = 0;
  for (const id of APTITUDE_IDS) {
    shares[id] = each;
    spent += each;
  }
  return { ok: true, reason: "", shares, leftover: budget - spent };
}

export function fillPosture(
  budget: number,
  posture: "force" | "finesse" | "bastion"
): AutoAssignResult {
  if (budget < 0) return fail("presets.budget.negative");
  const ids = APTITUDE_IDS.filter((id) => POSTURE_OF[id] === posture);
  const each = Math.trunc(budget / ids.length);
  const shares: Record<string, number> = {};
  let spent = 0;
  for (const id of APTITUDE_IDS) {
    const v = ids.includes(id) ? each : 0;
    shares[id] = v;
    spent += v;
  }
  return { ok: true, reason: "", shares, leftover: budget - spent };
}

export function fillAutoAssign(
  rule: AutoAssignRule | string,
  budget: number,
  opts?: {
    favourPermille?: Record<string, number>;
    activePresetPermille?: Record<string, number>;
    /** Mode A/B true; Mode C false — species-favour refused. */
    favourAllowed?: boolean;
  }
): AutoAssignResult {
  switch (rule) {
    case "even":
      return fillEven(budget);
    case "posture-force":
      return fillPosture(budget, "force");
    case "posture-finesse":
      return fillPosture(budget, "finesse");
    case "posture-bastion":
      return fillPosture(budget, "bastion");
    case "species-favour":
      if (opts?.favourAllowed === false) return fail("autoAssign.favour.modeC");
      return fillFromPermille(budget, opts?.favourPermille ?? {});
    case "active-preset":
      if (!opts?.activePresetPermille || Object.keys(opts.activePresetPermille).length === 0) {
        return fail("autoAssign.activePreset.missing");
      }
      return fillFromPermille(budget, opts.activePresetPermille);
    default:
      return fail("autoAssign.rule.unknown");
  }
}

/** Apply a fill result into a draft via per-id setValue (no POST). */
export function applyAutoAssignShares(
  setValue: (id: string, next: number) => void,
  shares: Record<string, number>
): void {
  for (const id of APTITUDE_IDS) {
    setValue(id, shares[id] ?? 0);
  }
}
