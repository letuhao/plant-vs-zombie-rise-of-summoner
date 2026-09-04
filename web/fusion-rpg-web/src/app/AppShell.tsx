import { Outlet, useLocation } from "react-router-dom";
import { useHealth, useHubStatus, usePlayers } from "@/lib/bus";
import { DevTreeHost } from "@/dev/DevTreeHost";
import { SystemHost } from "@/layers/system/SystemHost";
import { useGlobalKeys } from "@/shell/useGlobalKeys";
import { Banner } from "@/ui";

/**
 * world-stage W34: routes whose stage is measured against the viewport, not the page — a stage's
 * own camera owns its extent, so the outlet must never grow past the viewport and hand the page a
 * scrollbar (GG-36 forbids exactly that dressed as a feature). Route-scoped on purpose: this is an
 * opt-in lookup, not a blanket AppShell layout change, so Sanctum and Lawn — neither of which is in
 * this set — render byte-identically to before.
 */
const NON_SCROLLING_ROUTES = new Set(["/world-stage"]);

export function AppShell() {
  useGlobalKeys();
  const health = useHealth();
  const players = usePlayers();
  const hub = useHubStatus();
  const location = useLocation();

  const apiErr = health.error?.message || players.error?.message;
  const hubWarn = hub === "err" ? "SignalR disconnected — falling back to poll" : null;
  const nonScrolling = NON_SCROLLING_ROUTES.has(location.pathname);

  return (
    <div className="flex h-screen flex-col" data-testid="app-shell">
      {apiErr ? (
        <Banner tone="error" data-testid="banner-api-error">
          Server unreachable: {apiErr}
        </Banner>
      ) : null}
      {!apiErr && hubWarn ? (
        <Banner tone="warn" data-testid="banner-hub-warn">
          {hubWarn}
        </Banner>
      ) : null}
      <div className="flex min-h-0 flex-1" data-testid="shell-body">
        <main
          className={nonScrolling ? "min-w-0 flex-1 overflow-hidden" : "min-w-0 flex-1 overflow-auto p-5"}
          data-testid="page-outlet"
        >
          <Outlet />
        </main>
      </div>
      <DevTreeHost />
      <SystemHost />
    </div>
  );
}
