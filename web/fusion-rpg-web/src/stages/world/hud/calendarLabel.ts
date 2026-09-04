/**
 * The calendar slot's data (world-stage W53) — `WorldStateDto.Calendar` (`world-wire` W15), never a
 * `calendar` report entry (`TurnEngine.cs:225-231` only emits one on a week boundary, blank six
 * turns out of seven). `TurnCalendar.cs` is pure in `(turn, seed)`; the seed never reaches the wire,
 * but `DaysPerWeek`/`WeeksPerMonth` do, and placing today's turn in its week and month from those two
 * public tunables is ordinary calendar arithmetic — not a re-derivation of the hidden roll, which
 * only ever decides the boolean flags below.
 *
 * **§8b.7 is binding: this is a calendar, not a season.** No season name, no "Long Wither" — only
 * the day/week/month numbers plus whichever of `plague`/`specialMonth`/`specialWeek` the server
 * actually rolled. `Roll()`'s own rule (`plague` beats `specialMonth` on the same month) means at
 * most one of those two ever fires together; `specialWeek` can still co-occur on a month-boundary
 * turn, so the label surfaces plague/specialMonth as the headline flavour and folds specialWeek in
 * as a second clause rather than dropping it silently.
 */
export type CalendarRollView = {
  daysPerWeek: number;
  weeksPerMonth: number;
  weekBoundary: boolean;
  monthBoundary: boolean;
  specialWeek: boolean;
  specialMonth: boolean;
  plague: boolean;
};

export type CalendarLabel = {
  turn: number;
  /** 1-indexed: the very first turn (turn 0) is week 1 of month 1. */
  week: number;
  month: number;
  /** `null` on a plain day — no flavour to report, not an empty gap. */
  flavour: string | null;
};

export function calendarLabelFor(turn: number, calendar: CalendarRollView): CalendarLabel {
  const day = Math.max(0, turn);
  // `TurnCalendar.cs`'s own boundary check is `turn % daysPerWeek == 0` — turn 7 (with
  // daysPerWeek=7) is the *last* day of week 1, so turn counts completed days, 1-indexed; turn 0 is
  // the pre-game default (week 1, month 1, matching `Roll`'s own `turn <= 0 → default` rule).
  const week = day === 0 ? 1 : Math.floor((day - 1) / calendar.daysPerWeek) + 1;
  const month = Math.floor((week - 1) / calendar.weeksPerMonth) + 1;

  const clauses: string[] = [];
  if (calendar.plague) clauses.push("plague");
  else if (calendar.specialMonth) clauses.push("a special month");
  if (calendar.specialWeek) clauses.push("a special week");

  return { turn, week, month, flavour: clauses.length > 0 ? clauses.join(", ") : null };
}

/** Plain text for the strip — e.g. `"Day 22 · Week 4 · Month 1"`, with a flavour clause appended. */
export function formatCalendarLabel(label: CalendarLabel): string {
  const base = `Day ${label.turn} · Week ${label.week} · Month ${label.month}`;
  return label.flavour ? `${base} — ${label.flavour}` : base;
}
