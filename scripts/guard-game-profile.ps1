# Refuse building/deploying a game profile against a pack whose fingerprints do not match.
# Usage:
#   .\scripts\guard-game-profile.ps1 -GameDir <pack> -ExpectedProfile pvzrh-3.9
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$ExpectedProfile,
    [string]$CatalogPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $CatalogPath) { $CatalogPath = Join-Path $Root "game-profiles.json" }
if (-not (Test-Path $CatalogPath)) { throw "Missing catalog: $CatalogPath" }
if (-not (Test-Path $GameDir)) { throw "GameDir missing: $GameDir" }

$json = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
$profile = $json.profiles | Where-Object { $_.id -eq $ExpectedProfile } | Select-Object -First 1
if (-not $profile) {
    Write-Host "GAME-PROFILE GUARD: unknown profile '$ExpectedProfile' — allowing (no fingerprint row)."
    exit 0
}

$ga = Join-Path $GameDir "GameAssembly.dll"
$gaLen = if (Test-Path $ga) { (Get-Item $ga).Length } else { -1 }

$matched = $false
foreach ($fp in @($profile.fingerprints)) {
    if ($null -eq $fp) { continue }
    if ($fp.gameAssemblyLength -and $gaLen -eq [long]$fp.gameAssemblyLength) {
        $matched = $true
        break
    }
    $paths = @($fp.assemblyCSharpPaths)
    $lens = @($fp.assemblyCSharpLengths)
    for ($i = 0; $i -lt $paths.Count -and $i -lt $lens.Count; $i++) {
        $acs = Join-Path $GameDir ($paths[$i] -replace '/', [IO.Path]::DirectorySeparatorChar)
        if ((Test-Path $acs) -and ((Get-Item $acs).Length -eq [long]$lens[$i])) {
            $matched = $true
            break
        }
    }
    if ($matched) { break }
}

if (-not $matched) {
    Write-Host "GAME-PROFILE GUARD FAILED — pack does not match profile '$ExpectedProfile'." -ForegroundColor Red
    Write-Host "  GameDir=$GameDir"
    Write-Host "  GameAssembly.dll length=$gaLen (expected one of catalog fingerprints)"
    Write-Host "  Set FUSIONRPG_GAME_PROFILE to the correct id or use the matching game pack."
    exit 1
}

Write-Host "GAME-PROFILE GUARD OK — $ExpectedProfile matches $GameDir"
exit 0
