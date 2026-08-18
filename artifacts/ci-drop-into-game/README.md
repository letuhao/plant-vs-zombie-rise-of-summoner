# CI DropIntoGame cache

Prebuilt FusionRpg plugin DLLs for GitHub Actions release when `FUSIONRPG_INTEROP_ZIP_URL` is unset.

Refresh after injector changes:

```powershell
$env:FUSIONRPG_GAME_DIR = "<legal game with BepInEx\interop>"
./scripts/sync-ci-drop-into-game.ps1
```

Do **not** put `BepInEx\interop` game DLLs here.
