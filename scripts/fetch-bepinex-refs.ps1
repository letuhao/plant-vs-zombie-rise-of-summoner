# Download official BepInEx Unity IL2CPP win-x64 zip for *reference assemblies only* (CI / path-free builds).
# Does not install into any game folder.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Tag = if ($env:BEPINEX_REF_TAG) { $env:BEPINEX_REF_TAG } else { "v6.0.0-pre.2" }
$AssetRegex = "BepInEx-Unity\.IL2CPP-win-x64"
$OutDir = Join-Path $Root "artifacts\bepinex-refs"
$ZipPath = Join-Path $Root "artifacts\bepinex-il2cpp.zip"

New-Item -ItemType Directory -Force -Path (Join-Path $Root "artifacts") | Out-Null
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }

$api = "https://api.github.com/repos/BepInEx/BepInEx/releases/tags/$Tag"
Write-Host "==> Fetching release $Tag"
$headers = @{ "User-Agent" = "FusionRpg-fetch-bepinex-refs/1.0"; "Accept" = "application/vnd.github+json" }
$release = Invoke-RestMethod -Uri $api -Headers $headers
$asset = $release.assets | Where-Object { $_.name -match $AssetRegex } | Select-Object -First 1
if (-not $asset) { throw "No asset matching $AssetRegex in $Tag" }

Write-Host "==> Downloading $($asset.name)"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $ZipPath -Headers $headers -UseBasicParsing

Write-Host "==> Extracting to $OutDir"
Expand-Archive -Path $ZipPath -DestinationPath $OutDir -Force
Remove-Item $ZipPath -Force

# Zip root may be a single folder; flatten if needed so BepInEx\core exists at OutDir
if (-not (Test-Path (Join-Path $OutDir "BepInEx\core"))) {
    $inner = Get-ChildItem $OutDir -Directory | Select-Object -First 1
    if ($inner -and (Test-Path (Join-Path $inner.FullName "BepInEx\core"))) {
        Get-ChildItem $inner.FullName | Move-Item -Destination $OutDir -Force
        Remove-Item $inner.FullName -Recurse -Force
    }
}

if (-not (Test-Path (Join-Path $OutDir "BepInEx\core"))) {
    throw "Extracted zip missing BepInEx\core under $OutDir"
}

# Interop assemblies are generated per-game; for building Injector against a real game,
# FUSIONRPG_GAME_DIR must still point at a game with interop. For CI we only have core —
# Injector build in CI requires a game interop OR we skip injector in CI.
Write-Host "Refs ready: $OutDir"
Write-Host "Note: Assembly-CSharp interop still requires a real game (FUSIONRPG_GAME_DIR) or CI cache of interop."
