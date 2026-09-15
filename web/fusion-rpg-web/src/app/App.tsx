import { HashRouter } from "react-router-dom";
import { useActorSurfaceCatalog } from "@/lib/bus";
import { Toasts } from "@/shell/Toasts";
import { OverlayLeave } from "@/shell/OverlayLeave";
import { FirstOpenSignal } from "@/shell/FirstOpenSignal";
import { AppProviders } from "./providers";
import { AppRoutes } from "./routes";

// Toasts live here, not inside AppShell — mutation feedback (e.g. creating a summoner on
// SaveSelect, outside AppShell) needs to reach the player on every route, not just AppShell ones.
//
// OverlayLeave is here for the same reason (rift-gate overlay-hide): it must be reachable wherever
// the player is, including the band -1 TitleScreen, which is deliberately outside AppShell. It
// renders nothing at all unless the host marked the visit as embedded (decision 16).
//
// FirstOpenSignal is here too (rift-gate first-open-signal): the durable "the FE has been opened" fact
// is about the FE opening, not about any route, so it belongs at the root and fires once per load.
export default function App() {
  return (
    <AppProviders>
      <ActorSurfaceCatalogBootstrap />
      <FirstOpenSignal />
      <HashRouter>
        <AppRoutes />
        <Toasts />
        <OverlayLeave />
      </HashRouter>
    </AppProviders>
  );
}

function ActorSurfaceCatalogBootstrap() {
  useActorSurfaceCatalog();
  return null;
}
