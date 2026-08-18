# Web spec (v1)

Vite + React + TypeScript in `web/fusion-rpg-web`.

**Players:** `npm run build` output is copied to `FusionRpg.Server/wwwroot`. The server hosts it at `http://127.0.0.1:5088`. The UI uses the **same origin** (relative `/api` and `/hub/rpg`). No Node on the player PC.

**Developers:** `npm run dev` on port 5173 (API `http://127.0.0.1:5088` in DEV).

Hand-written types matching [protocol/rest.md](../protocol/rest.md). No OpenAPI generator in v1.

## Architecture

Four layers. Feature screens do not call `fetch` or SignalR directly.

| Layer | Location | Role |
|---|---|---|
| Foundation | `src/app/`, `src/layouts/` | Providers (Query, Hub, ErrorBoundary), HashRouter (`#/status`), AppShell |
| Theme | `src/theme/tokens.css` | Lawn Almanac CSS variables; Tailwind `@theme` maps them |
| Shared kit | `src/ui/` | Buttons, Panel, tables, log, KPIs — no feature names |
| Data bus | `src/lib/bus/` | REST snapshots + SignalR live + query keys + mutations + event ring |
| Game island | `src/game/` | Phaser-only Dual-Plane Lawn Projector (scenes, registry, systems, FX) — **W6 monitor shipped**; SSOT [architecture/fe-game-foundation.md](../architecture/fe-game-foundation.md) |

```text
src/
  app/           AppShell, HudBar, AuditNav, routes, providers
  theme/         tokens.css, fonts.css
  layouts/       Page, Split
  ui/            shared primitives
  lib/bus/       types, rest, keys, queries, mutations, hub, log-store
  features/      status, stats, catalog, recipes, log, metrics, sim, …, lawn (host + fold)
  game/          Phaser island — EventBus, scenes, entities, systems, fx
```

## Theme (Lawn Almanac)

Tokens live only in `theme/tokens.css`. Components use Tailwind keys / `var(--*)` — no raw hex in feature CSS.

| Token | Value | Use |
|---|---|---|
| `--color-soil` | `#16120e` | App background |
| `--color-soil-raised` | `#221c16` | HudBar |
| `--color-panel` | `#2a231b` | Panel fill |
| `--color-panel-inset` | `#120f0c` | Log / JSON wells |
| `--color-almanac` | `#e4d5b5` | Paper accent |
| `--color-lawn` / `--color-lawn-hot` | `#3d6b45` / `#5a8f62` | Primary / active |
| `--color-sun` | `#e0b44b` | KPI accent, focus |
| `--color-zombie` | `#6e5a7a` | Zombie badge |
| `--color-text` / `--color-muted` | `#f2ead8` / `#a89880` | Body / secondary |
| `--color-ok` / `--color-bad` / `--color-warn` | greens/reds/amber | Status |
| `--font-display` | Lilita One | Titles |
| `--font-ui` | Nunito | Body / forms |
| `--font-mono` | Consolas | Log / JSON |

Space is a 4px grid (`--space-1`…`8`, `--space-10`). Radius `sm`/`md`/`pill`. Motion 120ms; reduced-motion disables transitions.

## Shared UI kit (audit)

Layout: `AppShell`, `HudBar`, `AuditNav`, `Page`, `Split`, `Row`, `Grid`.

Controls: `Button`, `TextInput`, `NumberInput`, `Select`, `Checkbox`, `Field`.

Feedback: `Banner`, `Badge`, `StatusDot`, `EmptyState`, `HelpText`.

Data: `Panel`, `KeyValue`, `KpiStat`, `StatBar`, `DataTable`, `JsonBlock`, `LogStream`.

## Data bus

- **REST** = snapshot truth (`useHealth`, `usePlayers`, `useStats`, `useTypes`, `useRecipes`, `useMetrics`, `useRuns`, `useRunSpawns`, `useSimState`).
- **SignalR** = live bus (`HubProvider`, `Join("web")`). `Health` updates query cache; `Event`/`EventBatch` append to an 800-event ring and invalidate by `kind`.
- **Hub connected:** no 3s poll-everything. **Hub down:** poll health 5s, lists 10s.
- Mutations: save stats (+ reload-stats), create/select player, sim commands, reset.

## Screens (HashRouter)

1. **Status** (`#/status`) — health, injector, source, ingest, last `board.economy`.
2. **Stats** (`#/stats`) — plant/zombie HP/ATK/DEF percent + flat, toggles; save + push.
3. **Types** (`#/types`) — catalog; reference HP only, not combat SSOT.
4. **Recipes** (`#/recipes`) — parentA + parentB → result.
5. **Live log** (`#/log`) — SignalR events; filter, pause, clear.
6. **Runs** (`#/runs`) — metrics + runs; KPI row + spawn-dump JSON inspector.
7. **Simulator** (`#/sim`) — only if `GET /api/sim/state` is not 404.
8. **PvzStats** (`#/pvz-stats`) — player modifier sheet + source drill-down.
9. **PvzActivity** (`#/pvz-activity`) — rollups + facts timeline.
10. **Progression** (`#/rpg-progression`) — Almanac dossier: Overview KPIs/charts, Plants/Zombies Split dossiers, paged Ledger.
11. **Lawn** (`#/lawn`) — **Shipped (W6 monitor + W7 Intent/debug).** Phaser 4 Dual-Plane Lawn Projector: event-fold `LawnViewModel`, canvas + inspector with Intent/debug enqueue and Bound Cold observe. Projection: [architecture/lawn-projector.md](../architecture/lawn-projector.md). Runtime: [architecture/fe-game-foundation.md](../architecture/fe-game-foundation.md) (DPLP). Observe MatchSnapshot/events; mutations via `lib/bus` only — never Hot Admit. Living set never from Activity rollups.
12. **Roster** (`#/roster`) — **Shipped (W8).** UniqueActor Cold list/create/equip/deploy/retire + specimen XP award. Full gear shop polish = W12.

## Phaser lawn island (design)

- Pin **Phaser 4** (`phaser@^4.2`) — no Phaser 3 fallback.
- React hosts canvas under `features/lawn/`; Phaser code under `src/game/` (no React imports; no fetch/SignalR in scenes).
- Pure fold `events → LawnViewModel` unit-tested without Phaser; EventBus generation-scoped bridge.
- Runtime SSOT: [architecture/fe-game-foundation.md](../architecture/fe-game-foundation.md). Projection model: [architecture/lawn-projector.md](../architecture/lawn-projector.md). Plane rules: [overlay-control-loops.md](../architecture/overlay-control-loops.md).

`GET /api/players`. Select = `PUT /api/players/current`. Create = `POST /api/players`. Switching mid-match does not rewrite the open run.

## UX

Dark Lawn Almanac audit UI. No in-game overlay control besides injector status.

If API is down, show a banner; keep last stats form values.

## Out of scope v1

Auth, per-type **stat editors**, i18n, player XP/inventory fields, lawn **gear shop polish** (W12 — roster equip + specimen XP shipped W8).

**Charts:** Progression Overview uses **Recharts** via shared `BarChart` / `Sparkline` wrappers (Lawn Almanac colors).

## Tests

See [testing/web.md](../testing/web.md). Vitest + coverage on bus/ui/layouts; Playwright e2e against `vite preview` with mocked REST.
