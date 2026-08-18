# Build a zip folder players can use with no Node and no .NET SDK / Desktop Runtime.
# Injector refs: set FUSIONRPG_GAME_DIR to a folder that contains BepInEx\core + BepInEx\interop,
# or leave unset to try the parent of this repo (common local layout). CI downloads BepInEx for refs.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Version = if ($env:FUSIONRPG_VERSION) { $env:FUSIONRPG_VERSION.TrimStart("v", "V") } else { "1.0.0" }
$Out = Join-Path $Root "dist\FusionRpg"
$ServerOut = Join-Path $Out "Server"
$Web = Join-Path $Root "web\fusion-rpg-web"
$PluginOut = Join-Path $Root "artifacts\plugins\FusionRpg"
$ManifestOut = Join-Path $Out "loader-manifest.json"

function Resolve-GameDir {
    if ($env:FUSIONRPG_USE_CI_DROP -eq "1") {
        return $null
    }
    if ($env:FUSIONRPG_GAME_DIR -and (Test-Path $env:FUSIONRPG_GAME_DIR)) {
        return (Resolve-Path $env:FUSIONRPG_GAME_DIR).Path
    }
    $parent = Join-Path $Root ".."
    if (Test-Path (Join-Path $parent "BepInEx\core")) {
        return (Resolve-Path $parent).Path
    }
    $ciRefs = Join-Path $Root "artifacts\bepinex-refs"
    if (Test-Path (Join-Path $ciRefs "BepInEx\core")) {
        return (Resolve-Path $ciRefs).Path
    }
    throw @"
No BepInEx reference tree found.
Set FUSIONRPG_GAME_DIR to a game folder with BepInEx\core and BepInEx\interop,
or run scripts/fetch-bepinex-refs.ps1 for CI-style refs under artifacts\bepinex-refs,
or set FUSIONRPG_USE_CI_DROP=1 with artifacts/ci-drop-into-game present.
"@
}

$GameDir = Resolve-GameDir
if ($GameDir) {
    Write-Host "==> Injector GameDir (refs only): $GameDir"
}
else {
    Write-Host "==> Using committed artifacts/ci-drop-into-game (no injector rebuild)"
}

