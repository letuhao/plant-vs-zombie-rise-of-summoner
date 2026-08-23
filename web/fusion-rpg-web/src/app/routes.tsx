import { Navigate, Route, Routes } from "react-router-dom";
import { CatalogPage } from "@/features/catalog/CatalogPage";
import { DemonsPage } from "@/features/demons/DemonsPage";
import { ExpeditionsPage } from "@/features/expeditions/ExpeditionsPage";
import { FusionPage } from "@/features/fusion/FusionPage";
import { LawnStage } from "@/stages/lawn/LawnStage";
import { SanctumStage } from "@/stages/sanctum/SanctumStage";
import { ActorLadderDemoPage } from "@/ui/actor/ActorLadderDemoPage";
import { RpgProgressionPage } from "@/features/rpg-progression/RpgProgressionPage";
import { PvzStatsPage } from "@/features/pvz-stats/PvzStatsPage";
import { RecipesPage } from "@/features/recipes/RecipesPage";
import { StoragePage } from "@/features/storage/StoragePage";
import { WorldPage } from "@/features/world/WorldPage";
import { AppShell } from "./AppShell";

/** T12: these nine now live in the developer tree, reached via `` ` `` or `?dev=<id>` — never a route of their own. */
const DEV_ROUTE_REDIRECTS: Record<string, string> = {
  status: "status",
  stats: "stats",
  "pvz-activity": "pvz-activity",
  "icon-dump": "icon-dump",
  "almanac-dump": "almanac-dump",
  cheats: "cheats",
  sim: "sim",
  log: "log",
  runs: "runs"
};

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/sanctum" replace />} />
        <Route path="sanctum" element={<SanctumStage />} />
        {Object.entries(DEV_ROUTE_REDIRECTS).map(([routePath, devId]) => (
          <Route key={routePath} path={routePath} element={<Navigate to={`/sanctum?dev=${devId}`} replace />} />
        ))}
        <Route path="pvz-stats" element={<PvzStatsPage />} />
        <Route path="rpg-progression" element={<RpgProgressionPage />} />
        <Route path="types" element={<CatalogPage />} />
        <Route path="recipes" element={<RecipesPage />} />
        <Route path="metrics" element={<Navigate to="/sanctum?dev=runs" replace />} />
        <Route path="storage" element={<StoragePage />} />
        <Route path="lawn" element={<LawnStage />} />
        <Route path="actor-ladder-demo" element={<ActorLadderDemoPage />} />
        <Route path="world" element={<WorldPage />} />
        <Route path="roster" element={<Navigate to="/sanctum?panel=creatures" replace />} />
        <Route path="demons" element={<DemonsPage />} />
        <Route path="expeditions" element={<ExpeditionsPage />} />
        <Route path="fusion" element={<FusionPage />} />
        <Route path="*" element={<Navigate to="/sanctum" replace />} />
      </Route>
    </Routes>
  );
}
