# Player-pack smoke probe

Automation checks for an **unpacked player pack** (`dist/FusionRpg` or a Release unzip). This is **not** the SIM-only HTTP [`/api/test/probe`](probes.md).

| Probe | Role |
|---|---|
| HTTP `test.probe` / snapshot | SIM server events (`FUSIONRPG_SIM=1`) — E2E / fake injector |
| **Player-pack smoke** | Layout, loader/plugin offline, update data preserve, server boot with SIM **off** |

## What it checks

Core logic: [`PlayerPackProbe`](../../src/FusionRpg.Launcher/Services/PlayerPackProbe.cs)

1. **layout** — launcher/server exes, DropIntoGame injector, `loader-manifest.json`, `PLAYERS.txt`, `LICENSE`, `Server/wwwroot/index.html`
2. **manifest** — pins load; asset regexes non-empty
3. **loader_plugin** — temp BepInEx game tree; `LoaderProbe` OkForV1; `PluginInstaller` copies DropIntoGame
4. **dual_load** — Bep + Melon markers → `LoaderKind.Both`, blocks both installs
5. **update_preserve** — staged zip + `FusionRpgUpdater.PrepareApply` keeps `Server/data`
6. **server_boot** (script) — start `Server\FusionRpg.Server.exe`, `GET /health` with `ok` and `simEnabled` false; `/api/test/snapshot` must not return SIM JSON (SPA fallback is OK when SIM is off)

## How to run

```powershell
# After a local publish:
$env:FUSIONRPG_GAME_DIR = "<your game folder>"
.\scripts\publish-player.ps1
.\scripts\smoke-player-pack.ps1

# Offline probe only (no server process):
.\scripts\smoke-player-pack.ps1 -SkipServerBoot

# Console JSON only:
dotnet run --project tools\FusionRpg.PackSmoke -c Release -- dist\FusionRpg
```

Summary JSON: `artifacts/player-pack-smoke.json`.

Unit tests (always in CI, fake pack fixture): `dotnet test tests\FusionRpg.Launcher.Tests --filter PlayerPackProbe`.

Release workflow runs `smoke-player-pack.ps1` after `publish-player.ps1`, then zips `FusionRpg-win-x64.zip`.
