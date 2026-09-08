/**
 * world-stage W85 (spec-world-notify.md §4) — the closed category list. A category is what a channel
 * setting applies to; the loam vocabulary already names the top tier without inventing a priority
 * scheme. This module never parses an engine token — it is data, consumed by `notifyRailStore.ts` and
 * `channelSettings.ts` against a keyframe `world-playback` has already translated.
 */
export type NotifyCategory =
  | "loam.shortfall"
  | "loam.release"
  | "legion.runway"
  | "battle.result"
  | "supply.change"
  | "growth"
  | "intel.new"
  | "command.dropped";

export type NotifyChannel = "toast" | "rail" | "off";

/**
 * Every category *not* in this list defaults to the rail and has to earn a promotion — a category
 * arriving on Toast by default is a spec change to this list, never a quiet line in a component.
 */
export const TOAST_TIER: readonly NotifyCategory[] = ["loam.shortfall", "loam.release", "legion.runway"];

/**
 * Battle results default to the rail on purpose (§4, §5) — ES2 shipped a battle notification as its
 * canonical hard blocker, the community called it a feature not a bug, and Amplitude patched it back
 * out ("Battle Result Notifications no longer block the turn"). The strongest candidate for a Toast
 * default in our own game — ground releasing tonight — is `loam.release`, already in `TOAST_TIER`.
 */
export const CATEGORY_DEFAULT_CHANNEL: Readonly<Record<NotifyCategory, NotifyChannel>> = {
  "loam.shortfall": "toast",
  "loam.release": "toast",
  "legion.runway": "toast",
  "battle.result": "rail",
  "supply.change": "rail",
  growth: "rail",
  "intel.new": "rail",
  "command.dropped": "rail"
};

export const ALL_CATEGORIES: readonly NotifyCategory[] = Object.keys(
  CATEGORY_DEFAULT_CHANNEL
) as NotifyCategory[];
