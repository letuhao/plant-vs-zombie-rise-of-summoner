import { HashRouter } from "react-router-dom";
import { Toasts } from "@/shell/Toasts";
import { AppProviders } from "./providers";
import { AppRoutes } from "./routes";

// Toasts live here, not inside AppShell — mutation feedback (e.g. creating a summoner on
// SaveSelect, outside AppShell) needs to reach the player on every route, not just AppShell ones.
export default function App() {
  return (
    <AppProviders>
      <HashRouter>
        <AppRoutes />
        <Toasts />
      </HashRouter>
    </AppProviders>
  );
}
