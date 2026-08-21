/** Pure fusion-lab display logic (spec-demon-fusion.md F9) — vitest-covered. */

export type FusionCostDto = {
  souls: number;
  shardMaterialId: string;
  shardCount: number;
  essenceMaterialId: string;
  essenceCount: number;
};

export type MaterialShelf = { materialId: string; qty: number }[];

export type HaveNeedLine = { label: string; have: number; need: number; enough: boolean };

/** Cost rows with have/need against the shelf + Souls balance; affordable = every line enough. */
export function haveNeed(cost: FusionCostDto, shelf: MaterialShelf, soulsBalance: number): {
  lines: HaveNeedLine[];
  affordable: boolean;
} {
  const qty = (id: string) => shelf.find((m) => m.materialId === id)?.qty ?? 0;
  const lines: HaveNeedLine[] = [
    { label: "Souls", have: soulsBalance, need: cost.souls, enough: soulsBalance >= cost.souls },
    {
      label: cost.shardMaterialId,
      have: qty(cost.shardMaterialId),
      need: cost.shardCount,
      enough: qty(cost.shardMaterialId) >= cost.shardCount
    },
    {
      label: cost.essenceMaterialId,
      have: qty(cost.essenceMaterialId),
      need: cost.essenceCount,
      enough: qty(cost.essenceMaterialId) >= cost.essenceCount
    }
  ];
  return { lines, affordable: lines.every((l) => l.enough) };
}

/** Star pips: filled to `star`, hollow to the cap. */
export function starPips(star: number, cap: number): string {
  const filled = Math.max(0, Math.min(star, cap));
  return "★".repeat(filled) + "☆".repeat(Math.max(0, cap - filled));
}

export const STAR_CAPS: Record<string, number> = {
  common: 3,
  rare: 4,
  epic: 5,
  legendary: 5
};

/** Recipe browser label: undiscovered entries stay silhouetted ("???"), hints show ingredients. */
export function recipeLabel(item: {
  discovered: boolean;
  resultSpeciesId?: string | null;
  inputs?: string[] | null;
}, speciesName: (id: string) => string): { title: string; subtitle: string } {
  if (item.discovered && item.resultSpeciesId) {
    return {
      title: speciesName(item.resultSpeciesId),
      subtitle: (item.inputs ?? []).map(speciesName).join(" + ")
    };
  }
  return {
    title: "???",
    subtitle: item.inputs ? item.inputs.map(speciesName).join(" + ") + " …?" : "Undiscovered"
  };
}
