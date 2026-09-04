import { memo, useEffect, useRef } from "react";

/**
 * A force sliding along a lane during turn playback.
 *
 * The animation writes `transform` straight onto its own `<g>` through a ref inside a
 * `requestAnimationFrame` loop — it never calls `setState`, so a legion crossing the map costs the
 * React tree exactly **zero** re-renders. Position comes from the lane's own `<path>` via
 * `getPointAtLength`, so the marker follows whatever curve the edge is actually drawn with rather
 * than a straight line the renderer only pretends is the lane.
 */

/** Module-scope so the default is a stable identity, not a fresh closure on every render. */
const wallClock = () => performance.now();

export type LegionMarkerProps = {
  /** The `<path>` element id React Flow gives this lane's edge. */
  pathId: string;
  fromMilli: number;
  toMilli: number;
  durationMs: number;
  color: string;
  entityId: string;
  /** Injected in tests; the browser's clock otherwise. */
  now?: () => number;
};

function LegionMarkerView({
  pathId,
  fromMilli,
  toMilli,
  durationMs,
  color,
  entityId,
  now = wallClock
}: LegionMarkerProps) {
  const group = useRef<SVGGElement | null>(null);

  // The clock is read through a ref rather than depended on. It is *how* we tell the time, not part
  // of the march — and treating it as a dependency restarts the animation from the start of the lane
  // on any re-render that gets past `memo`, which reads as a legion stuttering backwards.
  const clock = useRef(now);
  clock.current = now;

  useEffect(() => {
    const path = document.getElementById(pathId) as SVGPathElement | null;
    const node = group.current;
    if (!path || !node || typeof path.getPointAtLength !== "function") return;

    const total = path.getTotalLength();
    const started = clock.current();
    let frame = 0;

    const place = (milli: number) => {
      const point = path.getPointAtLength((total * Math.min(1000, Math.max(0, milli))) / 1000);
      node.setAttribute("transform", `translate(${point.x}, ${point.y})`);
    };

    const tick = () => {
      const elapsed = durationMs <= 0 ? 1 : (clock.current() - started) / durationMs;
      const t = Math.min(1, Math.max(0, elapsed));
      place(fromMilli + (toMilli - fromMilli) * t);
      if (t < 1) frame = requestAnimationFrame(tick);
    };

    place(fromMilli);
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [pathId, fromMilli, toMilli, durationMs]);

  return (
    <g ref={group} data-testid={`legion-marker-${entityId}`}>
      <circle r={6} fill={color} stroke="var(--color-ink-dark)" strokeWidth={1.5} />
    </g>
  );
}

export const LegionMarker = memo(LegionMarkerView);
LegionMarker.displayName = "LegionMarker";
