# Local development (people who change code)

Players do not follow this page. See [players.md](players.md).

## 1. RPG server

```powershell
cd src\FusionRpg.Server
dotnet run
```

`http://127.0.0.1:5088/health` should return JSON.  
If `wwwroot/index.html` exists, the UI is also at `http://127.0.0.1:5088`.

SQLite: `bin/.../data/rpg-hot.sqlite` + `rpg-media.sqlite` (next to the running server).

## 2. Web UI while editing

```powershell
cd web\fusion-rpg-web
npm install
npm run dev
```

Hot reload: `http://127.0.0.1:5173`.

Build static files into the server (what players get):

```powershell
npm run build
```

`vite.config.ts` writes to `src/FusionRpg.Server/wwwroot`.

## 3. Injector

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder with BepInEx\core and BepInEx\interop>"
dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release
# or: .\scripts\deploy-play.ps1
```

MelonLoader twin (optional):

```powershell
$env:FUSIONRPG_ML_GAMEDIR = "<Blooms-style MelonLoader pack>"  # e.g. Blooms 3.8.1 Game Files
.\scripts\deploy-play.ps1 -LoaderHost MelonLoader
# After a level lawn is open:
.\scripts\smoke-melon-live.ps1
```

LIVE Pass/Fail: [`melon-live-checklist.md`](melon-live-checklist.md). Leave `FUSIONRPG_SIM` and `FUSIONRPG_MELON_SKIP_HARMONY` unset.

Shared sources live in `src/FusionRpg.Injector/` (RpgHost facade — no BepInEx/Melon usings in hooks).  
Launch `PlantsVsZombiesRH.exe`. Config: `BepInEx/config/com.fusionrpg.injector.cfg` or `Mods/fusionrpg.cfg`. Env `FUSIONRPG_SERVER_URL` wins when set by the launcher.

## 4. Player zip (self-contained launcher + server)

From the repo root (needs .NET SDK + Node on the **dev** PC only):

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder>"
.\scripts\publish-player.ps1
```
Output: `dist/FusionRpg/FusionRpg.Launcher.exe` + `dist/FusionRpg/Server/` + `DropIntoGame/`.  
Players unzip and double-click the launcher — no SDK, no Desktop Runtime, no Node.

## 5. Simulator and tests (no real game)

See [simulator.md](simulator.md). `dotnet run` sets `FUSIONRPG_SIM=1` via launchSettings.

```powershell
dotnet test
```

Web UI tests (Vitest coverage + Playwright e2e):

```powershell
cd ..\..\web\fusion-rpg-web
npm run test:all
```

See [testing/web.md](../testing/web.md).

Launcher unit tests:

```powershell
dotnet test tests\FusionRpg.Launcher.Tests
```

## 6. Fast deploy (real game + injector)

One command from the repo root: build the web UI, build the injector into the game's mod folder, start the RPG server **without** the simulator, launch `PlantsVsZombiesRH.exe`.

```powershell
.\scripts\deploy-play.ps1
```

Default host is MelonLoader (2026-08-30, `H:\Games\PVZ-Fusion-3.9_MelonLoader` on this machine — faster
startup than the older BepInEx install), building into that game's `Mods\` folder. Pass
`-LoaderHost BepInEx` for the older `BepInEx\plugins\FusionRpg\` install instead.

The web UI build runs by default (2026-08-30 — it used to be opt-in via `-RebuildUi` and got forgotten,
leaving a stale FE served for a whole session). Flags: `-LoaderHost` (`MelonLoader`/`BepInEx`), `-NoGame`,
`-NoServer`, `-NoRebuildUi` (skip the web UI build), `-RestartServer`, `-QuickTest`.

**`-QuickTest` (2026-09-14).** The slowest step by far is the default test profile (`test-fast.ps1`,
13k+ tests) — a real cost when a redeploy follows a small, already-hand-verified edit. `-QuickTest`
skips only that step; every boundary guard still runs, and a loud warning prints every time it's used.
**Never the default, never proof of anything** — re-run without it (or the targeted `dotnet test`
filters for what you actually touched) before calling a build verified, before a live proof, and
before commit/merge. It exists to shorten the local iteration loop, not to replace the gate.

**FE always lands in `dist` (2026-09-09):** Vite writes `src/FusionRpg.Server/wwwroot`; the published
server serves `dist/FusionRpg.Server/wwwroot` (`ContentRoot` = exe dir). If `:5088` is already up,
`dotnet publish` is skipped (DLL locks) — the script still **mirrors** src wwwroot → dist wwwroot, so
`.\scripts\deploy-play.ps1 -NoGame -NoServer` is enough to confirm UI fixes after a hard-refresh.
Pass `-RestartServer` when you also need a fresh server binary.

SQLite for this session: `dist/FusionRpg.Server/data/rpg-hot.sqlite` + `rpg-media.sqlite` (beside the published exe; gitignored). Icons/almanac are BLOBs in the media file.

Do not use the Simulator tab in the same session as the real injector.

## 7. Live-probe tool (`tools/ProveLiveProbe`)

The real 6-step live-probe recipe (spec: `docs/architecture/live-probe/spec-live-probe-tool.md`) —
acquire, allocate, equip, deploy, persisted-state read-back, and (Mode B only) a live-engine read —
run as real HTTP against a running `FusionRpg.Server`, never a fabricated actor and never an
`ok:true` response taken as proof on its own.

```powershell
# Mode A -- persisted-state only (steps 1-5): Server up, no game/Injector needed
.\scripts\prove-live-probe.ps1 -Mode A -PlayerId 1 -Side plant -TypeId <id> `
    -AptitudeId Might -AptitudePoints 30 -Role <slot> -ItemInstanceId <owned-item-id>

# Mode B -- full 6-step proof: real summon + live match/board + Injector connected required.
# Cold-start the lawn first via the `live-lawn-quick-start` skill (enter level 1, lab-overlay,
# target ptr) -- Mode B refuses outright if step 1 is given the Mode-A-only debug shortcut, since
# that shortcut's synthetic ptr never exists on a real board.
.\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId <banner-id> `
    -AptitudeId Might -AptitudePoints 30 -Role <slot> -ItemInstanceId <owned-item-id> -TimeoutSec 30
```

Exits 0 only when persisted state and (Mode B) the live engine both agree; a non-zero exit always
names which of the two halves failed, and whether it was a real server refusal, a value mismatch, or
a poll timeout — never one merged pass/fail boolean.

## End-to-end check (real game)

1. Start server (and Vite only if you are editing UI).
2. Set plant `hpPercent` to `2`, Save, Push.
3. Start a level, plant something.
4. Live log shows `plant.spawn` with roughly doubled HP.
