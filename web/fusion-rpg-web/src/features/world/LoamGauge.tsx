import { cn } from "@/lib/cn";
import type { LoamEmpireSummary } from "./worldViewModel";

/**
 * The empire's loam reading (spec-loam-fe.md: "the gauge") — income, upkeep, net, and stock, always
 * visible, the way a city-builder shows power. Split into a row per component once territory is
 * split, because "my empire is fine" can be false while half of it starves. Player words only:
 * never `componentId`, `stabilityMilli`, or `intensityMilli`.
 */
export function LoamGauge({ summary }: { summary: LoamEmpireSummary }) {
  if (summary.components.length === 0) {
    return (
      <p className="text-sm text-muted" data-testid="loam-gauge-empty">
        No territory of your own to draw on yet.
      </p>
    );
  }

  return (
    <div data-testid="loam-gauge" className="space-y-2">
      <dl className="grid grid-cols-4 gap-2 text-center" data-testid="loam-gauge-empire">
        <Reading label="Income" value={summary.production} />
        <Reading label="Upkeep" value={summary.upkeep} />
        <Reading label="Net" value={summary.net} signed />
        <Reading label="Stock" value={summary.stock} />
      </dl>

      {summary.components.length > 1 ? (
        <>
          <p className="text-xs text-muted">Your supply is split into {summary.components.length} parts.</p>
          <ul className="space-y-1" data-testid="loam-gauge-components">
            {summary.components.map((c) => (
              <li
                key={c.componentId}
                data-testid={`loam-component-${c.componentId}`}
                className={cn(
                  "flex items-center justify-between gap-2 rounded-sm border px-2 py-1 text-xs",
                  c.net < 0 ? "border-bad/60 bg-bad/10" : "border-border"
                )}
              >
                <span className="text-muted">
                  {c.sectorCount} sector{c.sectorCount === 1 ? "" : "s"}
                </span>
                {c.net < 0 ? (
                  <span className="font-semibold text-bad" data-testid={`loam-component-warning-${c.componentId}`}>
                    can&apos;t cover its own keep
                  </span>
                ) : (
                  <span className="text-text">
                    {c.net >= 0 ? "+" : ""}
                    {c.net}/turn
                  </span>
                )}
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </div>
  );
}

function Reading({ label, value, signed }: { label: string; value: number; signed?: boolean }) {
  return (
    <div>
      <div className="text-[10px] uppercase tracking-wide text-muted">{label}</div>
      <div
        className={cn("font-display text-sm", signed && value < 0 ? "text-bad" : "text-text")}
        data-testid={`loam-gauge-${label.toLowerCase()}`}
      >
        {signed && value > 0 ? "+" : ""}
        {value}
      </div>
    </div>
  );
}
