import { useState } from "react";
import { Cell, PolarAngleAxis, RadialBar, RadialBarChart, ResponsiveContainer } from "recharts";
import type { ActorView } from "@/contract/types";
import type { ActorSurfaceCatalog, ResourceCatalogRow } from "@/lib/bus/actorSurface";
import { CatalogIcon } from "./CatalogIcon";
import { InspectSplit } from "./InspectSplit";
import { PendingNote } from "./shared";

type InspectTarget =
  | { kind: "resource"; row: ResourceCatalogRow }
  | { kind: "xp" }
  | { kind: "standing" };

/**
 * Condition glance — resource meters from catalog, honest pending for Standing / xpToNext /
 * live pools until ActorView carries them.
 */
export function ConditionTab({
  data,
  surface
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
}) {
  const [inspect, setInspect] = useState<InspectTarget>({ kind: "xp" });
  const hpRow = surface.resources.find((row) => row.id === "hp");

  return (
    <InspectSplit
      testId="condition-tab"
      list={
        <div className="space-y-4">
          <section aria-labelledby="condition-hp-title">
            <h3 id="condition-hp-title" className="font-display text-lg text-text">
              Vitality
            </h3>
            <div className="mt-2 flex items-center gap-4">
              <div className="h-28 w-28" data-testid="condition-hp-radial">
                <ResponsiveContainer width="100%" height="100%">
                  <RadialBarChart
                    innerRadius="60%"
                    outerRadius="100%"
                    data={[{ name: "hp", value: 0, fill: hpRow?.color ?? "#e74c3c" }]}
                    startAngle={90}
                    endAngle={-270}
                  >
                    <PolarAngleAxis type="number" domain={[0, 100]} tick={false} />
                    <RadialBar dataKey="value" background isAnimationActive={false}>
                      <Cell fill={hpRow?.color ?? "#e74c3c"} />
                    </RadialBar>
                  </RadialBarChart>
                </ResponsiveContainer>
              </div>
              <p className="text-xs italic text-muted">Current / max HP pending until the live pool adapter lands.</p>
            </div>
          </section>

          <section aria-labelledby="condition-resources-title">
            <h3 id="condition-resources-title" className="font-display text-lg text-text">
              Resources
            </h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {surface.resources.map((resource) => {
                const label = resource.labels[data.side];
                return (
                  <button
                    type="button"
                    key={resource.id}
                    data-testid={`condition-resource-${resource.id}`}
                    onClick={() => setInspect({ kind: "resource", row: resource })}
                    className="rounded-sm border border-border-control bg-panel-inset p-3 text-left"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="flex items-center gap-2 font-ui text-sm text-text">
                        <CatalogIcon icon={resource.icon} color={resource.color} fallbackToken={label.slice(0, 1)} />
                        {label}
                      </span>
                      <span className="text-2xs text-muted">Current / max pending</span>
                    </div>
                    <div
                      role="progressbar"
                      aria-label={`${label} meter`}
                      aria-valuetext="Current value pending"
                      className="mt-2 h-2 overflow-hidden rounded-pill bg-soil-raised shadow-inset"
                    >
                      <div className="h-full w-0" style={{ background: resource.color }} />
                    </div>
                  </button>
                );
              })}
            </div>
          </section>

          <section data-testid="condition-live-effects">
            <h3 className="font-display text-base text-text">Live effects</h3>
            {/* Spec: live instances ∩ catalog — never paint the catalog roster as if applied. */}
            <p className="mt-2 text-xs italic text-muted" data-testid="condition-live-effects-pending">
              Live effect instances are not available yet — glyph strip stays empty until the actor
              carries applied statuses.
            </p>
          </section>
        </div>
      }
      inspector={
        <>
          {inspect.kind === "xp" || inspect.kind === "standing" ? null : null}
          <section>
            <button type="button" className="text-left" onClick={() => setInspect({ kind: "xp" })}>
              <h3 className="font-display text-base text-text">Experience</h3>
            </button>
            <p className="text-sm text-text" data-testid="condition-xp-count">
              {data.xp.toLocaleString()} XP
            </p>
            <PendingNote pending={data.xpToNext} testId="condition-xp-pending" />
          </section>
          <section>
            <button type="button" className="text-left" onClick={() => setInspect({ kind: "standing" })}>
              <h3 className="font-display text-base text-text">Standing</h3>
            </button>
            <PendingNote pending={data.channelSummary} testId="actor-standing-pending" />
          </section>
          {inspect.kind === "resource" ? (
            <section data-testid="condition-inspect-resource">
              <h3 className="font-display text-base text-text">{inspect.row.labels[data.side]}</h3>
              <p className="text-xs text-muted">Live pool values are pending — meter slot reserved from catalog.</p>
            </section>
          ) : null}
        </>
      }
    />
  );
}
