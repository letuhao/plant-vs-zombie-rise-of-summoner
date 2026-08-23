import { useEffect, useState } from "react";
import { AlmanacDumpPage } from "@/features/almanac-dump/AlmanacDumpPage";
import { CheatsPage } from "@/features/cheats/CheatsPage";
import { IconDumpPage } from "@/features/icon-dump/IconDumpPage";
import { LogPage } from "@/features/log/LogPage";
import { MetricsPage } from "@/features/metrics/MetricsPage";
import { PvzActivityPage } from "@/features/pvz-activity/PvzActivityPage";
import { SimPage } from "@/features/sim/SimPage";
import { StatsPage } from "@/features/stats/StatsPage";
import { StatusPage } from "@/features/status/StatusPage";
import { PanelShell } from "@/shell/PanelShell";

export const DEV_SURFACES = [
  { id: "status", label: "Status", Component: StatusPage },
  { id: "stats", label: "Stats", Component: StatsPage },
  { id: "pvz-activity", label: "PvzActivity", Component: PvzActivityPage },
  { id: "icon-dump", label: "IconDump", Component: IconDumpPage },
  { id: "almanac-dump", label: "AlmanacText", Component: AlmanacDumpPage },
  { id: "cheats", label: "Cheats", Component: CheatsPage },
  { id: "sim", label: "Sim", Component: SimPage },
  { id: "log", label: "Log", Component: LogPage },
  { id: "runs", label: "Runs", Component: MetricsPage }
] as const;

export type DevSurfaceId = (typeof DEV_SURFACES)[number]["id"];

/**
 * The gated tree (T12, GG-40–42): density beats polish, engine vocabulary
 * is correct here (the nine pages inside are unchanged from v1 — this only
 * changes how they're *reached*), but it still obeys the stack, Esc, focus
 * and volume rules like any other band-2 layer — it does not get its own
 * bespoke overlay mechanism.
 */
export function DeveloperTree({
  open,
  onOpenChange,
  initialTab
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialTab?: DevSurfaceId;
}) {
  const [tab, setTab] = useState<DevSurfaceId>(initialTab ?? "status");

  // `initialTab` names the surface a deep link (`?dev=<id>`, an old-route redirect) wants
  // open, but this component is mounted once for the app's lifetime — the `useState`
  // initializer above only fires on that first mount. Every later deep link (SPA nav
  // between two old routes, or a fresh load where the router hasn't resolved the query on
  // the very first render) must also resync the visible tab, not just the very first one.
  useEffect(() => {
    if (initialTab) setTab(initialTab);
  }, [initialTab]);

  const active = DEV_SURFACES.find((s) => s.id === tab) ?? DEV_SURFACES[0];

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title="Developer"
      subtitle="Off by default — never part of player navigation"
      testId="dev-tree"
      footer={
        <div className="flex flex-wrap gap-1" data-testid="dev-tree-tabs">
          {DEV_SURFACES.map((s) => (
            <button
              key={s.id}
              type="button"
              data-testid={`dev-tree-tab-${s.id}`}
              onClick={() => setTab(s.id)}
              aria-current={s.id === tab}
              className={`rounded-sm border px-2 py-1 text-xs ${
                s.id === tab ? "border-lawn-hot bg-lawn text-text" : "border-border text-muted hover:bg-panel"
              }`}
            >
              {s.label}
            </button>
          ))}
        </div>
      }
    >
      <div data-testid={`dev-tree-surface-${active!.id}`}>
        <active.Component />
      </div>
    </PanelShell>
  );
}