Write-Host "==> Building web UI (static files into server wwwroot)"
Push-Location $Web
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required on the developer PC to build the UI. Players will not need npm."
}
if (-not (Test-Path "node_modules")) { npm install }
npm run build
Pop-Location

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
New-Item -ItemType Directory -Force -Path $ServerOut | Out-Null
if (Test-Path $PluginOut) { Remove-Item $PluginOut -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PluginOut | Out-Null

Write-Host "==> Publishing self-contained server (win-x64) -> Server\  version=$Version"
dotnet publish (Join-Path $Root "src\FusionRpg.Server\FusionRpg.Server.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -o $ServerOut

Write-Host "==> Publishing self-contained launcher (win-x64) -> FusionRpg\"
dotnet publish (Join-Path $Root "src\FusionRpg.Launcher\FusionRpg.Launcher.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -o $Out

$WwwFromServer = Join-Path $ServerOut "wwwroot"
if (-not (Test-Path $WwwFromServer)) {
    $builtWww = Join-Path $Root "src\FusionRpg.Server\wwwroot"
    if (Test-Path $builtWww) {
        Copy-Item $builtWww $WwwFromServer -Recurse -Force
    }
}

Write-Host "==> Building BepInEx injector plugin -> artifacts\plugins\FusionRpg"
$CiDrop = Join-Path $Root "artifacts\ci-drop-into-game"
$BepProj = Join-Path $Root "src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj"
if ($env:FUSIONRPG_USE_CI_DROP -eq "1") {
    if (-not (Test-Path (Join-Path $CiDrop "FusionRpg.Injector.dll"))) {
        throw "FUSIONRPG_USE_CI_DROP=1 but missing $CiDrop\FusionRpg.Injector.dll — run scripts/sync-ci-drop-into-game.ps1 locally."
    }
    if (Test-Path $PluginOut) { Remove-Item $PluginOut -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PluginOut | Out-Null
    Copy-Item (Join-Path $CiDrop "*") $PluginOut -Force
    Write-Host "==> Copied prebuilt DropIntoGame from $CiDrop"
}
else {
    if (-not $GameDir) { throw "GameDir required to build injector (or set FUSIONRPG_USE_CI_DROP=1)." }
    dotnet build $BepProj `
        -c Release `
        -p:GameDir=$GameDir `
        -p:Version=$Version `
        -p:InformationalVersion=$Version `
        -p:OutputPath=$PluginOut\

    if (-not (Test-Path (Join-Path $PluginOut "FusionRpg.Injector.dll"))) {
        throw "Injector output missing: $PluginOut\FusionRpg.Injector.dll"
    }
}

# Nested DropIntoGame layout by game profile + loader. Keep legacy flat / unscoped paths for 3.8.1 Bep.
$Drop = Join-Path $Out "DropIntoGame"
$DropBepLegacy = Join-Path $Drop "BepInEx"
$DropBep381 = Join-Path $Drop "pvzrh-3.8.1\BepInEx"
New-Item -ItemType Directory -Force -Path $DropBepLegacy | Out-Null
New-Item -ItemType Directory -Force -Path $DropBep381 | Out-Null
Get-ChildItem $PluginOut -File | Where-Object {
    $_.Extension -in ".dll", ".json", ".pdb"
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $DropBepLegacy -Force
    Copy-Item $_.FullName -Destination $DropBep381 -Force
    Copy-Item $_.FullName -Destination $Drop -Force  # legacy flat DropIntoGame\
}

# Optional MelonLoader drop when FUSIONRPG_ML_GAMEDIR is set
$MlDir = $env:FUSIONRPG_ML_GAMEDIR
$GameProfile = if ($env:FUSIONRPG_GAME_PROFILE) { $env:FUSIONRPG_GAME_PROFILE } else { "pvzrh-3.8.1" }
if ($MlDir -and (Test-Path (Join-Path $MlDir "MelonLoader\net6\MelonLoader.dll"))) {
    # Auto-detect 3.9 by GameAssembly size when profile not forced
    $ga = Join-Path $MlDir "GameAssembly.dll"
    if (-not $env:FUSIONRPG_GAME_PROFILE -and (Test-Path $ga) -and ((Get-Item $ga).Length -eq 57717248)) {
        $GameProfile = "pvzrh-3.9"
    }
    Write-Host "==> Building MelonLoader injector profile=$GameProfile -> DropIntoGame\$GameProfile\MelonLoader"
    if ($GameProfile -eq "pvzrh-3.9") {
        $MelonProj = Join-Path $Root "src\FusionRpg.Injector.MelonLoader.39\FusionRpg.Injector.MelonLoader.39.csproj"
        $MelonDllName = "FusionRpg.Injector.MelonLoader.39.dll"
    }
    else {
        $MelonProj = Join-Path $Root "src\FusionRpg.Injector.MelonLoader\FusionRpg.Injector.MelonLoader.csproj"
        $MelonDllName = "FusionRpg.Injector.MelonLoader.dll"
    }
    $PluginMelon = Join-Path $Root "artifacts\plugins\MelonLoader"
    if (Test-Path $PluginMelon) { Remove-Item $PluginMelon -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $PluginMelon | Out-Null
    & (Join-Path $Root "scripts\guard-game-profile.ps1") -GameDir $MlDir -ExpectedProfile $GameProfile
    if ($LASTEXITCODE -ne 0) { throw "game-profile guard failed for Melon pack" }
    dotnet build $MelonProj `
        -c Release `
        -p:MlGameDir=$MlDir `
        -p:GameProfile=$GameProfile `
        -p:Version=$Version `
        -p:InformationalVersion=$Version `
        -p:OutputPath=$PluginMelon\
    $DropMelonScoped = Join-Path $Drop "$GameProfile\MelonLoader"
    $DropMelonLegacy = Join-Path $Drop "MelonLoader"
    New-Item -ItemType Directory -Force -Path $DropMelonScoped | Out-Null
    if ($GameProfile -eq "pvzrh-3.8.1") {
        New-Item -ItemType Directory -Force -Path $DropMelonLegacy | Out-Null
    }
    Get-ChildItem $PluginMelon -File | Where-Object {
        $_.Extension -in ".dll", ".json", ".pdb", ".cfg"
    } | ForEach-Object {
        Copy-Item $_.FullName -Destination $DropMelonScoped -Force
        if ($GameProfile -eq "pvzrh-3.8.1") {
            Copy-Item $_.FullName -Destination $DropMelonLegacy -Force
        }
    }
    if (-not (Test-Path (Join-Path $DropMelonScoped $MelonDllName))) {
        Write-Warning "MelonLoader injector DLL missing after build — Melon drop skipped."
        Remove-Item $DropMelonScoped -Recurse -Force -ErrorAction SilentlyContinue
    }
}
else {
    Write-Host "==> Skipping MelonLoader drop (set FUSIONRPG_ML_GAMEDIR to include it)"
}

$ManifestSrc = Join-Path $Root "src\FusionRpg.Launcher\loader-manifest.json"
if (Test-Path $ManifestSrc) {
    Copy-Item $ManifestSrc $ManifestOut -Force
}
$ProfilesSrc = Join-Path $Root "game-profiles.json"
if (Test-Path $ProfilesSrc) {
    Copy-Item $ProfilesSrc (Join-Path $Out "game-profiles.json") -Force
}

Copy-Item (Join-Path $Root "docs\runbook\PLAYERS.txt") (Join-Path $Out "PLAYERS.txt") -Force
Copy-Item (Join-Path $Root "LICENSE") (Join-Path $Out "LICENSE") -Force
if (Test-Path (Join-Path $Root "NOTICE")) {
    Copy-Item (Join-Path $Root "NOTICE") (Join-Path $Out "NOTICE") -Force
}

Get-ChildItem $Out -Recurse -Include "*.pdb" | Remove-Item -Force
$ServerData = Join-Path $ServerOut "data"
if (Test-Path $ServerData) {
    Remove-Item $ServerData -Recurse -Force
}

Write-Host ""
Write-Host "Player folder: $Out"
Write-Host "Version: $Version"
Write-Host "Players: double-click FusionRpg.Launcher.exe (no Node, no .NET SDK, no Desktop Runtime)."
Write-Host "Launcher copies DropIntoGame into BepInEx\plugins\FusionRpg and starts Server\FusionRpg.Server.exe."
