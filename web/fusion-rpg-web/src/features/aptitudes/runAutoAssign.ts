/**
 * Host-side auto-assign: fill draft only (AS-3.3). Fetches favour / active preset when needed.
 */
import {
  applyAutoAssignShares,
  fillAutoAssign,
  type AutoAssignResult,
  type AutoAssignRule
} from "./autoAssign";
import {
  fetchAptitudePresetActive,
  fetchAptitudePresetFavour,
  materializeAptitudePreset,
  type AptitudePreset
} from "@/lib/bus/aptitudePresets";
import { getJson } from "@/lib/bus/rest";

export type AutoAssignHostMode = "unique" | "species" | "commander";

export type RunAutoAssignArgs = {
  rule: string;
  budget: number;
  mode: AutoAssignHostMode;
  playerId: number;
  /** Mode B required for species-favour; Mode A optional when known. */
  speciesId?: string | null;
  /** Mode A unique / Mode B species / Mode C empty. */
  scopeKey?: string | null;
  setValue: (id: string, next: number) => void;
};

export async function runAutoAssign(args: RunAutoAssignArgs): Promise<AutoAssignResult> {
  const favourAllowed = args.mode !== "commander";
  const rule = args.rule as AutoAssignRule;

  if (rule === "species-favour") {
    if (!favourAllowed) {
      return { ok: false, reason: "autoAssign.favour.modeC", shares: {}, leftover: 0 };
    }
    if (!args.speciesId) {
      return { ok: false, reason: "autoAssign.favour.noSpecies", shares: {}, leftover: 0 };
    }
    let favour: Record<string, number>;
    try {
      favour = await fetchAptitudePresetFavour(args.speciesId);
    } catch {
      return { ok: false, reason: "autoAssign.favour.fetchFailed", shares: {}, leftover: 0 };
    }
    const result = fillAutoAssign(rule, args.budget, {
      favourPermille: favour,
      favourAllowed: true
    });
    if (result.ok) applyAutoAssignShares(args.setValue, result.shares);
    return result;
  }

  if (rule === "active-preset") {
    const scope =
      args.mode === "unique" ? "unique" : args.mode === "species" ? "species" : "commander";
    try {
      const active = await fetchAptitudePresetActive(args.playerId, scope, args.scopeKey ?? "");
      if (!active?.presetId) {
        return { ok: false, reason: "autoAssign.activePreset.missing", shares: {}, leftover: 0 };
      }
      const mat = await materializeAptitudePreset(active.presetId, args.budget, args.playerId);
      const shares: Record<string, number> = {};
      for (const [id, v] of Object.entries(mat.shares ?? {})) {
        shares[id] = Number(v) || 0;
      }
      const spent = Object.values(shares).reduce((a, b) => a + b, 0);
      if (spent > args.budget) {
        return { ok: false, reason: "presets.materialize.overspend", shares, leftover: args.budget - spent };
      }
      applyAutoAssignShares(args.setValue, shares);
      return { ok: true, reason: "", shares, leftover: mat.leftover ?? args.budget - spent };
    } catch {
      // Fallback: load library + match active by hand if materialize failed oddly.
      try {
        const lib = await getJson<{ presets: AptitudePreset[] }>(
          `/api/aptitude-presets/${args.playerId}`
        );
        const active = await fetchAptitudePresetActive(args.playerId, scope, args.scopeKey ?? "");
        const preset = lib.presets?.find((p) => p.presetId === active?.presetId);
        if (!preset) {
          return { ok: false, reason: "autoAssign.activePreset.missing", shares: {}, leftover: 0 };
        }
        const permille: Record<string, number> = {};
        for (const row of preset.rows ?? []) {
          permille[row.aptitudeId] = Number(row.targetPermille) || 0;
        }
        const result = fillAutoAssign("active-preset", args.budget, {
          activePresetPermille: permille
        });
        if (result.ok) applyAutoAssignShares(args.setValue, result.shares);
        return result;
      } catch {
        return { ok: false, reason: "autoAssign.activePreset.fetchFailed", shares: {}, leftover: 0 };
      }
    }
  }

  const result = fillAutoAssign(rule, args.budget, { favourAllowed });
  if (result.ok) applyAutoAssignShares(args.setValue, result.shares);
  return result;
}
