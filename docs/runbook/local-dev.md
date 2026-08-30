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
`-NoServer`, `-NoRebuildUi` (skip the web UI build), `-RestartServer`.

SQLite for this session: `dist/FusionRpg.Server/data/rpg-hot.sqlite` + `rpg-media.sqlite` (beside the published exe; gitignored). Icons/almanac are BLOBs in the media file.

Do not use the Simulator tab in the same session as the real injector.

## End-to-end check (real game)

1. Start server (and Vite only if you are editing UI).
2. Set plant `hpPercent` to `2`, Save, Push.
3. Start a level, plant something.
4. Live log shows `plant.spawn` with roughly doubled HP.
