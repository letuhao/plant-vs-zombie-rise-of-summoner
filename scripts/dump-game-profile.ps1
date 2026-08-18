# Dump ACS symbols for a game profile (Melon Il2CppAssemblies or Bep interop).
# Usage:
#   $env:FUSIONRPG_ML_GAMEDIR = "<Melon pack>"
#   .\scripts\dump-game-profile.ps1
#   .\scripts\dump-game-profile.ps1 -ProfileId pvzrh-3.9
param(
    [string]$MlGameDir = $env:FUSIONRPG_ML_GAMEDIR,
    [string]$BepGameDir = $(if ($env:FUSIONRPG_GAME_DIR) { $env:FUSIONRPG_GAME_DIR } else { "" }),
    [string]$ProfileId = $(if ($env:FUSIONRPG_GAME_PROFILE) { $env:FUSIONRPG_GAME_PROFILE } else { "auto" })
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "dump-melon-p0.ps1") -MlGameDir $MlGameDir -BepGameDir $(if ($BepGameDir) { $BepGameDir } else { (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path })
Write-Host "Profile hint: $ProfileId — update docs/research/game-types-*.md and game-profiles.json fingerprints."
