import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import { cn } from "@/lib/cn";
import type { SectorNodeData } from "./worldViewModel";

/**
 * One sector card. Memoized, and it reads nothing but its own `data` — panning and zooming change
 * the canvas transform, not this, so the cards do not re-render while the map is dragged.
 */

const ownershipRing: Record<string, string> = {
  mine: "border-emerald-400/70",
  enemy: "border-rose-500/70",
  neutral: "border-border"
};

const climateAccent: Record<string, string> = {
  Fire: "bg-orange-500",
  Ice: "bg-sky-400",
  Earth: "bg-amber-600",
  Dark: "bg-violet-500",
  Light: "bg-yellow-300",
  Water: "bg-blue-500",
  Wind: "bg-teal-400",
  Thunder: "bg-indigo-400"
};

/** Slot glyphs: one letter is enough to tell a Seat from a vein at map zoom. */
const slotGlyph: Record<string, string> = {
  seat: "S",
  wildland: "·",
  "essence-deposit": "E",
  "material-seam": "M",
  "shard-vein": "V",
  lair: "L",
  spire: "T",
  hazard: "!",
  market: "$"
};

export type SectorNodeProps = NodeProps & { data: SectorNodeData; showLifelines?: boolean };

/**
 * Territory is light in the dark (spec-loam-fe.md). `fading`'s dimming is continuous, in
 * proportion to `stabilityMilli` — `barren`'s is not: it is a flat, distinct look regardless of the
 * number, because barren ground can never be kept no matter what that number says this turn, and a
 * player who reads it as "just another shade of fading" will make the wrong call every time.
 */
function anchorOpacity(anchorState: SectorNodeData["anchorState"], stabilityMilli: number): number {
  if (anchorState !== "fading") return 1;
  return 0.35 + 0.65 * Math.max(0, Math.min(1000, stabilityMilli)) / 1000;
}

const anchorStatusText: Record<SectorNodeData["anchorState"], string | null> = {
  anchored: null, // silence is the healthy state — nothing to say
  fading: "fading",
  barren: "cannot be kept",
  "not-yours": null
};

function SectorNodeView({ data, selected, showLifelines }: SectorNodeProps) {
  const shrouded = data.unknown;

  // A remembered card is dimmed and stamped with its age. Hiding *when* you last looked would not
  // create tension, it would create note-taking — the interesting uncertainty is what changed.
  const stale = data.remembered && data.age > 0;
  const anchorStatus = anchorStatusText[data.anchorState];

  return (
    <div
      data-testid={`sector-node-${data.sectorId}`}
      data-ownership={data.ownership}
      data-anchor-state={data.anchorState}
      style={{ opacity: shrouded ? undefined : anchorOpacity(data.anchorState, data.stabilityMilli) }}
      className={cn(
        "w-[168px] rounded-lg border-2 bg-soil-raised/95 px-3 py-2 text-left shadow-md transition-shadow",
        ownershipRing[data.ownership] ?? ownershipRing.neutral,
        shrouded && "opacity-45 border-dashed",
        stale && "opacity-75",
        // Barren: a flat, desaturated look distinct from fading's continuous dim — never the same
        // visual language as a problem the player could still solve.
        data.anchorState === "barren" && "grayscale border-stone-600/70 bg-stone-900/60",
        showLifelines && data.lifeline && "!border-amber-400 shadow-[0_0_12px] shadow-amber-400/40",
        selected && "shadow-[0_0_0_3px] shadow-sky-400/60"
      )}
    >
      {/* Lanes attach to the card edges; connecting by hand is off, so the handles are invisible. */}
      <Handle type="target" position={Position.Left} className="!h-1 !w-1 !border-0 !bg-transparent" />
      <Handle type="source" position={Position.Right} className="!h-1 !w-1 !border-0 !bg-transparent" />

      <div className="flex items-center gap-2">
        <span
          data-testid="sector-climate"
          className={cn("h-2.5 w-2.5 shrink-0 rounded-full", data.climate ? climateAccent[data.climate] ?? "bg-muted" : "bg-slate-500")}
          title={data.climate ?? "no climate"}
        />
        <span className="truncate font-display text-sm text-text">{shrouded ? "?" : data.label}</span>
      </div>

      <div className="mt-0.5 flex items-center justify-between text-[11px] text-muted">
        <span data-testid="sector-status">
          {shrouded ? "unscouted" : stale ? `seen ${data.age} turn${data.age === 1 ? "" : "s"} ago` : data.phase.toLowerCase()}
        </span>
        <span title="danger band">{"◆".repeat(Math.max(0, Math.min(5, data.dangerBand))) || "—"}</span>
      </div>

      {anchorStatus ? (
        <div
          data-testid="sector-anchor-status"
          className={cn(
            "mt-0.5 text-[10px] font-semibold uppercase tracking-wide",
            data.anchorState === "barren" ? "text-stone-400" : "text-amber-300"
          )}
        >
          {anchorStatus}
        </div>
      ) : null}

      {showLifelines && data.lifelineCost > 0 ? (
        <div
          className="mt-1 text-[10px] font-semibold uppercase tracking-wide text-amber-300"
          data-testid="sector-lifeline"
          title="what losing this would cost your empire"
        >
          {data.lifeline ? "lifeline" : "route"}
        </div>
      ) : null}

      <div className="mt-1.5 flex flex-wrap gap-1" data-testid="sector-slots">
        {data.slots.map((slot) => (
          <span
            key={slot.slotIndex}
            data-testid={`slot-${slot.slotIndex}`}
            data-guard={slot.guard}
            title={`${slot.slotTypeId}${slot.guard === "intact" ? " — guarded" : ""}`}
            className={cn(
              "inline-flex h-5 w-5 items-center justify-center rounded border text-[10px] font-semibold",
              slot.guard === "intact"
                ? "border-rose-500/70 bg-rose-500/15 text-rose-200"
                : "border-border bg-soil/60 text-muted"
            )}
          >
            {slotGlyph[slot.slotTypeId] ?? "?"}
          </span>
        ))}
      </div>

      {data.forces.length > 0 ? (
        <div className="mt-1.5 flex flex-wrap gap-1" data-testid="sector-forces">
          {data.forces.map((force) => (
            <span
              key={force.entityId}
              data-testid={`force-${force.entityId}`}
              title={
                force.exact
                  ? `${force.entityId} — ${force.strength} hp${force.routed ? ", routed" : ""}`
                  : `${force.entityId} — ${force.bandName} (not counted)`
              }
              className={cn(
                "rounded px-1.5 py-0.5 text-[10px] font-semibold",
                force.ownership === "mine" ? "bg-emerald-500/25 text-emerald-200" : "bg-rose-500/25 text-rose-200",
                force.routed && "line-through opacity-70"
              )}
            >
              {/* A band where the intel is banded: a number here would imply a count nobody made. */}
              {force.exact ? force.strength : force.bandName}
            </span>
          ))}
        </div>
      ) : null}

      {data.claimable ? (
        <div className="mt-1.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-300" data-testid="sector-claimable">
          claimable
        </div>
      ) : null}
    </div>
  );
}

export const SectorNode = memo(SectorNodeView);
SectorNode.displayName = "SectorNode";
