/** Shared actor HUD display tokens — Inspector (CSS) + Phaser canvas (numeric colors). */

/** Structural mirror of `data/tuning/actor-hud.v1.json` `statusStripMax` (web does not load tuning file v1). */
export const STATUS_STRIP_MAX = 3;

export const ELEMENT_COLORS_HEX: Record<string, string> = {
  fire: "#e07040",
  ice: "#60a8e0",
  poison: "#70c060",
  lightning: "#e0d040",
  physical: "#a0a0a8"
};

/** Phaser fill colors — keep in sync with ELEMENT_COLORS_HEX. */
export const ELEMENT_COLORS: Record<string, number> = {
  fire: 0xe07040,
  ice: 0x60a8e0,
  poison: 0x70c060,
  lightning: 0xe0d040,
  physical: 0xa0a0a8
};

export const TIER_BORDER: Record<string, string> = {
  normal: "border-border-control",
  elite: "border-rarity-4",
  boss: "border-bad-solid",
  unique: "border-rarity-5"
};

export const TIER_STROKE: Record<string, number> = {
  normal: 0x6a6258,
  elite: 0x9b7ddb,
  boss: 0xcc4444,
  unique: 0xe8b040
};

export function elementColorHex(element: string): string {
  return ELEMENT_COLORS_HEX[element.toLowerCase()] ?? ELEMENT_COLORS_HEX.physical;
}

export function elementColorPhaser(element: string): number {
  return ELEMENT_COLORS[element.toLowerCase()] ?? ELEMENT_COLORS.physical;
}

export function statusInitials(id: string): string {
  const parts = id.split("_").filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  return id.slice(0, 2).toUpperCase();
}

export function tierBadgeLetter(tier: string): string {
  if (tier === "unique") return "U";
  if (tier === "elite") return "E";
  if (tier === "boss") return "B";
  return "";
}
