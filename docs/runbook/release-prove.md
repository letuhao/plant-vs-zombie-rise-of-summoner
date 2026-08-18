# Release prove-out checklist

Operator path: ship a GitHub Release and prove the player zip on a real game install.

## Before tagging

- [ ] `dotnet test tests\FusionRpg.Launcher.Tests -c Release` green (includes `PlayerPackProbe`)
- [ ] `$env:FUSIONRPG_GAME_DIR = "<legal game with BepInEx\interop>"`
- [ ] `.\scripts\publish-player.ps1`
- [ ] `.\scripts\smoke-player-pack.ps1` → `SMOKE PASSED` / `artifacts/player-pack-smoke.json`
- [ ] Cloud release: secret `FUSIONRPG_INTEROP_ZIP_URL` **or** committed `artifacts/ci-drop-into-game` (refresh via `scripts/sync-ci-drop-into-game.ps1`)

## Commit / push / tag

- [ ] Commit first-release + smoke probe changes (ask maintainer if unclear)
- [ ] Push to `main` (or release branch); wait for [CI](../../.github/workflows/ci.yml) green
- [ ] `git tag v1.0.0` (or next semver) and `git push origin v1.0.0`
- [ ] Confirm [release.yml](../../.github/workflows/release.yml) publishes `FusionRpg-win-x64.zip`

## Real deploy prove (manual)

Use a **clean folder** (not the repo `dist/`).

- [ ] Download `FusionRpg-win-x64.zip` from GitHub Releases
- [ ] Unzip → double-click `FusionRpg.Launcher.exe`
- [ ] **Browse** to legal game folder (`PlantsVsZombiesRH.exe`)
- [ ] **Install BepInEx** if missing (confirm dialog names the folder)
- [ ] **Play** — server starts, game starts, browser opens RPG UI
- [ ] Status lights: Server online; Injector connected after load
- [ ] (Optional) Newer tag: **Download & install FusionRpg update** — `Server\data` kept; launcher restarts
- [ ] Dual-load: do **not** install MelonLoader into the same Bep pack

### Result

| Check | Pass / Fail | Notes |
|---|---|---|
| Smoke script (local or CI) | | |
| Fresh unzip Play | | |
| Injector connected | | |
| Update preserve data (optional) | | |

Date / tag: _______________
