# Build Injector against a legal game (or FUSIONRPG_GAME_DIR) and copy DropIntoGame
# DLLs into artifacts/ci-drop-into-game for cloud releases without interop secrets.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Version = if ($env:FUSIONRPG_VERSION) { $env:FUSIONRPG_VERSION.TrimStart("v", "V") } else { "0.1.0" }
$Out = Join-Path $Root "artifacts\ci-drop-stage"
$env:FUSIONRPG_USE_CI_DROP = $null
Remove-Item Env:FUSIONRPG_USE_CI_DROP -ErrorAction SilentlyContinue

# Reuse publish-player injector build path via a temp publish then copy DropIntoGame only.
$env:FUSIONRPG_VERSION = $Version
& (Join-Path $PSScriptRoot "publish-player.ps1")

$Drop = Join-Path $Root "dist\FusionRpg\DropIntoGame"
$Dest = Join-Path $Root "artifacts\ci-drop-into-game"
if (-not (Test-Path (Join-Path $Drop "FusionRpg.Injector.dll"))) {
    throw "Missing DropIntoGame after publish: $Drop"
}
if (Test-Path $Dest) { Remove-Item $Dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Dest | Out-Null
Get-ChildItem $Drop -File | Where-Object { $_.Extension -in ".dll", ".json" } |
    Copy-Item -Destination $Dest -Force

# Keep a tiny readme (no game interop — our AGPL plugin binaries only)
@"
# CI DropIntoGame cache

Prebuilt FusionRpg plugin DLLs for GitHub Actions release when ``FUSIONRPG_INTEROP_ZIP_URL`` is unset.

Refresh after injector changes:

``````powershell
`$env:FUSIONRPG_GAME_DIR = "<legal game with BepInEx\interop>"
./scripts/sync-ci-drop-into-game.ps1
``````

Do **not** put ``BepInEx\interop`` game DLLs here.
"@ | Set-Content -Path (Join-Path $Dest "README.md") -Encoding utf8

Write-Host "Synced $Dest"
Get-ChildItem $Dest | Format-Table Name, Length
