import type { NotifyCategory } from "./categories";

export type RailItemState = "unread" | "opened" | "dismissed" | "minimized" | "blocking";

export type RailItem = {
  id: string;
  category: NotifyCategory;
  /** Already translated by `world-playback`. This module never sees an engine token. */
  title: string;
  body: string;
  state: RailItemState;
  /** Blockers cannot be dismissed and do not flush — spec-world-notify.md §2. */
  readonly blocking: boolean;
};

/** End Turn flush. The one rule, in one line, so it cannot drift. */
export const flush = (items: RailItem[]): RailItem[] => items.filter((i) => i.blocking);

/**
 * Fires on `WorldTurnCommitDto.Advanced`, never on the button press — a commit that did not advance
 * (a resend, a barrier still waiting) has not ended a turn, so the feed must not empty for it.
 */
export function onCommit(items: RailItem[], advanced: boolean): RailItem[] {
  return advanced ? flush(items) : items;
}

/** Opening and dismissing are two gestures with two outcomes (§3) — this only clears "unread". */
export function open(items: RailItem[], id: string): RailItem[] {
  return items.map((i) => (i.id === id && i.state === "unread" ? { ...i, state: "opened" } : i));
}

/**
 * Removed from the feed, never from history — `world-playback` holds the record, so this store never
 * deletes the item outright; it marks it dismissed, and the next End Turn flush is what actually
 * clears it (alongside every other non-blocking item). A blocker refuses silently: the state is
 * already `"blocking"` and this leaves it exactly as it was.
 */
export function dismiss(items: RailItem[], id: string): RailItem[] {
  return items.map((i) => (i.id === id && !i.blocking ? { ...i, state: "dismissed" } : i));
}

/** A per-category state, not a per-message one — where a whole category lands once routed here. */
export function minimizeCategory(items: RailItem[], category: NotifyCategory): RailItem[] {
  return items.map((i) => (i.category === category && !i.blocking ? { ...i, state: "minimized" } : i));
}
