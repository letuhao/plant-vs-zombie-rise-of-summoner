import { KeyValue } from "@/ui";
import type { SectorNodeData } from "./worldViewModel";

/**
 * A selected sector's own loam economy (spec-loam-fe.md): what it earns, what it costs, what pool
 * it belongs to, and — the abandonment surface — whether the engine is about to release it outright
 * next turn, with the reason. Owner-only fields default to zero/false for anything not held, so
 * this renders identically (and harmlessly) for ground the viewer does not own.
 */
export function SectorPanel({ sector }: { sector: SectorNodeData }) {
  // Barren ground you own still draws upkeep and still pools with its component — hiding its
  // economy would hide exactly the ground most worth deciding to let go of. What is never shown is
  // another faction's economy: every field below is owner-gated to zero/false at the source.
  if (sector.ownership !== "mine") return null;

  return (
    <div className="space-y-2" data-testid="sector-economy">
      <KeyValue
        items={[
          { label: "Earns", value: String(sector.loamProduction) },
          { label: "Costs", value: String(sector.loamUpkeep) },
          {
            label: "Net",
            value: (
              <span className={sector.loamNet < 0 ? "font-semibold text-bad" : undefined}>
                {sector.loamNet > 0 ? "+" : ""}
                {sector.loamNet}
              </span>
            )
          },
          {
            label: "Supply",
            value:
              sector.componentNet < 0 ? (
                <span className="font-semibold text-bad" data-testid="sector-economy-supply-warning">
                  this territory can&apos;t cover its own keep
                </span>
              ) : (
                `${sector.componentStock} in store`
              )
          }
        ]}
      />

      {sector.willReleaseNextTurn ? (
        <div
          data-testid="sector-release-warning"
          className="rounded-sm border border-bad/60 bg-bad/10 px-2 py-1.5 text-xs font-semibold text-bad"
        >
          Losing ground next turn — its territory can&apos;t keep up.
        </div>
      ) : null}
    </div>
  );
}
