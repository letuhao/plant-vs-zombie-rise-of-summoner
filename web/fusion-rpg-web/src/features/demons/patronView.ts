/**
 * Patron display math (spec-patron-demon.md) — mirrors PatronPolicy server-side; vitest-pinned
 * like STAR_CAPS so drift shows up in CI, and the server stays authoritative for real values.
 */

export const PATRON_RARITY_BASE_MILLI: Record<string, number> = {
  common: 20,
  rare: 30,
  epic: 45,
  legendary: 60
};

export const PATRON_CLAMP_MILLI = 150;
export const PATRON_PER_STAR_MILLI = 10;

export function auraPreviewMilli(rarity: string, star: number, level: number): number {
  const base = PATRON_RARITY_BASE_MILLI[rarity] ?? 0;
  return Math.min(PATRON_CLAMP_MILLI, Math.max(0, base + PATRON_PER_STAR_MILLI * star + level));
}

/** "+7.5% fire power · +3.7% defense" style label from per-mille values. */
export function auraLabel(elementPrimary: string, powerMilli: number, defenseMilli: number): string {
  const pct = (milli: number) => `${(milli / 10).toFixed(1).replace(/\.0$/, "")}%`;
  return `+${pct(powerMilli)} ${elementPrimary} power · +${pct(defenseMilli)} defense`;
}
