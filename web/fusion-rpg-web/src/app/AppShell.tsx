import { Outlet } from "react-router-dom";
import { useHealth, useHubStatus, usePlayers } from "@/lib/bus";
import { DevTreeHost } from "@/dev/DevTreeHost";
import { useGlobalKeys } from "@/shell/useGlobalKeys";
import { Toasts } from "@/shell/Toasts";
import { Banner } from "@/ui";
import { AuditNav } from "./AuditNav";
import { HudBar } from "./HudBar";

export function AppShell() {
  useGlobalKeys();
  const health = useHealth();
  const players = usePlayers();
  const hub = useHubStatus();

  const apiErr = health.error?.message || players.error?.message;
  const hubWarn = hub === "err" ? "SignalR disconnected — falling back to poll" : null;

  return (
    <div className="flex min-h-screen flex-col" data-testid="app-shell">
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
      <HudBar />
      <div className="flex min-h-0 flex-1" data-testid="shell-body">
        <AuditNav />
        <main className="min-w-0 flex-1 overflow-auto p-5" data-testid="page-outlet">
          <Outlet />
        </main>
      </div>
      <Toasts />
      <DevTreeHost />
    </div>
  );
}
