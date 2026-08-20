import type { DemonSpecimenDto } from "@/lib/bus/demons";

export const ACTIVE_CAP = 24;

const RARITY_ORDER: Record<string, number> = { legendary: 0, epic: 1, rare: 2, common: 3 };

export type ReserveStack = {
  speciesId: string;
  count: number;
  specimens: DemonSpecimenDto[];
};

/**
 * Active/Reserve split (spec-demon-summoning duplicate valve): Active holds up to 24 —
 * locked first, then rarity desc, then oldest first. Everything else stacks by species
 * in Reserve so the visible roster stays team-sized, not a warehouse.
 */
export function splitRoster(items: DemonSpecimenDto[]): {
  active: DemonSpecimenDto[];
  reserve: ReserveStack[];
} {
  const sorted = [...items].sort((a, b) => {
    if (a.profile.locked !== b.profile.locked) return a.profile.locked ? -1 : 1;
    const ra = RARITY_ORDER[a.profile.rarity] ?? 9;
    const rb = RARITY_ORDER[b.profile.rarity] ?? 9;
    if (ra !== rb) return ra - rb;
    return a.profile.createdUtc.localeCompare(b.profile.createdUtc);
  });

  const active = sorted.slice(0, ACTIVE_CAP);
  const rest = sorted.slice(ACTIVE_CAP);
  const stacks = new Map<string, ReserveStack>();
  for (const s of rest) {
    const stack = stacks.get(s.profile.speciesId);
    if (stack) {
      stack.count++;
      stack.specimens.push(s);
    } else {
      stacks.set(s.profile.speciesId, { speciesId: s.profile.speciesId, count: 1, specimens: [s] });
    }
  }

  return { active, reserve: [...stacks.values()].sort((a, b) => b.count - a.count) };
}

/** Pity progress line for the Summon panel — dead pulls rendered as visible progress. */
export function pityLine(sinceEpic: number, epicAt: number, sinceLegendary: number, legendaryAt: number): string {
  return `${sinceEpic}/${epicAt} to guaranteed Epic · ${sinceLegendary}/${legendaryAt} to guaranteed Legendary`;
}

export function displayName(s: DemonSpecimenDto, speciesName?: string): string {
  return s.profile.nickname?.trim() || speciesName || s.profile.speciesId;
}
