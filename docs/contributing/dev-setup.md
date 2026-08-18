# Developer setup

## Prerequisites

| Tool | Why |
|---|---|
| .NET 8 SDK | Server, Launcher, most tests |
| .NET 6 targeting pack | Injector (`net6.0`) |
| Node.js 20+ / npm | Build `web/fusion-rpg-web` into server `wwwroot` |
| Legal PVZ Fusion install with BepInEx | Injector compile refs (`BepInEx\core` + `BepInEx\interop`) |

Players do **not** need any of these — they use the release zip.

## Clone and build

```powershell
git clone https://github.com/letuhao/plant-vs-zombie-rise-of-summoner.git
cd plant-vs-zombie-rise-of-summoner
dotnet build
```

## Tests (same commands CI runs)

```powershell
dotnet test tests/FusionRpg.Core.Tests -c Release
dotnet test tests/FusionRpg.Data.Tests -c Release
dotnet test tests/FusionRpg.CheatCore.Tests -c Release
dotnet test tests/FusionRpg.Guard.Tests -c Release
dotnet test tests/FusionRpg.Launcher.Tests -c Release
```

## Injector refs (no hardcoded game path)

Set an environment variable to your game folder (must contain `BepInEx\core` and `BepInEx\interop`):

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder>"
```

Or run:

```powershell
.\scripts\prepare-injector-refs.ps1
```

That downloads official BepInEx core into `artifacts\bepinex-refs` and copies interop from `FUSIONRPG_GAME_DIR` (or from `FUSIONRPG_INTEROP_ZIP_URL` in CI). Do **not** commit interop DLLs.

## Player zip

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder>"
$env:FUSIONRPG_VERSION = "1.0.0"   # optional
.\scripts\publish-player.ps1
```

Output: `dist/FusionRpg/` (zip this folder as `FusionRpg-win-x64.zip` for releases).

Smoke before tagging:

```powershell
.\scripts\smoke-player-pack.ps1
```

See [player-pack-smoke.md](../testing/player-pack-smoke.md) and [release-prove.md](../runbook/release-prove.md).

## Release tagging

Push a tag `v*` (e.g. `v1.0.0`). GitHub Actions [`.github/workflows/release.yml`](../../.github/workflows/release.yml) runs unit tests, then publishes the zip.

**Cloud runners** prefer repository secret `FUSIONRPG_INTEROP_ZIP_URL` (private zip of `BepInEx\interop` including `Assembly-CSharp.dll`) so the Injector rebuilds from source. If that secret is unset, Release falls back to committed `artifacts/ci-drop-into-game` (AGPL plugin DLLs only — refresh with `scripts/sync-ci-drop-into-game.ps1`). Do not commit interop DLLs to git.
