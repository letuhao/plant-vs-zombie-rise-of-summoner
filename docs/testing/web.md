# Web UI test coverage

Vite + React app in `web/fusion-rpg-web`.

## Unit / component (Vitest)

```powershell
cd web/fusion-rpg-web
npm test
npm run test:coverage
```

Covers:

| Area | Assert |
|---|---|
| `keysForEventKind` | board/match/wave → runs+metrics+health; plant/zombie/mower → types+sim+metrics; mix/recipe → recipes; unknown → [] |
| log-store | append newest-first, batch reverse, cap 800, clear, subscribe |
| rest | getJson / tryGetJson 404 / sendJson body |
| mutations | saveStats PUT+reload; players create/select; sim + reset clears log; **cheat** save/toggle/set-float/action |
| ui / layouts | Button, Banner, Badge, DataTable click/empty, Page, Split, Field inputs |
| feature pages | Status health rows; Types/Recipes tables; Stats save; Runs KPI + spawn dump; **Cheats tabs + probe packs panel + reset-all** |

Coverage thresholds (v8) on `src/lib/bus`, `src/ui`, `src/layouts`, `src/lib/cn.ts`: lines/functions/statements ≥ 70%, branches ≥ 60%. Report in `web/fusion-rpg-web/coverage/`. Hub connection files are excluded (SignalR runtime).

## E2E (Playwright)

Uses the **production build** (`vite preview` on `:4173`) with REST mocked and SignalR aborted (hub shows fallback warn; queries still load via REST).

```powershell
cd web/fusion-rpg-web
npm run build
npx playwright install chromium   # once
npm run test:e2e
```

Or all together:

```powershell
npm run test:all
```

E2E asserts:

- Shell HudBar + Audit nav across Status / Stats / **Cheats** / Types / Recipes / Log / Runs / **Progression**
- Create player POST from HudBar
- Save stats → PUT `/api/stats` + POST reload-stats
- Cheats page tabs + mock `/api/cheats*`
- Run row opens KPI recap + spawn dump JSON
- **Progression** Almanac: Overview KPIs/charts, Plants/Zombies dossier, Ledger filters/pager

## Relation to server CI

`dotnet test` (Core + E2E.Tests) remains the server/sim pipe. Web Vitest/Playwright are Node developer/CI steps for the SPA. They do not replace `FusionRpg.E2E.Tests`.
