import { Navigate, Route, Routes } from "react-router-dom";
import { CatalogPage } from "@/features/catalog/CatalogPage";
import { DemonsPage } from "@/features/demons/DemonsPage";
import { ExpeditionsPage } from "@/features/expeditions/ExpeditionsPage";
import { FusionPage } from "@/features/fusion/FusionPage";
import { CheatsPage } from "@/features/cheats/CheatsPage";
import { AlmanacDumpPage } from "@/features/almanac-dump/AlmanacDumpPage";
import { IconDumpPage } from "@/features/icon-dump/IconDumpPage";
import { LawnPage } from "@/features/lawn/LawnPage";
import { RosterPage } from "@/features/roster/RosterPage";
import { LogPage } from "@/features/log/LogPage";
import { MetricsPage } from "@/features/metrics/MetricsPage";
import { PvzActivityPage } from "@/features/pvz-activity/PvzActivityPage";
import { RpgProgressionPage } from "@/features/rpg-progression/RpgProgressionPage";
import { PvzStatsPage } from "@/features/pvz-stats/PvzStatsPage";
import { RecipesPage } from "@/features/recipes/RecipesPage";
import { SimPage } from "@/features/sim/SimPage";
import { StatsPage } from "@/features/stats/StatsPage";
import { StatusPage } from "@/features/status/StatusPage";
import { StoragePage } from "@/features/storage/StoragePage";
import { WorldPage } from "@/features/world/WorldPage";
import { AppShell } from "./AppShell";

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/status" replace />} />
        <Route path="status" element={<StatusPage />} />
        <Route path="stats" element={<StatsPage />} />
        <Route path="pvz-stats" element={<PvzStatsPage />} />
        <Route path="pvz-activity" element={<PvzActivityPage />} />
        <Route path="rpg-progression" element={<RpgProgressionPage />} />
        <Route path="icon-dump" element={<IconDumpPage />} />
        <Route path="almanac-dump" element={<AlmanacDumpPage />} />
        <Route path="cheats" element={<CheatsPage />} />
        <Route path="types" element={<CatalogPage />} />
        <Route path="recipes" element={<RecipesPage />} />
        <Route path="log" element={<LogPage />} />
        <Route path="runs" element={<MetricsPage />} />
        <Route path="metrics" element={<Navigate to="/runs" replace />} />
        <Route path="storage" element={<StoragePage />} />
        <Route path="lawn" element={<LawnPage />} />
        <Route path="world" element={<WorldPage />} />
        <Route path="roster" element={<RosterPage />} />
        <Route path="demons" element={<DemonsPage />} />
        <Route path="expeditions" element={<ExpeditionsPage />} />
        <Route path="fusion" element={<FusionPage />} />
        <Route path="sim" element={<SimPage />} />
        <Route path="*" element={<Navigate to="/status" replace />} />
      </Route>
    </Routes>
  );
}
