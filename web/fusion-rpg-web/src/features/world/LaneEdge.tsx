import { memo } from "react";
import { BaseEdge, getStraightPath, type EdgeProps } from "@xyflow/react";
import { LegionMarker } from "./LegionMarker";
import type { LaneEdgeData } from "./worldViewModel";

/**
 * One lane. Type decides the colour, width decides the stroke — a chokepoint should look like one
 * before anyone reads the tooltip. Memoized for the same reason the sector card is.
 */

/**
 * `world-stage` W49: raw hex retired in favour of the shipped token set — each lane kind maps to
 * the closest existing semantic token by hue/role rather than a bespoke new one (this tree is the
 * pre-`stages/world` view, not a candidate for growing the kit). Corridor reads as the safe/normal
 * pass (`--color-ok`); rift and ley are the two "otherworldly" kinds, mapped to the cool-toned
 * elemental tokens (`--color-el-air`, `--color-el-dark`); deep uses the darkest neutral
 * (`--color-faint`); one-way and gated reuse the existing info/warn tokens.
 */
const laneStroke: Record<string, string> = {
  corridor: "var(--color-ok)",
  rift: "var(--color-el-air)",
  ley: "var(--color-el-dark)",
  deep: "var(--color-faint)",
  "one-way": "var(--color-info)",
  gated: "var(--color-warn)"
};

/** How long a force takes to slide from where it was to where the turn left it. */
export const MARCH_DURATION_MS = 900;

/** Lane width is per-mille of a full front; 1000 draws at 4px, a 600-wide pass at ~2.4px. */
export function strokeWidthFor(width: number): number {
  return Math.max(1.2, Math.min(6, (width / 1000) * 4));
}

export type LaneEdgeProps = EdgeProps & { data?: LaneEdgeData };

function LaneEdgeView({ id, sourceX, sourceY, targetX, targetY, data }: LaneEdgeProps) {
  const [path] = getStraightPath({ sourceX, sourceY, targetX, targetY });
  const severed = data?.severed ?? false;

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        style={{
          stroke: severed ? "var(--color-bad)" : laneStroke[data?.typeId ?? "rift"] ?? laneStroke.rift,
          strokeWidth: strokeWidthFor(data?.width ?? 1000),
          strokeDasharray: severed ? "6 5" : undefined,
          opacity: severed ? 0.6 : 0.9
        }}
      />
      {(data?.forces ?? []).map((force) => (
        // `alongMilli` is already measured from this lane's source end — see the fold. The marker
        // reads the drawn path itself, so it follows the lane rather than a straight line through it.
        <LegionMarker
          key={force.entityId}
          pathId={id}
          entityId={force.entityId}
          fromMilli={force.fromMilli ?? force.alongMilli}
          toMilli={force.alongMilli}
          durationMs={MARCH_DURATION_MS}
          color={force.ownership === "mine" ? "var(--color-side-plant)" : "var(--color-bad)"}
        />
      ))}
    </>
  );
}

export const LaneEdge = memo(LaneEdgeView);
LaneEdge.displayName = "LaneEdge";
