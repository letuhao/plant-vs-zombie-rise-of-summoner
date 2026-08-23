import { cn } from "@/lib/cn";
import type { RailEntry } from "./railState";

/**
 * Band-1 (HUD). Identical on every stage (information-architecture.md §3) —
 * this component doesn't know which stage it's on beyond what `entries`
 * (already derived by `deriveRailEntries`) tells it.
 */
export function Rail({ entries, onSelect }: { entries: RailEntry[]; onSelect: (id: RailEntry["id"]) => void }) {
  return (
    <nav
      className="band-hud flex items-center gap-1 overflow-x-auto border-b border-border bg-soil-raised px-3 py-2"
      data-testid="rail"
      aria-label="Layers"
    >
      {entries.map((entry) => (
        <button
          key={entry.id}
          type="button"
          data-testid={`rail-${entry.id}`}
          data-state={entry.state}
          disabled={entry.state === "locked"}
          title={entry.state === "locked" ? entry.lockedReason : undefined}
          onClick={() => entry.state !== "locked" && onSelect(entry.id)}
          className={cn(
            "relative flex shrink-0 items-center gap-1.5 rounded-sm border px-2.5 py-1.5 text-sm font-semibold transition-colors",
            entry.state === "active" && "border-lawn-hot bg-lawn text-text",
            entry.state === "available" && "border-transparent text-muted hover:bg-panel hover:text-text",
            entry.state === "badged" && "border-transparent text-text",
            entry.state === "locked" && "cursor-not-allowed border-transparent text-faint opacity-60"
          )}
        >
          <span>{entry.label}</span>
          {entry.state === "locked" ? <span aria-hidden="true">🔒</span> : null}
          {entry.state === "badged" && entry.badgeCount ? (
            <span
              data-testid={`rail-${entry.id}-badge`}
              className="flex h-4 min-w-4 items-center justify-center rounded-pill bg-bad-solid px-1 text-2xs text-text"
            >
              {entry.badgeCount}
            </span>
          ) : null}
        </button>
      ))}
    </nav>
  );
}
